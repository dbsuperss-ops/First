# KSC Refiner v1.3

경신그룹 해외법인 결산 데이터 통합 시스템

## 주요 개선사항 (v1.3)

### ✨ 설계 철학
- **설정 기반 접근**: 하드코딩 제거, YAML 설정으로 모든 파서 규칙 관리
- **모듈화**: 핵심 로직과 파서 완전 분리
- **확장성**: 새로운 법인 추가 시 설정 파일만 수정
- **유지보수성**: 명확한 구조와 로깅

### 🏗 아키텍처

```
KSC_Refiner_v1.3/
├── core/                    # 핵심 엔진
│   ├── schema.py           # 데이터 모델
│   ├── config_loader.py    # 설정 로더
│   ├── output_writer.py    # 엑셀 출력
│   └── engine.py           # 메인 엔진
├── parsers/                 # 파서 모듈
│   ├── base_parser.py      # 파서 기본 클래스
│   ├── config_driven_parser.py      # 범용 설정 기반 파서
│   ├── kctr_specialized_parser.py   # KCTR 전용 파서
│   └── parser_factory.py   # 파서 팩토리
├── config/                  # 설정 파일
│   ├── parser_config.yaml  # 파서 규칙 (법인별 시트명, 행/열 매핑)
│   └── rates.json          # 환율 정보
├── utils/                   # 유틸리티
│   └── excel_helper.py     # 엑셀 처리 헬퍼
└── output/                  # 출력 디렉토리
```

### 🔧 v1.1/v1.2에서 수정된 문제

1. **하드코딩 제거**
   - ❌ Before: 각 파서에 행/열 번호 하드코딩
   - ✅ After: `parser_config.yaml`에서 중앙 관리

2. **코드 중복 제거**
   - ❌ Before: 5개 파서에 유사한 코드 반복
   - ✅ After: `ConfigDrivenParser` 1개로 통합, 특수 케이스만 별도 클래스

3. **연도/시트 감지 개선**
   - ❌ Before: 각 파서마다 다른 감지 로직
   - ✅ After: `schema.py`에 통합 함수

4. **에러 처리 강화**
   - ❌ Before: 에러 발생 시 프로그램 중단
   - ✅ After: 로깅 후 계속 진행

5. **설정 파일 관리**
   - ❌ Before: settings.json 사용 미흡
   - ✅ After: YAML 기반 명확한 설정 구조

## 설치 및 실행

### Windows 사용자 (권장)

**1. 설치 프로그램 사용**
```
1. installer_output\KscRefiner_Setup_v1.3.0.exe 실행
2. 설치 마법사 따라 설치
3. 바탕화면 또는 시작메뉴에서 "KSC Settlement Refiner" 실행
```

**2. 실행 방법**
- 프로그램 실행 → 폴더 선택 → 연도 입력 → 자동 처리
- 완료 후 output 폴더에 통합 DB 생성

### 개발자 / Python 직접 실행

**1. 의존성 설치**
```bash
pip install -r requirements.txt
```

**2. 실행**
```bash
python ksc_refiner.py <입력디렉토리> [연도]
```

예시:
```bash
python ksc_refiner.py C:\Users\user\Documents\files 2026
```

**3. 출력**
- 통합 DB: `output/consolidated_db_YYYYMMDD_HHMMSS.xlsx`
- 3개 시트:
  - **통합_DB**: 전체 데이터
  - **법인별_요약**: 주요 계정과목 요약
  - **경영보고서**: 경영진용 종합 리포트

## Windows 빌드 (개발자용)

### 요구사항
- Python 3.8+ with PyInstaller
- .NET 8.0 SDK
- Inno Setup 6 (선택사항 - 설치 파일 생성용)

### 빌드 실행
```cmd
build.bat
```

빌드 결과:
- `publish\ksc_engine.exe` - Python 엔진
- `publish\KscRefiner_v1.3.exe` - C# 런처
- `installer_output\KscRefiner_Setup_v1.3.0.exe` - 설치 프로그램

## 설정 파일

### parser_config.yaml
```yaml
parsers:
  KSCP:
    company_code: "KSCP"
    currency: "KRW"
    actual_sheets: ["결산 종합", "월종합", "보고용자료"]
    plan_sheets: ["사업계획", "KSCP 계획"]
    actual_mapping:
      4: ["PL", "매출", "매출액"]
      # ...
```

- `actual_sheets`: 실적 데이터 시트명 (우선순위 순)
- `plan_sheets`: 계획 데이터 시트명
- `actual_mapping`: 실적 데이터 행 매핑 (행번호: [대분류, 중분류, 계정과목])
- `plan_mapping`: 계획 데이터 행 매핑

### rates.json
```json
{
  "2026": {
    "EUR": 1600.0,
    "USD": 1350.0,
    ...
  }
}
```

## 지원 법인

| 법인코드 | 법인명 | 통화 | 파서 타입 |
|---------|--------|------|----------|
| KSCP | 경신케이블폴란드 | KRW | 범용 |
| KCTR | 케이블테크터키 | TRY | 전용 |
| KSCCZ | 경신케이블체코 | RSD | 범용 |
| KSCE | 경신케이블유럽 | EUR | 범용 |
| KSCI | 경신산업(중국) | RMB | 범용 |

## 새로운 법인 추가 방법

1. `config/parser_config.yaml`에 법인 설정 추가
2. 특수 처리가 필요한 경우만 `parsers/` 에 전용 파서 작성
3. `parsers/parser_factory.py`의 `SPECIALIZED_PARSERS`에 등록

## 문제 해결

### 데이터가 추출되지 않는 경우
1. 시트명 확인: `parser_config.yaml`의 `actual_sheets`, `plan_sheets`
2. 행 번호 확인: `actual_mapping`, `plan_mapping`
3. 로그 확인: WARNING/ERROR 메시지

### 새로운 시트 형식
1. 엑셀 파일에서 시트명과 데이터 위치 확인
2. `parser_config.yaml`에 추가

## 버전 히스토리

- **v1.3**: 설정 기반 아키텍처 재설계, 코드 정리
- **v1.2**: 프로젝트명 변경, 버전 관리 개선
- **v1.1**: 초기 릴리스

## 라이센스

Copyright © 2024-2026 경신그룹
