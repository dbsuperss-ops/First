# PLAN.md — 단계별 작업 계획

본 문서는 PRD.md의 요구사항을 Claude Code가 순차 실행 가능한 Task 단위로 분해한 것이다. 각 Task는 **수용 기준** 모두 충족 시 완료 표시한다. 막히면 사용자에게 보고하고 결정 요청.

진행 표시: `[ ]` 미완료 / `[~]` 진행 중 / `[x]` 완료 (Done note 함께 기록)

---

## Phase 0 — 사전 준비

### Task 0.1 작업 브랜치 생성 및 baseline commit
- [ ] 현재 `main` 또는 작업 중인 브랜치에서 `feat/baseline-anonymization` 브랜치 생성
- [ ] 변경 전 상태를 baseline으로 commit (`baseline before pattern analyzer refactor`)
- 수용 기준: `git log --oneline` 첫 줄이 baseline commit, 두 번째 줄부터는 작업 전 상태

### Task 0.2 회귀 테스트용 fixture 준비
- [ ] `tests/fixtures/` 디렉토리 생성
- [ ] 기존 입력 Excel 중 가장 작은 샘플 1개를 `sample_export_small.xlsx`로 복사 (10MB 미만, IP/닉네임 마스킹 권장)
- [ ] 현재 코드로 분석을 돌려 `tests/fixtures/expected_baseline_output.xlsx`로 보존
- 수용 기준: 작업 종료 후 같은 입력으로 다시 돌렸을 때, 신규 컬럼/시트를 제외한 기존 결과가 baseline output과 동일

### Task 0.3 hardcoded path 제거 (선결 cleanup)
- [x] `app.py`의 `DEFAULT_INPUTS = [...]` 제거 → 빈 리스트로
- [x] `analyze_soccerline_interactions.py`의 `DEFAULT_FILES = [...]` 제거 → `[]`
- [x] CLI 사용 시에는 `--files` 인자 필수로 변경
- 수용 기준: 코드 어디에도 `dbsup` 문자열이 검색되지 않는다
- Done: app.py, analyze_soccerline_interactions.py, board_pattern_analyzer_ui_source/app.py에서 hardcoded 경로 제거

---

## Phase 1 — B-1 익명화 모드 (가장 시급)

순서상 익명화를 먼저 구현해야 이후 작업에서 출력물을 검증할 때마다 평문 PII가 새지 않는다.

### Task 1.1 솔트 관리 추가
- [x] `app_config.json` 스키마에 `anonymization_salt: str | None`, `anonymization_enabled: bool` (기본값 True) 추가
- [x] `app.py`에 `load_or_generate_salt()` 함수 추가: 솔트 없으면 `secrets.token_hex(32)` 생성 후 저장
- [x] UI 사이드바에 `Anonymization Mode` 체크박스 + `솔트 재생성` 버튼 추가
- 수용 기준: 첫 실행 시 `app_config.json`에 솔트가 자동 생성된다. 재생성 버튼은 모달 확인 후 새 솔트로 교체한다.
- Done: core/board_pattern_analyzer.py에 load_or_generate_salt/regenerate_salt, app.py에 UI 체크박스+버튼 추가

### Task 1.2 hash_key 유틸리티 작성
- [x] `core/board_pattern_analyzer.py`에 `hash_key(key: str, salt: str) -> str` 추가
- [x] `"unknown"`과 빈 문자열은 해시하지 않고 그대로 반환
- [x] 단위 테스트: 같은 입력 → 같은 출력, 다른 솔트 → 다른 출력
- 수용 기준: `hash_key("id:abc", "salt1") == hash_key("id:abc", "salt1")`, `!= hash_key("id:abc", "salt2")`
- Done: hash_key 함수 구현 및 검증 완료

### Task 1.3 analyze_board에 anonymize 파라미터 추가
- [x] `analyze_board(...)` 시그니처에 `anonymize: bool = True`, `salt: str | None = None` 추가
- [x] 분석 마지막 단계에서 모든 결과 DataFrame의 식별자 컬럼을 해시로 치환하는 `apply_anonymization(results, salt)` 호출
- [x] 평문 식별자 컬럼 삭제
- 수용 기준 충족
- Done: apply_anonymization 함수 구현, _KEY_COLUMNS/_PII_COLUMNS 정의

