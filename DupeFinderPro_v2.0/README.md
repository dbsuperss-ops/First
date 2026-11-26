# DupeFinderPro v1.4

> Windows용 중복 파일 탐지 및 정리 도구 — Avalonia UI 기반 데스크탑 앱

---

## 버전 히스토리

| 버전 | 날짜 | 내용 |
|------|------|------|
| v1.4 | 2026-11-26 | 삭제 직전에 남는 사본과 지울 파일의 내용을 검사 때 해시와 비교(남는 사본은 처리 끝까지 쓰기·삭제 잠금), 확인 창 전에 실행 계획을 확정해 동의한 작업만 실행, "처리 중단" 버튼, 격리에도 남는 사본 규칙 적용, 이동은 같은 볼륨이면 파일 ID 확인·다른 볼륨이면 복사→내용 확인→원본 삭제, 원본과 같은 폴더·격리 폴더로의 이동 거부, "유지"가 파일을 잠그지 않음, 모든 처리를 Application 계층의 한 경로로 통합, 검사 프로그램을 저장소(`tests/`)에 추가 |
| v1.3 | 2026-11-26 | 파일 고유 ID로 같은 실제 파일(정션·하드 링크 등)을 한 번만 세고 삭제 직전에도 ID로 남는 사본 확인, 격리 폴더·휴지통은 항상 검사에서 제외(격리 폴더에 표식 파일), 빈 파일 기본 제외, 처음 선택값을 모두 "유지"로, 파일별 "삭제"에도 확인 창, 같은 파일의 작업 중복 실행 차단과 일괄 처리 중 입력 잠금, 실패·건너뜀·중단을 "완료"로 표시하지 않음 |
| v1.2 | 2026-11-25 | 해시를 계산하지 못한 파일이 중복으로 묶이던 오류 수정, 검사하지 못한 파일 표시, 중복 삭제 시 추천 파일 보존 및 일괄 삭제 확인 창 추가, 남는 사본이 없는 삭제 차단, "중복 삭제" 취소 시 선택값 유지, 겹치는 검색 폴더에서 같은 파일이 두 번 수집되던 오류 수정 |
| v1.1 | 2026-04-07 | 현재 상태 기준 최초 문서화 (ResultsViewModel 정렬/필터, ScanHistory 내보내기, 이미지 미리보기) |

> **버전 규칙:** 마이너 수정(버그 수정, 소규모 기능) → +0.1 / 메이저 수정(대규모 기능, 아키텍처 변경) → +1.0

---

## 개요

DupeFinderPro는 파일 해시 기반의 중복 탐지와 규칙 기반 파일 분류(Organize) 두 가지 핵심 기능을 제공합니다.

- **중복 탐지**: 크기 → 부분 해시 → 전체 해시 3단계 필터링으로 빠르고 정확하게 중복 파일을 찾습니다.
- **파일 분류**: 사용자 정의 시나리오(규칙)에 따라 파일을 자동으로 이동·정리합니다.

---

## 기술 스택

| 항목 | 내용 |
|------|------|
| 프레임워크 | .NET 10, Avalonia 11.3.12 |
| UI 패턴 | MVVM (CommunityToolkit.Mvvm 8.2.1) |
| DI | Microsoft.Extensions.DependencyInjection 10.0 |
| 빌드 타깃 | Windows x64, 단일 파일 자체 포함(Self-contained) |

---

## 프로젝트 구조

```
src/DupeFinderPro/
├── Domain/
│   ├── Models/          # FileEntry, DuplicateGroup, ScanFilter, ScanJob 등 순수 도메인 모델
│   └── Interfaces/      # IDuplicateDetector, IFileScanner, IHashingService 등 추상화
├── Application/
│   ├── ScanOrchestrator.cs       # 스캔 파이프라인 조율
│   ├── ScanJobService.cs         # 스캔 작업 생성 및 실행 관리
│   ├── CleanupOrchestrator.cs    # 삭제/이동/격리 계획 실행, 마지막 사본 보존 규칙
│   ├── OrganizeOrchestrator.cs   # 파일 분류 실행
│   └── HomeStatsService.cs       # 홈 화면 통계
├── Infrastructure/
│   ├── Detection/        # DuplicateDetector (3단계 해시), PriorityAutoSelectStrategy
│   ├── FileSystem/       # FileScanner, FileOperationService, FileVerifier(잠금·내용 확인), FileIdentityReader
│   ├── Hashing/          # HashingService (부분/전체 SHA-256)
│   ├── Organize/         # ClassifyService, WatcherService, WindowsSchedulerService
│   └── Storage/          # InMemoryScanJobRepository, JSON 기반 Repository들
└── ViewModels / Views/
    ├── HomeViewModel / HomeView
    ├── Duplicate/        # DuplicateScan, Results, ScanHistory
    └── Organize/         # ScenarioList, ScenarioEdit, OrganizeRun, OrganizeLog, OrganizeStats

tests/DupeFinderPro.Checks/   # "마지막 사본 보존" 검사 프로그램 (실패 수를 종료 코드로 반환)
```

