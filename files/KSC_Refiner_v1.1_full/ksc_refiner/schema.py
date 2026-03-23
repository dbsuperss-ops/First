import json
import os
import sys
from dataclasses import dataclass, asdict
from typing import Optional

MASTER_COLUMNS = [
    "귀속연월", "법인코드", "데이터타입", "대분류", "중분류", "계정과목",
    "현지통화", "현지금액", "적용환율", "KRW금액"
]

CATEGORY_PL = "PL"
CATEGORY_MC = "MC"

@dataclass
class AccountRow:
    귀속연월: str
    법인코드: str
    데이터타입: str      # "실적" or "계획"
    대분류: str
    중분류: str
    계정과목: str
    현지통화: str
    현지금액: float
    적용환율: float
    KRW금액: float

    def to_list(self):
        return [
            self.귀속연월, self.법인코드, self.데이터타입, self.대분류, self.중분류,
            self.계정과목, self.현지통화, self.현지금액, self.적용환율, self.KRW금액
        ]

def load_rates(year: str, config_dir: str = None) -> dict:
    if config_dir is None:
        # PyInstaller 실행 시 번들 리소스 경로 사용
        if getattr(sys, 'frozen', False):
            # PyInstaller로 패키징된 경우
            base_path = sys._MEIPASS
        else:
            # 일반 Python 실행 시
            base_path = os.path.dirname(__file__)
        config_dir = os.path.join(base_path, "config")

    path = os.path.join(config_dir, "rates.json")
    with open(path, "r", encoding="utf-8") as f:
        all_rates = json.load(f)
    return all_rates.get(str(year), all_rates.get("2026"))

def safe_float(val) -> float:
    if val is None or val == "" or val == "-":
        return 0.0
    if isinstance(val, (int, float)):
        return float(val)
    try:
        cleaned = str(val).replace(",", "").replace(" ", "").strip()
        return float(cleaned)
    except (ValueError, TypeError):
        return 0.0

def detect_company(filename: str) -> Optional[str]:
    name = filename.upper()
    for code in ["KSCP", "KSCCZ", "KSCE", "KSCI", "KCTR"]:
        if code in name:
            return code
    return None

def detect_month(filename: str) -> Optional[str]:
    import re
    m = re.search(r'_(\d{1,2})_', filename)
    if m:
        return m.group(1).zfill(2)
    return None

def detect_year_from_sheet(ws, search_rows=10) -> str:
    import re
    for row in ws.iter_rows(min_row=1, max_row=search_rows, values_only=True):
        for cell in row:
            if cell and isinstance(cell, str):
                m = re.search(r'FY\s*(\d{4})', str(cell))
                if m:
                    return m.group(1)
                m = re.search(r'(\d{4})년', str(cell))
                if m:
                    return m.group(1)
    return "2026"
