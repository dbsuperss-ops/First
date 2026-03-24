"""
KSC Refiner v1.3 - Core Schema
데이터 모델 및 기본 상수 정의
"""
from dataclasses import dataclass, asdict
from typing import List, Optional
import re

# 통합 DB 컬럼 정의
MASTER_COLUMNS = [
    "귀속연월", "법인코드", "데이터타입", "대분류", "중분류", "계정과목",
    "현지통화", "현지금액", "적용환율", "KRW금액"
]

# 대분류 카테고리
CATEGORY_PL = "PL"  # 손익계산서
CATEGORY_MC = "MC"  # 제조원가


@dataclass
class AccountRow:
    """계정과목 데이터 행"""
    귀속연월: str      # YYYY-MM
    법인코드: str      # KSCP, KCTR, ...
    데이터타입: str    # "실적" or "계획"
    대분류: str        # PL, MC
    중분류: str        # 매출, 매출원가, 이익, ...
    계정과목: str      # 매출액, 영업이익, ...
    현지통화: str      # KRW, EUR, USD, ...
    현지금액: float
    적용환율: float
    KRW금액: float

    def to_list(self) -> List:
        """엑셀 출력용 리스트 변환"""
        return [
            self.귀속연월, self.법인코드, self.데이터타입, self.대분류, self.중분류,
            self.계정과목, self.현지통화, self.현지금액, self.적용환율, self.KRW금액
        ]

    def to_dict(self) -> dict:
        """딕셔너리 변환"""
        return asdict(self)


def safe_float(val) -> float:
    """안전한 float 변환"""
    if val is None or val == "" or val == "-":
        return 0.0
    if isinstance(val, (int, float)):
        return float(val)
    try:
        cleaned = str(val).replace(",", "").replace(" ", "").strip()
        if cleaned == "" or cleaned == "-":
            return 0.0
        return float(cleaned)
    except (ValueError, TypeError):
        return 0.0


def detect_company(filename: str) -> Optional[str]:
    """파일명에서 법인코드 추출"""
    name = filename.upper()
    # 우선순위: 긴 이름부터 매칭 (KSCCZ가 KSCP보다 먼저)
    for code in ["KSCCZ", "KSCP", "KSCE", "KSCI", "KCTR"]:
        if code in name:
            return code
    return None


def detect_year_from_filename(filename: str) -> Optional[str]:
    """파일명에서 연도 추출"""
    m = re.search(r'(\d{4})년', filename)
    if m:
        return m.group(1)
    m = re.search(r'_(\d{4})_', filename)
    if m:
        return m.group(1)
    return None


def detect_year_from_sheet(ws, search_rows: int = 10) -> str:
    """시트에서 연도 감지"""
    for row in ws.iter_rows(min_row=1, max_row=search_rows, values_only=True):
        for cell in row:
            if cell and isinstance(cell, str):
                # "FY 2026" 형식
                m = re.search(r'FY\s*(\d{4})', str(cell))
                if m and m.group(1) in ("2024", "2025", "2026", "2027", "2028"):
                    return m.group(1)
                # "2026년" 형식
                m = re.search(r'(\d{4})년', str(cell))
                if m and m.group(1) in ("2024", "2025", "2026", "2027", "2028"):
                    return m.group(1)
    return "2026"  # 기본값


def validate_row(row: AccountRow) -> bool:
    """데이터 행 검증"""
    if not row.귀속연월 or not re.match(r'\d{4}-\d{2}', row.귀속연월):
        return False
    if not row.법인코드:
        return False
    if row.데이터타입 not in ("실적", "계획"):
        return False
    if row.대분류 not in (CATEGORY_PL, CATEGORY_MC):
        return False
    return True
