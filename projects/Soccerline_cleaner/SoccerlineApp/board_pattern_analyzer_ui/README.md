# Board Pattern Analyzer UI

Soccerline 크롤링 Excel export의 `Posts`/`Comments` 시트를 읽어 반복 상호작용 패턴, 시간대 겹침, 빠른 댓글 비율, 공동 등장 관계를 중립적으로 집계하는 Windows용 GUI 프로그램입니다.

## 설치

```powershell
python -m pip install -r requirements.txt
```

## 실행

```powershell
python app.py
```

## 사용 방법

1. `입력 엑셀 파일`에서 `.xlsx` export 파일을 선택합니다.
2. `결과 엑셀 저장 경로`를 지정합니다. 기본값은 `output/board_analysis_report.xlsx`입니다.
3. 필요하면 `주제 키워드 JSON`을 선택합니다. 선택하지 않으면 `sample_topics.json`을 사용합니다.
4. 추가 키워드가 있으면 `Additional keywords`에 쉼표로 입력합니다.
5. `Keyword match mode`를 선택합니다. `OR`는 키워드 중 하나라도 포함된 글, `AND`는 모든 키워드가 포함된 글만 키워드 글로 계산합니다.
6. 특정 작성자만 보고 싶으면 작성자명, ID, IP 일부를 입력합니다. 빈칸이면 전체를 분석합니다.
7. `최소 댓글 수`, `빠른 댓글 기준 분`, `상위 키워드 개수`를 조정합니다.
8. `분석 실행`을 누릅니다.

키워드 JSON 파일이 없거나 비어 있거나 형식이 잘못된 경우에는 앱이 중단되지 않고 기본 키워드로 계속 분석합니다.

## 결과

결과 Excel에는 다음 시트가 포함됩니다.

- `Summary`: 분석 조건 요약
- `SourceFileCounts`: 입력 파일별 원본 건수와 중복 제거 후 건수
- `KeywordShareByUser`: 사용자 전체 글 중 키워드 포함 글의 비중
- `AuthorActivityTime`: 게시글 작성자의 일자/시간대별 활동
- `AuthorHourlyActivity`: 게시글 작성자의 시간대별 활동 요약
- `CommenterActivityTime`: 댓글 작성자의 일자/시간대별 활동
- `TimeCorrelation`: 게시글 작성 시간대와 댓글 작성 시간대의 상관 관계
- `Interactions`: 게시글 작성자와 댓글 작성자 간 반복 상호작용 패턴
- `FastCommentPatterns`: 빠른 댓글 비율과 시간대 겹침 패턴
- `TargetDailyPosts`: 지정 사용자의 일자별 글 작성 수
- `TimeDeltas`: 게시글 작성 시각과 댓글 작성 시각의 시간차

같은 폴더에 `interaction_heatmap.png`, `interaction_network.png`도 생성됩니다.

## exe 패키징

```powershell
python -m pip install pyinstaller
.\build_exe.ps1
```

생성 파일:

```text
dist/BoardPatternAnalyzer.exe
```

엑셀/시각화 라이브러리가 포함되므로 exe 용량이 커질 수 있습니다. exe 실행 시 `app_config.json`, `input/`, `output/`은 exe 파일 옆에 생성됩니다.

## 표현 기준

분석 결과는 동일인, 공모, 조직적 활동처럼 단정적인 표현을 사용하지 않습니다. 리포트와 UI는 반복 상호작용 패턴, 시간대 겹침, 빠른 댓글 비율, 공동 등장 같은 관찰 가능한 표현만 사용합니다.