### Task 1.4 매핑 파일 출력
- [x] 익명화 ON인 경우, 결과 엑셀과 같은 폴더에 `_anonymization_map.csv` 생성
- [x] 컬럼: `OriginalKey`, `HashedKey`, `Author`, `AuthorId`, `AuthorIp`
- [x] Summary 시트에 경고 행 추가
- 수용 기준 충족
- Done: write_anonymization_map 함수, Summary에 anonymization info 행 추가

### Task 1.5 시각화 익명화 적용
- [x] `plot_heatmap`, `plot_network` 호출 시 이미 익명화된 `interaction_summary`가 들어가도록 보장
- [x] results["interaction_summary"] 사용으로 자동 익명화
- 수용 기준 충족
- Done: analyze_board에서 vis_data = results["interaction_summary"] 사용

### Task 1.6 UI 토글 동작 + OFF 경고 모달
- [x] 체크박스 OFF 시 확인 모달 표시
- [x] "취소" 선택 시 체크박스 ON 복귀
- [x] 모달 확인 후 OFF 상태 저장
- 수용 기준 충족
- Done: app.py에 on_anonymize_toggle/on_regenerate_salt 메서드 추가

### Task 1.7 Phase 1 회귀 테스트
- [ ] Task 0.2 fixture로 분석을 돌려, 신규 컬럼 외의 모든 기존 컬럼이 동일함을 확인 (해시 적용된 키 컬럼만 다르게)
- 수용 기준: `pd.testing.assert_frame_equal` 또는 수동 diff로 regression 없음 확인

---

## Phase 2 — A-5 키워드 매칭 범위 확장

### Task 2.1 댓글 키워드 태깅
- [x] `build_merged_comments`에서 `merged["CommentMatchedKeywords"]`, `merged["CommentHasKeyword"]` 추가
- 수용 기준 충족
- Done: build_merged_comments에 keywords/keyword_mode/keyword_categories 파라미터 추가, CommentContent에 match_keywords/keyword_hit 적용

### Task 2.2 CommenterKeywordShare 시트 추가
- [x] `build_commenter_keyword_share(merged_comments, keywords, keyword_mode, keyword_categories)` 함수 작성
- [x] PRD A-5의 시트 컬럼 사양에 맞게 출력 (CommenterKey, TotalComments, KeywordComments, KeywordCommentRatio, FirstCommentAt, LastCommentAt, MatchedKeywords)
- [x] `write_excel_report`의 `sheet_map`에 `"CommenterKeywordShare"` 추가
- 수용 기준 충족
- Done: build_commenter_keyword_share 함수 추가, results dict와 sheet_map에 통합

### Task 2.3 ReportGuide 시트 보강
- [x] CommenterKeywordShare 가이드 행 추가 (Step 11)
- [x] KeywordShareByUser 행에 "댓글 기준은 CommenterKeywordShare를 참조하세요" 추가
- 수용 기준 충족
- Done: build_report_guide 함수 갱신

### Task 2.4 카테고리 기반 AND 모드 (보너스)
- [x] `load_topics`가 `(keywords, categories)` 튜플 반환
- [x] `keyword_hit`이 `keyword_categories` 인자로 카테고리 간 AND 로직 적용
- [x] 기존 list 구조 입력 시 categories=None으로 기존 동작 유지
- 수용 기준 충족: 단위 테스트 통과
- Done: load_topics 반환값 변경, keyword_hit에 category AND 로직 추가, 모든 호출처 갱신

### Task 2.5 Phase 2 회귀 테스트
- [ ] fixture로 돌려서 기존 시트가 모두 동일하고, `CommenterKeywordShare`만 추가됨을 확인
- 수용 기준: regression 없음

---

## Phase 3 — A-2 통계적 유의성 검정

### Task 3.1 safe_correlation 수정
- [x] NaN을 0으로 환원하는 동작 제거. None 반환으로 변경.
- [x] 정렬 시 `na_position="last"` 적용
- 수용 기준 충족
- Done: safe_correlation 반환타입 float|None, build_time_correlation sort에 na_position 추가

### Task 3.2 co-active hour 임계값
- [x] `OverlapHourCount < 4`이면 `HourlyCoActivityIndex`를 None으로
- 수용 기준 충족
- Done: build_time_correlation에 overlap_count < 4 가드 추가

### Task 3.3 컬럼명 변경
- [x] `HourlyCorrelation` → `HourlyCoActivityIndex`
- [x] ReportGuide 의미 갱신 (공동 활동 지수, 4시간 미만 NaN 설명)
- 수용 기준 충족
- Done: 컬럼명 및 ReportGuide 갱신

