import pandas as pd  # type: ignore
import os
import sys
import warnings
import io
from pathlib import Path

# 윈도우 콘솔 출력 자동 설정 (Unicode 대응)
# 윈도우 콘솔 출력 자동 설정 (Unicode 대응)
if sys.stdout.encoding.lower() != 'utf-8':
    _reconfig = getattr(sys.stdout, 'reconfigure', None)
    if callable(_reconfig):
        _reconfig(encoding='utf-8')
        _reconfig_err = getattr(sys.stderr, 'reconfigure', None)
        if callable(_reconfig_err):
            _reconfig_err(encoding='utf-8')
    else:
        try:
            sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
            sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')
        except (AttributeError, io.UnsupportedOperation):
            pass

warnings.filterwarnings("ignore", category=UserWarning, module="openpyxl")

def find_best_sheet(xl):
    sheet_names = xl.sheet_names
    # 보고서용 시트 키워드
    keywords = ['★', 'REPORT', 'PL', '손익', '종합', 'TOTAL', 'RESULT']
    for kw in keywords:
        for s in sheet_names:
            if kw in s.upper(): return s
    return sheet_names[0]

def smart_refine(df):
    """
    데이터를 함부로 지우지 않고, 파워쿼리가 읽기 좋게 다듬기만 하는 로직
    """
    # 1. 완전히 비어있는 행/열 제거
    df = df.dropna(how='all', axis=0).dropna(how='all', axis=1)
    
    if df.empty:
        return df

    # 2. 상단 노이즈 제거 (데이터가 시작되는 진짜 행 찾기)
    # 각 행의 유효 데이터(NaN이 아닌 값) 개수를 셈
    valid_counts = df.notnull().sum(axis=1)
    # 유효 데이터가 전체 컬럼의 30% 이상인 첫 번째 행을 찾음
    start_idx = 0
    for i, count in enumerate(valid_counts):
        if count >= df.shape[1] * 0.3:
            start_idx = i
            break
    
    # 3. 발견된 행부터 데이터 시작
    df = df.iloc[start_idx:].reset_index(drop=True)
    
    # 4. 첫 줄을 제목으로 올리기 (제목이 Unnamed 형태인 경우에만)
    # 만약 현재 컬럼명이 Unnamed 계열이면 첫 번째 행을 컬럼명으로 사용 시도
    if any('Unnamed' in str(c) for c in df.columns):
        new_header = df.iloc[0].fillna('')
        # 제목줄이 너무 비어있지 않은지 체크
        if new_header.notnull().sum() > 0:
            df.columns = [str(c).strip() for c in new_header]
            df = df[1:].reset_index(drop=True)

    # 5. 컬럼명 정제 (중복 방지 및 줄바꿈 제거)
    new_cols = []
    for i, col in enumerate(df.columns):
        col_name = str(col).strip().replace('\n', ' ')
        if not col_name or 'Unnamed' in col_name:
            col_name = f"Column_{i}"
        new_cols.append(col_name)
    
    # 중복 이름 뒤에 번호 붙이기
    final_cols = []
    seen = {}
    for col in new_cols:
        if col in seen:
            seen[col] += 1
            final_cols.append(f"{col}_{seen[col]}")
        else:
            seen[col] = 0
            final_cols.append(col)
    df.columns = final_cols

    # 6. 데이터 좌우 공백 제거 및 행 보존
    df = df.apply(lambda x: x.str.strip() if x.dtype == "object" else x)
    
    return df

# 파일명 패턴별 데이터 시작 마커 설정
# (marker_text, include_marker_row)
#   include_marker_row=True  → 마커 행 포함해서 읽기  (ksccz)
#   include_marker_row=False → 마커 행 제외하고 다음 행부터 읽기 (ksci)
FILE_MARKERS = {
    'ksccz': ('2026年 业绩 _ 损益表 _EV', True),
    'ksci':  ('▶ KSCM-GP (전장)',          False),
}

def apply_marker(df, file_stem):
    """파일명에 해당하는 마커가 있으면 그 위치부터 df를 잘라 반환"""
    stem_lower = file_stem.lower()
    for key, (marker, include) in FILE_MARKERS.items():
        if key in stem_lower:
            # 모든 셀을 문자열로 변환해 마커 텍스트 검색
            mask = df.apply(
                lambda col: col.astype(str).str.contains(marker, regex=False, na=False)
            ).any(axis=1)
            matched = mask[mask].index
            if len(matched) == 0:
                print(f"    ⚠️ 마커를 찾지 못했습니다: '{marker}'")
                return df
            start = matched[0] if include else matched[0] + 1
            print(f"    📌 마커 발견 ('{marker}') → {start}행부터 읽음")
            return df.iloc[start:].reset_index(drop=True)
    return df

def process_file(file_path, output_dir):
    print(f"  [처리] {file_path.name}")
    try:
        ext = file_path.suffix.lower()
        if ext == '.csv':
            df = pd.read_csv(file_path)
            sheet_name = "CSV"
        else:
            xl = pd.ExcelFile(file_path)
            sheet_name = find_best_sheet(xl)
            # header=None으로 읽어서 직접 제어
            df = pd.read_excel(file_path, sheet_name=sheet_name, header=None)

        # 법인별 마커 적용 (해당 없으면 그대로)
        df = apply_marker(df, file_path.stem)

        refined_df = smart_refine(df)
        
        if refined_df.empty:
            print(f"    ⚠️ 경고: 추출된 데이터가 없습니다.")
            return False
            
        output_name = f"Refined_{file_path.stem}.xlsx"
        refined_df.to_excel(output_dir / output_name, index=False)
        print(f"    ✅ 완료: {sheet_name} ({refined_df.shape[0]}행 x {refined_df.shape[1]}열)")
        return True
    except Exception as e:
        print(f"    ❌ 실패: {str(e)}")
        return False

def main(input_path):
    print(f"START: 정제 시스템 가동 - 경로: {input_path}")
    target_dir = Path(input_path)
    if not target_dir.exists():
        print(f"ERROR: 경로를 찾을 수 없습니다.")
        return

    output_dir = target_dir / "Refined_Results"
    output_dir.mkdir(exist_ok=True)

    files = [f for f in target_dir.glob("*") if f.suffix.lower() in ['.xlsx', '.xls', '.csv']]
    files = [f for f in files if not f.name.startswith('~$') and "Refined" not in f.name]

    success_files = []
    for f in files:
        if process_file(f, output_dir):
            success_files.append(str(f))

    print(f"\nFINISH: 총 {len(success_files)}개 파일 정제 완료.")
    print(f"PATH: {output_dir}")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        main(sys.argv[1])
    else:
        print("ERROR: 경로 인자가 없습니다.")
