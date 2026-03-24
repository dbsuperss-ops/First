"""
KSC Refiner v1.3 - Main Engine
통합 데이터 처리 엔진
"""
import os
import sys
import logging
from typing import List, Optional
from datetime import datetime

from core.schema import AccountRow, detect_company, validate_row
from core.config_loader import ConfigLoader
from core.output_writer import OutputWriter
from parsers.parser_factory import ParserFactory


class RefinerEngine:
    """KSC Refiner 메인 엔진"""

    VERSION = "1.3"

    def __init__(self, config_dir: str = None):
        """
        Args:
            config_dir: 설정 디렉토리 경로 (None이면 자동 감지)
        """
        self.config_loader = ConfigLoader(config_dir)
        self.output_writer = OutputWriter()
        self.logger = logging.getLogger("RefinerEngine")

    def find_excel_files(self, input_dir: str, year: str = None) -> List[str]:
        """
        입력 디렉토리에서 엑셀 파일 탐색

        Args:
            input_dir: 입력 디렉토리
            year: 필터링할 연도 (None이면 전체)

        Returns:
            파일 경로 리스트
        """
        files = []
        for root, dirs, filenames in os.walk(input_dir):
            for fn in filenames:
                # 임시 파일 제외
                if fn.startswith("~$") or "consolidated" in fn.lower():
                    continue
                # 엑셀 파일만
                if fn.lower().endswith((".xlsx", ".xlsm")):
                    # 법인코드가 있는 파일만
                    if detect_company(fn):
                        files.append(os.path.join(root, fn))

        # 연도 필터링
        if year:
            year_filtered = [
                f for f in files
                if os.sep + year + os.sep in f or "/" + year + "/" in f
            ]
            if year_filtered:
                files = year_filtered

        return sorted(set(files))

    def process_file(self, filepath: str, year: str) -> List[AccountRow]:
        """
        단일 파일 처리

        Args:
            filepath: 파일 경로
            year: 기준 연도

        Returns:
            추출된 AccountRow 리스트
        """
        filename = os.path.basename(filepath)
        company = detect_company(filename)

        if not company:
            self.logger.warning(f"⚠ 법인코드 미식별: {filename}")
            return []

        try:
            # 파서 설정 및 환율 로드
            parser_config = self.config_loader.get_parser_config(company)
            rates = self.config_loader.load_rates(year)

            # 파서 생성 및 실행
            parser = ParserFactory.create_parser(
                filepath=filepath,
                company_code=company,
                parser_config=parser_config,
                rates=rates
            )

            self.logger.info(f"📄 {filename} → {company} 파서 실행...")
            rows = parser.extract()

            # 데이터 검증
            valid_rows = [r for r in rows if validate_row(r)]
            if len(valid_rows) != len(rows):
                self.logger.warning(f"  ⚠ {len(rows) - len(valid_rows)}건 검증 실패")

            self.logger.info(f"  ✅ {len(valid_rows)}건 추출 완료")
            return valid_rows

        except Exception as e:
            self.logger.error(f"  ❌ 파싱 실패: {e}", exc_info=True)
            return []

    def run(self, input_dir: str, output_dir: str = None, year: str = "2026") -> Optional[str]:
        """
        메인 실행 함수

        Args:
            input_dir: 입력 디렉토리
            output_dir: 출력 디렉토리 (None이면 자동 설정)
            year: 기준 연도

        Returns:
            출력 파일 경로 또는 None
        """
        # 출력 디렉토리 설정
        if output_dir is None:
            if getattr(sys, 'frozen', False):
                base_path = os.path.dirname(sys.executable)
            else:
                base_path = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
            output_dir = os.path.join(base_path, "output")

        os.makedirs(output_dir, exist_ok=True)

        # 배너 출력
        print("=" * 60)
        print(f"🚀 KSC Settlement Refiner Engine v{self.VERSION}")
        print(f"   루트 경로: {input_dir}")
        print(f"   기준 연도: {year}")
        print(f"   출력 경로: {output_dir}")
        print("=" * 60)

        # 환율 정보
        rates = self.config_loader.load_rates(year)
        print(f"\n📌 적용 환율: {rates}")

        # 파일 탐색
        files = self.find_excel_files(input_dir, year)
        if not files:
            print("\n❌ 처리할 엑셀 파일이 없습니다.")
            return None

        print(f"\n📂 탐지된 파일: {len(files)}개")
        for f in files:
            print(f"   {os.path.relpath(f, input_dir)}")

        # 파일 처리
        all_rows = []
        for filepath in sorted(files):
            rows = self.process_file(filepath, year)
            all_rows.extend(rows)

        if not all_rows:
            print("\n❌ 추출된 데이터가 없습니다.")
            return None

        # 통합 파일 생성
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output_path = os.path.join(output_dir, f"consolidated_db_{timestamp}.xlsx")

        print(f"\n📊 통합 DB 생성 중...")
        self.output_writer.write_consolidated(all_rows, output_path)

        # 요약
        companies = sorted(set(r.법인코드 for r in all_rows))
        print(f"\n✅ 완료!")
        print(f"   출력 파일: {output_path}")
        print(f"   총 데이터: {len(all_rows)}건")
        print(f"   법인 수: {len(companies)}개 ({', '.join(companies)})")

        return output_path


def setup_logging(level=logging.INFO):
    """로깅 설정"""
    # UTF-8 인코딩 설정
    if sys.stdout.encoding != 'utf-8':
        sys.stdout.reconfigure(encoding='utf-8')
    if sys.stderr.encoding != 'utf-8':
        sys.stderr.reconfigure(encoding='utf-8')

    logging.basicConfig(
        level=level,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        handlers=[
            logging.StreamHandler(sys.stdout)
        ]
    )


def main():
    """CLI 진입점"""
    setup_logging(logging.WARNING)  # 엔진 실행 시 WARNING 이상만 표시

    # 인자 파싱
    input_dir = sys.argv[1] if len(sys.argv) > 1 else None
    year = sys.argv[2] if len(sys.argv) > 2 else "2026"

    if not input_dir:
        print("사용법: python engine.py <입력디렉토리> [연도]")
        sys.exit(1)

    # 엔진 실행
    engine = RefinerEngine()
    output_path = engine.run(input_dir=input_dir, year=year)

    if output_path:
        sys.exit(0)
    else:
        sys.exit(1)


if __name__ == "__main__":
    main()