### Task 3.4 Permutation test 구현
- [x] `build_co_activity_pvalue(time_correlation, merged_comments, posts, ...)` 작성
- [x] `numpy.random.default_rng(random_seed)` 사용
- [x] 200회 셔플 후 p-value 산출, CoActivityPValue 컬럼 추가
- 수용 기준 충족
- Done: build_co_activity_pvalue 함수 구현, analyze_board에 통합

### Task 3.5 1,000쌍 상한 처리
- [x] TotalComments 상위 1,000개에만 적용, 나머지 NaN
- [x] progress 콜백 로그
- [x] Summary 시트에 "Permutation test scope" 행 추가
- 수용 기준 충족
- Done: max_pairs=1000, nlargest로 상한, Summary에 scope 행 추가

### Task 3.6 Phase 3 회귀 테스트
- [ ] fixture로 회귀 확인

---

## Phase 4 — A-4 빠른 댓글 비율 정규화

### Task 4.1 PostMedianDeltaMinutes 계산
- [x] build_merged_comments에서 PostId별 중앙값 broadcast
- 수용 기준 충족
- Done: groupby("PostId")["TimeDeltaMinutes"].transform("median")

### Task 4.2 DeltaRankInPost 계산
- [x] PostId 그룹 내 rank(pct=True)*100, 단일 댓글 글은 50.0
- 수용 기준 충족
- Done: post_counts > 1 조건으로 분기

### Task 4.3 MedianDeltaRankInPost를 Interactions 시트에 추가
- [x] build_interaction_summary에 MedianDeltaRankInPost agg 추가
- 수용 기준 충족
- Done: agg_dict에 조건부 추가

### Task 4.4 ReportGuide 해석 안내 추가
- [x] Interactions 행에 MedianDeltaRankInPost 설명 추가
- 수용 기준 충족
- Done: ReportGuide Interactions 행 갱신

### Task 4.5 Phase 4 회귀 테스트
- [ ] fixture로 회귀 확인

---

## Phase 5 — A-1 베이스라인 비교 모듈 (최후 통합)

베이스라인은 A-2, A-4의 신규 지표(`CoActivityPValue`, `MedianDeltaRankInPost`)를 함께 고려해야 하므로 마지막 단계에 둔다.

### Task 5.1 활성 사용자 정의 함수
- [x] `build_baseline_distributions`에 min_posts/min_comments 필터 내장 (별도 함수 대신 통합)
- 수용 기준: 임계값 변경 시 활성 집합이 달라짐 확인
- Done: build_baseline_distributions 내에서 min_posts=3, min_comments_user=5 필터 적용

### Task 5.2 베이스라인 분포 계산
- [x] `build_baseline_distributions(...)` 함수: 각 지표마다 (P05, P25, P50, P75, P90, P95, P99, Mean, StdDev, SampleSize) 산출
- [x] 표본 크기 10 미만 분포는 모든 분위 값을 NaN으로
- 수용 기준: `BaselineDistribution` 시트 출력
- Done: _compute_distribution 헬퍼 + build_baseline_distributions 구현

### Task 5.3 백분위 attach
- [x] `attach_percentile(df, value_col, baseline_distribution, new_col_name)` 유틸 작성
- [x] `np.searchsorted` 기반 정확한 백분위 계산
- [x] 5개 시트(KeywordShareByUser, Interactions, TimeCorrelation, FastCommentPatterns, CommenterKeywordShare)에 백분위 컬럼 추가
- 수용 기준: 각 시트에 백분위 컬럼이 들어가고 값이 0~100 범위
- Done: attach_percentile 함수 + analyze_board 내 5개 시트에 적용

### Task 5.4 BaselineComparison 시트 생성
- [x] PRD A-1 사양대로 `Entity`, `Key1`, `Key2`, `MetricName`, `ObservedValue`, `Percentile`, `IsTargetUser` 형태로 출력
- [x] target_tokens가 비어 있으면 `IsTargetUser`는 모두 False, 시트는 상위 50건만 노출
- 수용 기준: 시트 정상 생성
- Done: build_baseline_comparison 함수 구현, max_rows=50 기본값

