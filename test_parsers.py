import sys
import os

sys.stdout.reconfigure(encoding='utf-8')
sys.path.insert(0, r'c:\Users\dbsup\.antigravity\files\KSC_Refiner_v1.1_full\ksc_refiner')

from parsers.kctr_parser import KctrParser
from parsers.ksccz_parser import KscczParser
from parsers.ksce_parser import KsceParser
from parsers.ksci_parser import KsciParser
from parsers.kscp_parser import KscpParser
from schema import load_rates

base = r'C:\Users\dbsup\OneDrive - Kyungshin Corporation\02_경영_보고_전략\사업계획\2026'
config_dir = r'c:\Users\dbsup\.antigravity\files\KSC_Refiner_v1.1_full\ksc_refiner\config'
rates = load_rates("2026", config_dir)

tests = [
    ("KCTR",  KctrParser,  "KCTR_2026년 사업계획.xlsx"),
    ("KSCCZ", KscczParser, "KSCCZ_2026년 사업계획.xlsx"),
    ("KSCE",  KsceParser,  "KSCE_2026년 사업계획.xlsx"),
    ("KSCI",  KsciParser,  "KSCI_2026년 사업계획.xlsx"),
    ("KSCP",  KscpParser,  "KSCP_2026년 사업계획.xlsx"),
]

for corp, cls, fname in tests:
    fpath = os.path.join(base, fname)
    print(f"\n{'='*50}")
    print(f"[{corp}] {fname}")
    if not os.path.exists(fpath):
        print(f"  파일 없음: {fpath}")
        continue
    try:
        parser = cls(fpath, rates)
        rows = parser.extract()
        print(f"  ✅ {len(rows)}건 추출")
        if rows:
            seen = set()
            for r in rows[:5]:
                key = (r.귀속연월, r.계정과목)
                if key not in seen:
                    seen.add(key)
                    print(f"     {r.귀속연월} | {r.대분류} | {r.중분류} | {r.계정과목} | {r.현지통화} {r.현지금액:,.0f}")
    except Exception as e:
        print(f"  ❌ 오류: {e}")
        import traceback
        traceback.print_exc()
