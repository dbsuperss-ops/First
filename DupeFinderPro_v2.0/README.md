# DupeFinderPro v1.1

> Windows용 중복 파일 탐지 및 정리 도구 — Avalonia UI 기반 데스크탑 앱

---

## 버전 히스토리

| 버전 | 날짜 | 내용 |
|------|------|------|
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
│   ├── CleanupOrchestrator.cs    # 삭제/이동/격리 실행
│   ├── OrganizeOrchestrator.cs   # 파일 분류 실행
│   └── HomeStatsService.cs       # 홈 화면 통계
├── Infrastructure/
│   ├── Detection/        # DuplicateDetector (3단계 해시), PriorityAutoSelectStrategy
│   ├── FileSystem/       # FileScanner, FileOperationService
│   ├── Hashing/          # HashingService (부분/전체 SHA-256)
│   ├── Organize/         # ClassifyService, WatcherService, WindowsSchedulerService
│   └── Storage/          # InMemoryScanJobRepository, JSON 기반 Repository들
└── ViewModels / Views/
    ├── HomeViewModel / HomeView
    ├── Duplicate/        # DuplicateScan, Results, ScanHistory
    └── Organize/         # ScenarioList, ScenarioEdit, OrganizeRun, OrganizeLog, OrganizeStats
```

---

## 주요 기능

### 중복 탐지

| 기능 | 설명 |
|------|------|
| 3단계 해시 필터링 | 파일 크기 → 부분 해시(앞부분만) → 전체 해시 순으로 비교 대상을 점진적으로 좁혀 성능 최적화 |
| 병렬 해싱 | `Parallel.ForEachAsync` (MaxDegreeOfParallelism=4) |
| 클라우드 파일 스킵 | `FileAttributes.Offline` / `RECALL_ON_DATA_ACCESS` 속성 파일 자동 제외 |
| 자동 보존 추천 | `PriorityAutoSelectStrategy`로 각 그룹에서 유지할 파일 자동 선택 |
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

#### 중복 처리 액션

| 액션 | 동작 |
|------|------|
| 유지(Keep) | 해당 파일 변경 없이 보존 |
| 삭제(Delete) | 휴지통으로 이동 |
| 격리(Quarantine) | 지정된 격리 폴더로 이동 |
| 이동(Move) | 사용자 지정 폴더로 이동 |

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
```

빌드 결과물: `src/DupeFinderPro/publish/DupeFinderPro.exe`

---

## 아키텍처 원칙

- **레이어 분리**: Domain → Application → Infrastructure → ViewModels 방향으로만 의존
- **불변 모델**: `FileEntry`, `ScanFilter` 등 도메인 모델은 `record`로 선언, 수정 시 새 인스턴스 생성
- **인터페이스 추상화**: 모든 인프라 구현체는 Domain 인터페이스에 의존하여 교체 가능
- **DI 컨테이너**: `App.axaml.cs`에서 전체 서비스 등록 및 ViewModel 주입

---

## 알려진 이슈 / TODO

- [ ] `FormatBytes` 헬퍼 중복 (4곳) → 공통 유틸리티로 추출
- [ ] `DuplicateDetector` 해시 실패 시 예외를 묵살하지 않고 로깅 추가
- [ ] `DuplicateGroupViewModel.AutoSelect`가 `SuggestedKeep`을 무시하는 불일치 수정
- [ ] `ResultsViewModel.ApplyAllAsync` 내부 CancellationToken 실제 연결
- [ ] `FileEntryViewModel.SetActionKeep` 즉시 `IsDone=true` 설정으로 인한 액션 변경 불가 문제
