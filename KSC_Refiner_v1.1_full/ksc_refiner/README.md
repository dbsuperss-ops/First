# KSC Settlement Refiner v1.1

## 개요
해외 법인(KCTR, KSCCZ, KSCE, KSCI, KSCP) 결산 엑셀 파일을 자동 파싱하여
단일 Flat Data 통합 DB를 생성하는 시스템

## 프로젝트 구조
```
KSC_Refiner/
├── ksc_refiner/               # Python 정제 엔진
│   ├── engine.py              # 메인 엔진 (재귀 탐색 + 연도 누적)
│   ├── schema.py              # 마스터 스키마, 유틸리티
│   ├── config/
│   │   ├── rates.json         # 연간 고정환율
│   │   └── settings.json      # 루트 경로 등 설정 (자동 생성)
│   ├── parsers/
│   │   ├── __init__.py        # BaseParser 인터페이스
│   │   ├── kscp_parser.py     # KSCP (KRW, 월종합)
│   │   ├── kctr_parser.py     # KCTR (TRY, PL)
│   │   ├── ksccz_parser.py    # KSCCZ (RMB, ★손익계산서+★제조원가명세서)
│   │   ├── ksce_parser.py     # KSCE (RSD, PL+MC)
│   │   └── ksci_parser.py     # KSCI (USD, 손익계산서 월별실적)
│   └── output/
│
└── KSC_Refiner_UI/            # C# WPF UI (하이브리드)
    ├── KSC_Refiner_UI.csproj
    ├── App.xaml / App.xaml.cs
    ├── Views/
    │   └── MainWindow.xaml(.cs)
    ├── ViewModels/
    │   └── MainViewModel.cs
    └── Services/
        ├── SettingsService.cs
        └── PythonRunner.cs
```

## 실행 방법

### Python 엔진 단독 실행
```bash
pip install openpyxl
python ksc_refiner/engine.py "D:\OneDrive\06_결산_마감" 2026
```

### C# UI 실행
1. Visual Studio 2022에서 KSC_Refiner_UI.csproj 열기
2. NuGet 패키지 복원 (자동)
3. ksc_refiner 폴더를 빌드 출력 폴더에 복사
4. F5로 실행

### 폴더 구조 자동 인식
```
D:\OneDrive - Kyungshin Corporation\06_결산_마감\
└── 2026\
    ├── 01\
    │   ├── KCTR\  → KCTR_결산_1_1.xlsx
    │   ├── KSCE\  → KSCE_결산_1_1.xlsx
    │   └── ...
    └── 02\
        └── ...
```
루트 경로만 한 번 설정하면, 하위 폴더를 재귀 탐색하여
해당 연도의 모든 법인 × 모든 월을 누적 처리

## 환율 변경
연도가 바뀌면 `config/rates.json`에 새 연도 추가 또는 UI에서 직접 수정

## 필요 환경
- Python 3.10+ (openpyxl)
- .NET 8.0 (WPF UI용)
- Visual Studio 2022 (UI 빌드용)
