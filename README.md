# antigravity

개인 프로젝트 모음 저장소입니다. 파일 관리, 오피스 자동화, 재무 데이터 처리 등 실무 도구들을 포함합니다.
프로젝트의 오류를 수정하거나 기능을 추가할때 프로그램의 이름을 V1.1, V1.2, V2.0 등으로 변경하여 관리해주세요.
새로운 버전의 프로그램을 저장할때는 프로젝트 버전마다 새로운 폴더에 저장해주세요

## 프로젝트 목록

### 파일 관리

| 프로젝트 | 기술 | 설명 |
|---------|------|------|
| [DupeFinderPro](./DupeFinderPro) | C# / .NET 10 / Avalonia | 중복 파일 탐색 및 정리 도구. 시나리오 기반 자동 분류, 스캔 히스토리, 배치 처리 지원 |
| [FileFlow](./FileFlow) | C# / .NET 8 / WPF | 사용자 정의 규칙 기반 파일 자동 분류 시스템. 시나리오 관리, 해시 기반 중복 감지 |
| [FileLister](./FileLister) | C# / .NET 8 / WPF | 파일 목록 생성 및 인벤토리 도구. 다중 폴더 스캔, 필터링, 내보내기 기능 |

### 오피스 자동화

| 프로젝트 | 기술 | 설명 |
|---------|------|------|
| [PptMergerWpf](./PptMergerWpf) | C# / .NET 8 / WPF | PowerPoint 파일 일괄 병합 도구. 폰트, 색상, 간격 등 서식 커스터마이징 지원 |
| [PptxMerger](./PptxMerger) | C# / .NET 8 / WinForms | PowerPoint 병합 도구 (WinForms 버전). 체크박스 기반 파일 선택 및 실행 로그 |

### 재무 데이터 처리

| 프로젝트 | 기술 | 설명 |
|---------|------|------|
| [KSC_Refiner_v1.1_full](./KSC_Refiner_v1.1_full) | Python / C# WPF (하이브리드) | 해외 법인 Excel 정산 보고서 자동 취합 시스템. 다중 법인(KCTR, KSCCZ, KSCE 등) 파싱, 환율 관리 |

### 업무 모니터링

| 프로젝트 | 기술 | 설명 |
|---------|------|------|
| [WorkMonitorWpf](./WorkMonitorWpf) | C# / .NET 8 / WPF | 활성/유휴 창 상태 추적 업무 모니터. 실시간 활동 로그 및 시간 통계 |

### 진행 중 / 실험적

| 프로젝트 | 기술 | 설명 |
|---------|------|------|
| [projects/SettlementAutomation_Hybrid](./projects/SettlementAutomation_Hybrid) | Python + C# .NET 10 WPF | KSC_Refiner의 차세대 하이브리드 버전. PyInstaller 패키징 및 WPF 인터페이스 |
| [projects/SettlementAutomation](./projects/SettlementAutomation) | Python / Tkinter | 정산 자동화 초기 버전 |

## 기술 스택

- **UI 프레임워크**: WPF, Avalonia, Windows Forms
- **언어**: C# (.NET 8~10), Python 3.10+
- **주요 라이브러리**: DocumentFormat.OpenXml, ClosedXML, PyInstaller
- **도메인**: 파일 시스템 유틸리티, 오피스 문서 처리, 재무 데이터 ETL