---

## 주요 기능

### 중복 탐지

| 기능 | 설명 |
|------|------|
| 3단계 해시 필터링 | 파일 크기 → 부분 해시(앞부분만) → 전체 해시 순으로 비교 대상을 점진적으로 좁혀 성능 최적화 |
| 병렬 해싱 | `Parallel.ForEachAsync` (MaxDegreeOfParallelism=4) |
| 클라우드 파일 스킵 | `FileAttributes.Offline` / `RECALL_ON_DATA_ACCESS` 속성 파일 자동 제외 |
| 같은 실제 파일 식별 | 볼륨 일련번호+파일 ID가 같은 경로(정션·하드 링크 등)는 한 번만 세고 결과 화면에 개수 표시. ID를 읽지 못한 파일은 비교하지 않음 |
| 검사하지 못한 파일 표시 | 읽을 수 없거나 클라우드에만 있거나 파일 ID를 확인하지 못한 비교 대상은 중복에서 빼고 결과 화면에 목록으로 표시 |
| 항상 제외하는 폴더 | 휴지통(`$RECYCLE.BIN`), `System Volume Information`, 기본 격리 폴더, 격리 표식(`.dupefinderpro-quarantine`)이 있는 폴더 |
| 삭제 확인 | "중복 삭제"·"전체 적용"·파일별 "삭제" 모두 삭제하기 전에 확인 창 표시 |
| 마지막 사본 보존 | 삭제·격리는 그룹에 확인된 남는 사본이 있을 때만 실행 (파일별·일괄 모두 `CleanupOrchestrator` 한 경로). 남는 사본은 쓰기·삭제를 막은 채 열어 파일 ID가 지울 파일과 다르고 내용이 검사 때 해시와 같은지 확인하며, 처리가 끝날 때까지 잠가 둔다. 지울 파일도 잠근 채 내용이 같은지 확인한 뒤 연 채로 휴지통에 보낸다 |
| 실행 계획 | "중복 삭제"·"전체 적용"은 확인 창을 띄우기 전에 그룹별 계획을 확정하고, 동의하면 그 계획만 실행. 실행 중에는 입력이 잠기고 "처리 중단"으로 남은 파일을 멈출 수 있음 |
| 처리 결과 표시 | 성공만 "완료". 실패·건너뜀(파일 없음)·보류·취소는 이유와 함께 표시하고 다시 시도할 수 있음. "유지"는 선택만 바꾸며 파일을 잠그지 않음 |
| 자동 보존 추천 | 처음에는 모든 파일이 "유지". "자동 선택"·"중복 삭제"를 누르면 `PriorityAutoSelectStrategy`의 추천 파일만 남기도록 설정 |
| 정렬/필터 | 낭비 공간·파일 수·파일명·크기 정렬, 확장자·최소 크기 필터 |
| 이미지 미리보기 | 파일 클릭 시 우측 패널에 썸네일 표시 (jpg, png, bmp, gif, webp 등) |

#### 스캔 필터 옵션

- 포함/제외 경로 다중 지정
- 파일 타입 카테고리 (문서, 이미지, 동영상, 오디오, 압축파일, 설치파일, 기타)
- 확장자 화이트리스트/블랙리스트
- 파일명 키워드 포함/제외
- 최소/최대 파일 크기
- 생성일/수정일 범위
- 시스템 파일 제외, 재귀 탐색 옵션
- 빈 파일(0바이트) 포함 옵션 (기본 제외)

#### 중복 처리 액션