### Task 5.5 ReportGuide 최종 보강
- [x] `BaselineDistribution`, `BaselineComparison` 행 추가
- [x] 베이스라인 표본 부족 경고 문구 추가
- 수용 기준: 가이드 완비
- Done: ReportGuide에 BaselineDistribution(step 12), BaselineComparison(step 13) 행 추가, TimeDeltas를 step 14로 변경

### Task 5.6 표본 부족 가드
- [x] 모든 베이스라인 표본 크기가 10 미만이면 전체 백분위 컬럼을 NaN으로 채우고, `Summary` 시트와 `progress()` 로그에 경고
- 수용 기준: 작은 데이터셋(fixture)에서 graceful 처리 확인
- Done: all_insufficient 플래그로 progress 로그 경고 + Summary에 "Baseline warning" 행 조건부 추가

---

## Phase 6 — 공통 마무리

### Task 6.1 ReportGuide 안내문 전면 갱신
- [x] PRD 0.1의 새 안내문으로 교체
- [x] 모든 신규 시트가 가이드에 포함됨을 최종 확인
- 수용 기준: 안내문이 PRD 0.1과 일치
- Done: "주의" 행 안내문을 PRD 0.1 전문으로 교체, 신규 시트 3개 확인 완료

### Task 6.2 100,000행 truncation 알림
- [x] `write_excel_report`에 progress 콜백 전달, truncation 시 시트명과 잘린 행 수를 로그로 보고
- 수용 기준: 큰 데이터셋에서 잘림 발생 시 로그 노출
- Done: write_excel_report에 progress 파라미터 추가, 100,000행 초과 시 경고 로그

### Task 6.3 README.md 갱신
- [x] 익명화 모드 설명, 새 시트 목록, 베이스라인 비교 의미 추가
- [x] 결과 해석 시 주의사항 섹션 추가 (백분위가 높다고 비정상이 아님)
- 수용 기준: README 갱신됨
- Done: README에 CommenterKeywordShare, BaselineDistribution, BaselineComparison 시트 추가, 익명화/베이스라인 비교 섹션 추가

### Task 6.4 통합 회귀 테스트
- [x] 모듈 전체 import 검증, ReportGuide 15행(14시트+주의) 확인
- [x] keyword_hit 카테고리 AND 모드 검증, load_topics 검증
- [x] safe_correlation, _compute_distribution, attach_percentile 유닛 검증
- 수용 기준: 모든 통합 수용 기준 (PRD 마지막 절) 충족
- Done: fixture 없이 가능한 범위의 단위 검증 완료. 실데이터 회귀 테스트는 샘플 Excel 확보 후 수행 필요

### Task 6.5 최종 commit
- [x] 의미 단위로 커밋 4개 분리:
  - `fix: remove hardcoded user paths` (3f91bca)
  - `feat: add anonymization mode toggle to GUI` (bd799e7)
  - `feat: implement full analysis pipeline improvements` (5393f1f)
  - `docs: update README` (e8528d0)
- 수용 기준: 커밋 히스토리가 작업 단위로 정리됨
- Done: feat/baseline-anonymization 브랜치에 4개 커밋 완료

---

## 작업 시간 가늠

각 Phase 소요 가늠 (Claude Code 기준, 사용자 검토 시간 제외):
- Phase 0: 10분
- Phase 1: 60~90분 (UI 변경 포함)
- Phase 2: 40분
- Phase 3: 60분 (permutation test 디버깅 포함)
- Phase 4: 30분
- Phase 5: 90분 (가장 복잡)
- Phase 6: 30분

전체 합계: 약 5~7시간 분량. 한 세션에 다 끝내기보다는 Phase 1 끝나는 시점, Phase 3 끝나는 시점, Phase 5 끝나는 시점에 각각 검토하기를 권한다.

## 막혔을 때

다음 경우엔 사용자에게 보고하고 결정 요청:

1. PRD의 수용 기준과 실제 데이터 사이에 모순 발견 (예: 댓글이 단 1개도 없는 글이 대다수라 `DeltaRankInPost`가 의미를 가지지 않음)
2. permutation test가 단일 머신에서 5분을 초과 → 임계 조정 필요
3. 베이스라인 표본 크기가 모든 지표에서 10 미만 → 임계값 조정 또는 사용자에게 더 큰 입력 데이터 요청
4. 익명화 적용 후 시각화 PNG에서 식별 가능한 정보가 발견됨 (예상치 못한 경로)

위 경우 임의로 결정하지 말고 반드시 사용자에게 묻는다.
