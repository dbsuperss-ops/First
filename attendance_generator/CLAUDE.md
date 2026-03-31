# 출석부 자동 생성기 — Claude Code 작업 지시서

## 프로젝트 목적
`명단.xlsx` 파일에서 수강생 데이터를 읽어,
맞춤형/방과후 출석부 xlsx를 과목×요일 조합별로 자동 생성한다.

---

## 즉시 실행할 작업

### Step 1. 의존성 설치
```bash
pip install openpyxl pyinstaller
```

### Step 2. engine.py 실행 테스트
```bash
python engine.py 출석부_TEMP.xlsx 2026-03-03 2026-07-06 ./output_test
```
- 정상 출력: 각 줄이 JSON `{"combo": "...", "files": [...], "status": "ok"}`
- `output_test/` 폴더에 xlsx 파일 생성 확인

### Step 3. Windows exe 빌드
```bash
pyinstaller --onefile --name 출석부생성기엔진 --distpath ./dist engine.py
```
- 결과물: `dist/출석부생성기엔진.exe`
- 이 exe는 Python 설치 없이 단독 실행 가능

### Step 4. WPF 프로젝트 빌드
```bash
cd AttendanceGenerator
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained false
```
- 결과물: `bin/Release/net8.0-windows/win-x64/publish/출석부생성기.exe`
- **`dist/출석부생성기엔진.exe`를 위 publish 폴더에 복사할 것**

---

## 파일 구조

```
attendance_generator/
├── engine.py                     ← Python 엔진 (핵심)
├── CLAUDE.md                     ← 이 파일
├── 출석부_TEMP.xlsx              ← 명단 원본 (사용자가 제공)
└── AttendanceGenerator/
    ├── AttendanceGenerator.csproj
    ├── App.xaml
    ├── App.xaml.cs
    ├── MainWindow.xaml
    ├── MainWindow.xaml.cs
    └── engine.py                 ← engine.py 복사본 (빌드 시 함께 배포)
```

---

## engine.py 동작 방식

### 입력
```
python engine.py <명단.xlsx> <시작일YYYY-MM-DD> <종료일YYYY-MM-DD> <출력폴더>
```

### 명단 시트 구조 (7행 헤더, 8행부터 데이터)
| 열인덱스(0기준) | 컬럼명 |
|---|---|
| 1 | 구분 (맞춤형/방과후/아침돌봄/오후돌봄) |
| 2 | 과목 |
| 3 | 요일 (월/화/수/목/금) |
| 5 | 강사 |
| 7 | 이름 |
| 8 | 학년 |
| 9 | 반 |
| 10 | 번호 |
| 11 | 전화번호 |
| 14 | 참여요일 |
| 15 | 비고 |

- `아침돌봄`, `오후돌봄` 구분은 스킵

### 출력 (stdout, 한 줄씩 JSON)
```json
{"combo": "맞춤형 / 책놀이 / 화", "files": ["맞춤형_책놀이_화_출석부.xlsx"], "status": "ok"}
{"combo": "방과후 / 한자 / 월", "files": ["방과후_한자_월_3월_출석부.xlsx", "방과후_한자_월_4월_출석부.xlsx"], "status": "ok"}
```

### 출석부 양식 규칙

**맞춤형 출석부** (`명단.xlsx` 내 `맞춤형 출석부` 시트를 템플릿으로 복사)
- 파일명: `맞춤형_{과목}_{요일}_출석부.xlsx`
- 기간 내 해당 요일 날짜를 최대 5개 입력
- 날짜 컬럼(행8): G=col7, H=col8, J=col10, L=col12, N=col14
- 학생 데이터 시작: 행10
  - col2=순번, col3=이름, col5=반, col6=참여요일, col16=비고, col19=전화번호

**방과후 출석부** (`명단.xlsx` 내 `방과후 출석부` 시트를 템플릿으로 복사)
- 파일명: `방과후_{과목}_{요일}_{월}_출석부.xlsx` (월별 분리)
- 월별로 파일 분리 (학기 전체 18회 → 양식 최대 6칸이므로)
- 날짜 컬럼(행7): H=col8, I=col9, K=col11, M=col13, O=col15, Q=col17
- 학생 데이터 시작: 행9
  - col2=순번, col3=이름, col5=학년, col6=반, col7=번호, col17=비고, col20=전화번호

---

## WPF UI 동작 방식

1. 사용자가 명단 xlsx 파일 선택
2. 기간(시작일~종료일) 설정
3. 저장 폴더 선택 (기본값: 명단 파일 옆 `출석부_생성결과/`)
4. "전체 출석부 일괄 생성" 버튼 클릭
5. WPF가 `출석부생성기엔진.exe`를 `Process.Start()`로 호출
6. stdout JSON을 실시간으로 읽어 ListBox에 표시
7. 완료 후 "저장 폴더 열기" 버튼 활성화

### WPF → 엔진 호출 인수 형식
```
출석부생성기엔진.exe "<명단경로>" <시작일> <종료일> "<출력폴더>"
```

---

## 자주 발생하는 오류 대응

| 오류 | 원인 | 해결 |
|---|---|---|
| `engine.py not found` | exe 옆에 engine.py 없음 | engine.py를 exe와 같은 폴더에 복사 |
| `Python not found` | PATH 미등록 | python 설치 후 PATH 등록 |
| `KeyError: '명단'` | 명단 파일 시트명 다름 | 시트명 `명단` 확인 |
| 날짜가 숫자로 표시 | number_format 미적용 | `cell.number_format = 'M/D'` 확인 |
| 병합셀 오류 | 템플릿 복사 실패 | openpyxl 버전 확인 (`pip install --upgrade openpyxl`) |

---

## 향후 개선 예정
- [ ] 총 인원 자동 표시 (출석부 하단)
- [ ] 학년별 정렬 옵션
- [ ] 맞춤형 5회 초과 시 월별 분리 (현재 5회 초과분 누락)