| 액션 | 동작 |
|------|------|
| 유지(Keep) | 해당 파일 변경 없이 보존 |
| 삭제(Delete) | 휴지통으로 이동. 확인 창을 거치며, 확인된 남는 사본이 있을 때만 |
| 격리(Quarantine) | 지정된 격리 폴더로 이동하고 폴더에 격리 표식을 남김. 삭제와 같이 격리 밖에 확인된 남는 사본이 있을 때만 |
| 이동(Move) | 사용자 지정 폴더로 이동. 원래 위치에 사본을 남기지 않아도 되며, 옮긴 파일은 그 위치에서 다시 확인될 때 같은 그룹의 남는 사본이 됨. 다른 볼륨이면 복사 → 내용 확인 → 원본 삭제. 원본과 같은 폴더·격리 폴더로는 옮기지 않음 |

### 파일 분류 (Organize)

| 기능 | 설명 |
|------|------|
| 시나리오 관리 | 규칙 기반 분류 시나리오 생성/편집/삭제 |
| 분류 실행 | 시나리오 실행 및 실시간 진행 표시 |
| 실행 로그 | 분류된 파일 이력 조회 |
| 통계 | 분류 결과 통계 화면 |
| 파일 감시 | `WatcherService`로 폴더 변경 감지 자동 분류 |
| 스케줄러 | `WindowsSchedulerService`로 예약 실행 |

---

## 빌드 및 실행

```bash
# 개발 실행
cd src/DupeFinderPro
dotnet run

# 릴리즈 빌드 (단일 .exe)
dotnet publish -c Release

# 인스톨러 생성 (Inno Setup 필요)
installer/build-installer.bat

# 검사 실행 (Windows, 종료 코드 0이면 모두 통과)
dotnet run --project tests/DupeFinderPro.Checks
```

검사는 임시 폴더에서 정션·하드 링크를 만들고, 다른 볼륨이 있으면 그 볼륨의 사용자 임시 폴더로 이동도 시험한 뒤 지운다. 휴지통 삭제는 실제 휴지통 대신 임시 파일을 바로 지우는 대역으로 시험한다.

빌드 결과물: `src/DupeFinderPro/publish/DupeFinderPro.exe`

---

## 아키텍처 원칙

- **레이어 분리**: Domain → Application → Infrastructure → ViewModels 방향으로만 의존
- **불변 모델**: `FileEntry`, `ScanFilter` 등 도메인 모델은 `record`로 선언, 수정 시 새 인스턴스 생성
- **인터페이스 추상화**: 모든 인프라 구현체는 Domain 인터페이스에 의존하여 교체 가능
- **DI 컨테이너**: `App.axaml.cs`에서 전체 서비스 등록 및 ViewModel 주입

---

## 알려진 이슈 / TODO

- [ ] 같은 실제 파일 식별은 정션·하드 링크로 확인함. `subst` 드라이브·매핑 드라이브와 UNC 경로, 네트워크·FAT 계열 드라이브의 파일 ID 동작은 확인하지 않음
- [ ] 휴지통이 없는 위치(네트워크·일부 이동식 드라이브)에서 휴지통 삭제가 영구 삭제가 되는지 확인하지 않음. 확인 창에 경고만 표시하며, 삭제 뒤 휴지통에서 찾지 못하면 결과에 "영구 삭제되었을 수 있음"을 표시 (남는 사본은 확인된 뒤라 마지막 사본은 아님)
- [ ] 삭제 직전 내용 확인은 파일 크기와 관계없이 전체를 읽음. 큰 파일은 시간이 걸리며, 큰 파일을 격리·보류로 돌리는 선택지는 아직 없음
- [ ] 격리 폴더를 비울 때 내용을 확인하는 기능 없음. 이전 버전에서 만든 기본 위치 밖의 격리 폴더에는 표식이 없어 검사에 포함됨
- [ ] 제외 경로가 폴더 경계 없이 문자열 앞부분으로 비교됨 (`C:\Data`를 제외하면 `C:\Database`도 제외)
- [ ] `FormatBytes` 헬퍼 중복 (4곳) → 공통 유틸리티로 추출
- [ ] `DuplicateDetector` 해시 실패 원인 로깅 추가 (실패한 파일 목록은 결과 화면에 표시됨)
