# CLAUDE.md — Board Pattern Analyzer 개선 작업

이 문서는 Claude Code가 이 저장소에서 작업할 때 반드시 따라야 하는 컨텍스트, 설계 원칙, 금지 사항을 정의한다. 모든 작업은 이 문서의 원칙 안에서 수행한다.

---

## 1. 프로젝트 정체성

Soccerline 게시판 크롤링 Excel을 입력으로 받아, **반복 상호작용 패턴 · 시간대 겹침 · 빠른 댓글 비율 · 공동 등장 관계**를 중립적으로 집계하는 Windows GUI 분석 도구다. 사용자는 비개발자 출신의 분석가이며, 산출물은 본인 검증용으로 사용한다.

도구가 다루는 데이터의 성격상, **분석 결과가 의도와 무관하게 특정 개인을 공격하는 자료로 전용될 위험**이 상시 존재한다. 이번 개선의 핵심 목표는 분석의 통계적 엄밀성을 높이고 동시에 결과가 외부 유통될 경우의 피해를 최소화하는 것이다.

## 2. 절대 원칙 (DO / DON'T)

### DO

1. 모든 출력 컬럼명·시트명·로그 메시지는 **관찰값 표현**만 사용한다. "InteractionCount", "FastCommentRatio", "HourlyCoActivityIndex" 등.
2. 신규 지표를 추가할 때는 반드시 **베이스라인 분포 대비 백분위**를 함께 출력한다.
3. 통계적 비교를 출력할 때는 **계산 불가(NaN)와 의미 있는 0**을 구별한다. NaN을 0으로 환원하지 않는다.
4. 식별자(닉네임/ID/IP)가 출력 엑셀에 포함될 가능성이 있는 모든 경로에서, **익명화 모드의 기본값은 ON**으로 한다.
5. 매 작업 단계마다 `progress()` 콜백으로 진행 상황을 한국어로 보고한다.
6. 새 기능을 추가하면 `ReportGuide` 시트에 해당 시트의 "보여주지 않는 것"을 명시한다.

### DON'T

1. 출력물(시트명·컬럼명·로그·UI 라벨·문서)에 "동일인", "공모", "조직적", "외부세력", "댓글부대", "의심 사용자" 같은 단정 표현을 절대 사용하지 않는다. 변수명·내부 함수명에도 사용하지 않는다.
2. 익명화 모드를 우회하는 "디버그용 평문 출력" 같은 옵션을 추가하지 않는다. 매핑 파일은 반드시 결과 엑셀과 **별도 파일**로 분리한다.
3. 100,000행 truncation 같은 silent 처리를 추가하지 않는다. 잘림이 발생하면 반드시 사용자에게 알린다.
4. 본인 PC 경로를 코드에 하드코딩하지 않는다 (`DEFAULT_INPUTS = [...C:\Users\dbsup\...]` 같은 패턴 금지). 마지막 사용 경로는 `app_config.json`에만 기억한다.
5. 통계적 유의성 검정을 거치지 않은 지표를 "패턴 강도" 같은 평가적 표현으로 라벨링하지 않는다.

## 3. 작업 흐름 규칙

1. **모든 코드 변경 전, PLAN.md의 해당 Task 번호를 인용**하고 그 Task의 수용 기준(Acceptance Criteria)을 명시한 뒤 작업에 들어간다.
2. 한 Task를 끝낼 때마다 PLAN.md의 해당 항목에 ✅ 표시를 하고, 그 아래에 "Done note"로 변경된 파일과 핵심 결정사항을 짧게 기록한다.
3. **수용 기준을 만족하지 못한 채 다음 Task로 넘어가지 않는다.** 막히면 사용자에게 명시적으로 보고하고 결정을 요청한다.
4. 기존 함수의 시그니처를 깨야 하는 경우, 호출 측을 모두 함께 수정한다. 모듈은 `core/board_pattern_analyzer.py`와 `analyze_soccerline_interactions.py` 양쪽에 걸쳐 있다.
5. 코딩 스타일은 기존 코드를 따른다 (type hint, `from __future__ import annotations`, dataclass 대신 dict, pandas 우선).

## 4. 모듈 구조 (현재 상태)

```
board_pattern_analyzer_ui/
├── app.py                          # CustomTkinter GUI
├── core/
│   ├── __init__.py
│   └── board_pattern_analyzer.py   # 분석 오케스트레이션 (analyze_board)
├── sample_topics.json
├── requirements.txt
└── (상위 디렉토리)/
    └── analyze_soccerline_interactions.py  # 기반 모듈 (read_exports, plot_*, make_user_key 등)
```

`board_pattern_analyzer.py`는 `analyze_soccerline_interactions`를 `sys.path` 조작으로 import하고 있다. 이번 작업 중에 이 의존 구조를 **그대로 유지**한다. 리팩토링은 별도 작업으로 분리한다.

## 5. 데이터 핵심 사실 (analyze_soccerline_interactions.py 분석 결과)

### 식별자 키 우선순위 (`make_user_key`)
1. `id:{user_id}` — UserID 있을 때
2. `nameip:{nickname}|{ip}` — 닉네임 + IP 둘 다 있을 때
3. `name:{nickname}` — 닉네임만
4. `ip:{ip}` — IP만
5. `unknown` — 아무것도 없을 때

### 댓글 닉네임 형태
원본 컬럼 `Nickname`은 `"닉네임(userid, 123.***.***.1)"` 형태이며 `parse_comment_nickname`에서 분리된다.

### IP 형태
이미 게시판 단계에서 부분 마스킹된 형태 (`123.***.***.1`). 그래도 닉네임·ID와 조합되면 식별 가능하므로 익명화 대상이다.

### 키워드 매칭 범위 (현재)
`PostText = Title + "\n" + Body` 만. **댓글 본문(`CommentContent`)은 키워드 매칭에서 빠져 있다** — 이번 작업의 A-5에서 확장한다.

## 6. 출력 표현 규칙

ReportGuide 시트와 UI 양쪽에 다음 문구를 반드시 유지한다:

> 이 리포트는 반복 상호작용, 시간대 겹침, 빠른 댓글 비율 같은 관찰값만 제공합니다. 동일인, 공모, 조직적 활동을 단정하지 않습니다. 모든 지표는 통계적 패턴이며, 특정 사용자의 의도·소속·정체성을 시사하지 않습니다.

새 시트(BaselineComparison, CommenterKeywordShare 등)에는 해당 시트가 **무엇을 보여주지 않는지**를 첫 줄 또는 헤더 노트에 명시한다.

## 7. 응답 스타일

- 한국어 하오체 사용 (사용자가 선호)
- 작업 전 진행 여부 확인. 단, PLAN.md에 명시된 Task를 순차 진행할 때는 매번 확인하지 않고 Task 단위로 묶어 보고한다.
- 코드 변경 후에는 변경된 파일 경로와 핵심 변경사항을 3~5줄로 요약한다.
- 후속 선택지가 필요한 시점에는 Q1/Q2/Q3 형식으로 제시한다.
