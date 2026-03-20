import openpyxl
import re
from typing import List
from parsers import BaseParser
from schema import AccountRow, safe_float, CATEGORY_PL, CATEGORY_MC

# PL 시트: col B 레이블 → (대분류, 중분류, 계정과목)
PL_LABEL_MAP = {
    "Ⅰ. 매출액":     (CATEGORY_PL, "매출",   "매출액"),
    "Ⅱ. 매출원가":   (CATEGORY_PL, "매출원가", "매출원가"),
    "Ⅲ. 매출총이익": (CATEGORY_PL, "이익",   "매출총이익"),
    "Ⅳ.판매관리비":  (CATEGORY_PL, "판관비",  "판관비계"),
    "Ⅴ.영업이익":    (CATEGORY_PL, "이익",   "영업이익"),
}

# MC 시트: col B 레이블 → (대분류, 중분류, 계정과목)
MC_LABEL_MAP = {
    "Ⅰ. 재료비": (CATEGORY_MC, "재료비", "재료비계"),
    "Ⅱ. 노무비": (CATEGORY_MC, "노무비", "노무비계"),
    "Ⅲ. 경비":   (CATEGORY_MC, "경비",  "제조경비계"),
}

# PL 시트: 월 데이터 시작 열 (F열 = 1월)
PL_MONTH_COL_START = 6
# MC 시트: 월 데이터 시작 열 (E열 = 1월)
MC_MONTH_COL_START = 5


PLAN_MONTH_COL_START = 23  # PL2026(RS/KR): W열 = 1월 (2026 사업계획)


class KsceParser(BaseParser):
    def extract(self) -> List[AccountRow]:
        wb = openpyxl.load_workbook(self.filepath, read_only=True, data_only=True)
        rows = []
        year = self._detect_year(wb)

        # 사업계획 파일: PL2026(RS) 또는 PL2026(KR) 시트
        plan_sheet = None
        for sname in wb.sheetnames:
            if sname.startswith("PL2026"):
                plan_sheet = sname
                break

        if plan_sheet:
            # 시트명 PL2026(RS) / PL2026(KR) 에서 연도 추출
            m_year = re.search(r"PL(\d{4})", plan_sheet)
            plan_year = m_year.group(1) if m_year else year
            rows.extend(self._extract_sheet(
                wb[plan_sheet], plan_year, PL_LABEL_MAP, PLAN_MONTH_COL_START, label_col=1
            ))
        else:
            # 실적 파일: PL / MC 시트
            if "PL" in wb.sheetnames:
                rows.extend(self._extract_sheet(
                    wb["PL"], year, PL_LABEL_MAP, PL_MONTH_COL_START, label_col=2
                ))
            if "MC" in wb.sheetnames:
                rows.extend(self._extract_sheet(
                    wb["MC"], year, MC_LABEL_MAP, MC_MONTH_COL_START, label_col=2
                ))

        wb.close()
        return rows

    def _extract_sheet(self, ws, year: str, label_map: dict, month_col_start: int, label_col: int = 2) -> List[AccountRow]:
        # label_col 열에서 레이블 위치 탐색
        label_rows = {}
        for r in range(1, ws.max_row + 1):
            raw = ws.cell(row=r, column=label_col).value
            if raw is None:
                continue
            key = str(raw).strip()
            if key in label_map:
                label_rows[key] = r

        rows = []
        for month_offset in range(12):
            col = month_col_start + month_offset
            month_num = month_offset + 1

            # 해당 월에 데이터가 있는지 확인
            has_data = any(
                safe_float(ws.cell(row=row_num, column=col).value) != 0
                for row_num in label_rows.values()
            )
            if not has_data:
                continue

            ym = f"{year}-{month_num:02d}"
            for label, (cat, sub, account) in label_map.items():
                if label not in label_rows:
                    continue
                val = safe_float(ws.cell(row=label_rows[label], column=col).value)
                rows.append(self.make_row(ym, "KSCE", cat, sub, account, "RSD", val))

        return rows

    def _detect_year(self, wb) -> str:
        check_sheets = ["PL", "PL(Report)"]
        # 사업계획 시트도 연도 탐색에 포함
        for sname in wb.sheetnames:
            if sname.startswith("PL2026"):
                check_sheets.insert(0, sname)
                break

        for sname in check_sheets:
            if sname not in wb.sheetnames:
                continue
            ws = wb[sname]
            for r in range(1, 8):
                for c in range(1, 10):
                    val = ws.cell(row=r, column=c).value
                    if not val or not isinstance(val, str):
                        continue
                    m = re.search(r"FY\s*(\d{4})", val)
                    if m:
                        return m.group(1)
                    m = re.search(r"(\d{4})", val)
                    if m and m.group(1) in ("2024", "2025", "2026", "2027"):
                        return m.group(1)
        # 파일명에서 연도 추출 (fallback)
        m = re.search(r"(\d{4})", self.filepath)
        if m:
            return m.group(1)
        return "2026"
