# 출석부 관리 프로그램 (Attendance Management System)

## 1. 프로젝트 개요

학교의 방과후/맞춤형 프로그램 출석부를 C# WPF 기반의 데스크톱 애플리케이션으로 관리하는 시스템이다.
MVVM(Model-View-ViewModel) 아키텍처를 적용하여 UI와 비즈니스 로직을 완전히 분리하고,
향후 데이터베이스 연동 및 기능 확장이 용이하도록 설계한다.

---

## 2. 아키텍처 (MVVM 구조)

| 레이어 | 역할 |
|--------|------|
| **Model** | 출석부 데이터 구조 정의 (학생 정보, 출석 기록, 프로그램 메타데이터) |
| **ViewModel** | UI와 데이터 간의 바인딩, 출석 일수 계산 등의 비즈니스 로직 및 커맨드 처리 |
| **View** | XAML 기반의 사용자 인터페이스 (결재란, DataGrid 표 등 시각적 양식) |
| **Service** | 데이터 저장 및 불러오기 (인터페이스 기반 설계로 향후 저장 방식 변경에 대비) |

---

## 3. 디렉토리 및 파일 구조

```text
AttendanceGenerator/
├── Models/
│   ├── ProgramMeta.cs          # 연도, 프로그램명, 기간, 요일, 결재자 정보
│   ├── Student.cs              # 순번, 이름, 학년, 반, 참여요일, 연락처, 비고
│   ├── AttendanceEntry.cs      # 특정 날짜의 출결 상태 (○, △, ×)
│   └── AttendanceData.cs       # 저장/불러오기용 최상위 데이터 모델
├── ViewModels/
│   ├── ViewModelBase.cs        # INotifyPropertyChanged 구현체
│   ├── RelayCommand.cs         # ICommand 구현체
│   ├── AttendanceCellViewModel.cs  # 날짜 + 출결 상태 셀 단위 ViewModel
│   ├── StudentRowViewModel.cs  # 학생 한 행 ViewModel (출결 셀 목록 포함)
│   └── MainViewModel.cs        # 전체 데이터 바인딩, 커맨드, 비즈니스 로직
├── Services/
│   ├── IDataService.cs         # 데이터 입출력 인터페이스
│   └── FileDataService.cs      # 로컬 JSON 파일 저장소 구현체
├── App.xaml / App.xaml.cs
├── MainWindow.xaml             # 전체 UI 레이아웃 및 DataGrid 정의
└── MainWindow.xaml.cs          # 동적 날짜 컬럼 생성 코드
```

---

## 4. 주요 기능

### 프로그램 정보 입력
- 연도, 구분(맞춤형/방과후), 프로그램명, 강사명
- 기간(시작일 ~ 종료일), 수업 요일
- 결재란(담당 / 팀장 / 교장)

### 날짜 자동 생성
- 기간과 요일을 설정 후 "날짜 생성" 클릭
- 해당 요일의 수업일을 자동 계산하여 DataGrid 컬럼으로 추가

### 출석 관리
- DataGrid에서 학생별 날짜별 출결 상태 입력
- 출결 상태: `○`(출석), `△`(지각), `×`(결석), 빈칸(미입력)
- 총 출석 / 지각 / 결석 자동 계산

### 저장 / 불러오기
- `.att` 확장자의 JSON 파일로 저장
- 저장한 파일을 다시 불러와 편집 계속 가능

---

## 5. 기술 스택

| 항목 | 내용 |
|------|------|
| 언어 | C# 12 |
| 프레임워크 | .NET 8 WPF |
| UI 라이브러리 | MaterialDesignThemes 5.x |
| 데이터 직렬화 | System.Text.Json (내장) |
| 아키텍처 | MVVM (직접 구현, 외부 MVVM 프레임워크 미사용) |

---

## 6. 빌드 및 실행

```bash
# 의존성 복원
dotnet restore

# 디버그 빌드
dotnet build

# 배포 빌드 (Windows x64)
dotnet publish -c Release -r win-x64 --self-contained false
```

실행 파일 위치: `bin/Release/net8.0-windows/win-x64/publish/출석부관리.exe`

---

## 7. 향후 개선 예정

- [ ] Excel 내보내기 (기존 engine.py 연동)
- [ ] 명단 xlsx에서 학생 일괄 가져오기
- [ ] 총 인원 / 출석률 통계 표시
- [ ] 월별 요약 뷰
- [ ] 데이터베이스 연동 (SQLite)
