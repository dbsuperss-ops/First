===================================================
  출석부 자동 생성기 v1.1 - 설치 및 사용 안내
===================================================

[구성 파일]
  engine.py                   ← Python 엔진 (핵심)
  AttendanceGenerator/        ← C# WPF 프로젝트 소스

[빠른 사용 방법 - Python만으로]
  1. Python 3.10 이상 설치  https://python.org  (PATH 등록 필수)
  2. openpyxl 설치:
       pip install openpyxl
  3. 명령 실행:
       python engine.py 출석부_TEMP.xlsx 2026-03-03 2026-07-06 ./출력폴더
  4. 출력폴더에 xlsx 파일 자동 생성됨

[WPF GUI 빌드 방법]
  1. Visual Studio 2022 설치
  2. AttendanceGenerator.csproj 열기
  3. 빌드 → bin\Release\net8.0-windows\ 폴더에 exe 생성
  4. engine.py를 exe와 같은 폴더에 복사 (필수!)
  5. Python + openpyxl 설치 필요

[생성 결과 예시]
  맞춤형 (1파일/과목):
    맞춤형_책놀이_화_출석부.xlsx
    맞춤형_미술_월_출석부.xlsx  ...

  방과후 (월별 분리):
    방과후_한자_월_3월_출석부.xlsx
    방과후_한자_월_4월_출석부.xlsx  ...

[v1.1 변경사항]
  - 맞춤형: 날짜 5개까지 지원 (기존 4개 → 수정)
  - 방과후: 학기 전체를 월별로 자동 분리 생성
  - WPF: 파일 수 표시 및 완료 메시지 개선
