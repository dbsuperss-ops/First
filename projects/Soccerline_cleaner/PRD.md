# PRD.md — Board Pattern Analyzer 개선 명세

본 문서는 5개 우선순위 개선(A-1, B-1, A-2, A-5, A-4)에 대한 기능 명세를 정의한다. 각 항목은 **목적 → 동작 → 출력 사양 → 수용 기준** 순서로 기술한다.

작업 순서는 PLAN.md를 따른다.

---

## 0. 공통 개선 — 출력 표현 강화

### 0.1 ReportGuide 시트 보강

기존 ReportGuide 시트의 마지막 "주의" 행을 다음으로 교체한다:

> 이 리포트는 반복 상호작용, 시간대 겹침, 빠른 댓글 비율 같은 관찰값만 제공합니다. 동일인, 공모, 조직적 활동을 단정하지 않습니다. 모든 지표는 통계적 패턴이며, 특정 사용자의 의도·소속·정체성을 시사하지 않습니다. 베이스라인 분포 대비 백분위가 높다는 것이 곧 "비정상"을 의미하지는 않으며, 활발한 정상 사용자도 상위 백분위에 들어갈 수 있습니다.

### 0.2 컬럼명 라벨 조정

`TimeCorrelation` 시트의 `HourlyCorrelation` 컬럼명을 `HourlyCoActivityIndex`로 변경한다. 상관계수라는 통계 용어가 인과적 해석을 부르기 때문이다. 값의 계산 방식은 유지한다 (다만 A-2의 통계 검정 결과를 함께 출력).

### 0.3 100,000행 truncation 알림

`excel_safe_dataframe`이 행을 자를 때 `progress()` 콜백으로 시트 이름과 잘린 행 수를 통지한다.

---

## A-1. 베이스라인 비교 모듈

### 목적
타깃 사용자/사용자 쌍의 지표가 게시판 전체 활성 사용자 분포에서 어느 위치에 있는지를 보여, 절대값의 잘못된 해석을 방지한다.

### 핵심 개념: 베이스라인 그룹 정의

**활성 사용자(Active Users)**: 분석 기간 내 다음 조건 중 하나를 만족하는 모든 PostAuthorKey 또는 CommenterKey
- 게시글 작성자 베이스라인: 최소 N개 글을 작성한 PostAuthorKey (기본값 N=3, UI 노출)
- 댓글 작성자 베이스라인: 최소 M개 댓글을 작성한 CommenterKey (기본값 M=5, UI 노출)
- 사용자 쌍 베이스라인: `min_comments` 임계값을 만족하는 모든 (PostAuthorKey, CommenterKey) 쌍

**타깃 그룹**: 키워드 매칭 글을 작성한 사용자, 또는 키워드 매칭 글에 댓글을 단 사용자. 사용자가 명시적으로 target 토큰을 입력한 경우 그 부분집합을 추가 강조한다.

### 동작

1. `build_baseline_distributions(posts, comments, merged_comments, min_comments)`를 신규 작성.
   - 활성 사용자(게시 N+, 댓글 M+) 전체에 대해 다음 분포를 계산:
     - `KeywordPostRatio` (사용자 단위)
     - `InteractionCount` (사용자 쌍 단위)
     - `FastCommentRatio` (사용자 쌍 단위)
     - `MedianDeltaMinutes` (사용자 쌍 단위)
     - `HourlyCoActivityIndex` (사용자 쌍 단위, A-2에서 산출)
   - 각 분포에서 백분위 5/25/50/75/90/95/99 값을 산출
2. 기존 시트 4종(`KeywordShareByUser`, `Interactions`, `TimeCorrelation`, `FastCommentPatterns`)의 각 행에, 해당 행의 지표가 베이스라인 분포에서 차지하는 **백분위**를 새 컬럼으로 추가.
3. 신규 시트 `BaselineDistribution` 추가: 베이스라인 분포 자체의 분위수 테이블.
4. 신규 시트 `BaselineComparison` 추가: 타깃 사용자(또는 사용자 쌍)별 주요 지표와 백분위를 한눈에 비교.

### 출력 사양

#### 신규 컬럼 (기존 시트에 추가)

| 시트 | 추가 컬럼 | 의미 |
|---|---|---|
| KeywordShareByUser | `KeywordRatioPercentile` | KeywordPostRatio가 활성 사용자 분포에서 차지하는 백분위 (0~100) |
| Interactions | `InteractionCountPercentile`, `FastRatioPercentile` | 두 지표 각각의 활성 사용자 쌍 분포 내 백분위 |
| TimeCorrelation | `CoActivityPercentile` | HourlyCoActivityIndex의 활성 쌍 분포 내 백분위 |
| FastCommentPatterns | `BurstRatioPercentile` | FastCommentRatio가 활성 게시글 분포에서 차지하는 백분위 |

#### 신규 시트: `BaselineDistribution`

| Column | Description |
|---|---|
| Metric | 지표 이름 (KeywordPostRatio / InteractionCount / FastCommentRatio / HourlyCoActivityIndex / MedianDeltaMinutes) |
| SampleSize | 베이스라인 표본 크기 |
| P05, P25, P50, P75, P90, P95, P99 | 백분위수 값 |
| Mean | 평균 |
| StdDev | 표준편차 |

#### 신규 시트: `BaselineComparison`

각 행이 타깃 사용자(쌍). 컬럼:
- `Entity` ("user" or "user_pair")
- `Key1`, `Key2` (Key2는 user_pair만)
- `MetricName`
- `ObservedValue`
- `Percentile`
- `IsTargetUser` (사용자가 입력한 target 토큰에 매치되면 True)

### 수용 기준 (Acceptance Criteria)

- [ ] `BaselineDistribution` 시트가 결과 엑셀에 생성된다.
- [ ] `BaselineComparison` 시트가 결과 엑셀에 생성된다.
- [ ] 4개 기존 시트에 백분위 컬럼이 추가된다.
- [ ] 베이스라인 표본 크기가 10 미만인 경우, 모든 백분위 컬럼이 NaN이 되고 ReportGuide에 "표본 부족" 경고가 추가된다.
- [ ] 사용자가 빈 입력 파일을 넘겨도 빈 시트가 정상적으로 생성된다 (예외 발생 없이).

---

## B-1. 익명화 모드

### 목적
출력 엑셀이 외부로 유출될 경우의 신상털이 피해를 차단한다. 분석가 본인은 별도 매핑 파일로 식별 가능하지만, 결과 엑셀 단독으로는 식별이 불가능하게 만든다.

### 동작

1. UI에 `Anonymization Mode` 체크박스 추가. **기본값 ON**.
2. 익명화 활성화 시:
   - 모든 `PostAuthorKey`, `CommenterKey`를 `sha256(salt + key)[:10]`의 해시(앞 10자리)로 치환.
   - 출력 컬럼 `Author`, `AuthorId`, `AuthorIp`, `AuthorNames`, `AuthorIds`, `AuthorIps`, `CommentNickname`, `CommentUserId`, `CommentIpParsed`, `CommenterNames` 등 평문 식별자 컬럼은 **모두 제거**한다 (NaN 처리 아님 — 컬럼 자체 삭제).
   - 해시는 같은 키 → 같은 해시이므로 분석은 그대로 가능하다.
3. 솔트 관리:
   - `app_config.json`에 `anonymization_salt` 항목 추가.
   - 비어 있으면 최초 실행 시 자동으로 32바이트 hex 랜덤 생성하여 저장.
   - 사용자가 UI에서 "솔트 재생성" 버튼을 누르면 새 솔트로 교체 (이전 결과와 해시가 달라짐).
4. 매핑 파일 출력:
   - 익명화 ON일 때 결과 엑셀과 같은 폴더에 `_anonymization_map.csv` 별도 파일 생성.
   - 컬럼: `OriginalKey`, `HashedKey`, `Author` (있으면), `AuthorId` (있으면), `AuthorIp` (있으면).
   - 파일명에 언더스코어 prefix를 붙여 일반 결과 파일과 구별.
   - 결과 엑셀의 `Summary` 시트에 매핑 파일 경로와 경고문구를 명시.
5. UI 경고:
   - `Anonymization Mode`를 OFF로 토글할 때 모달로 확인 받기:
     > 익명화를 끄면 결과 엑셀에 닉네임, ID, IP가 평문으로 포함됩니다. 이 파일이 외부로 유출될 경우 게시판 이용자의 신상이 노출될 수 있습니다. 그래도 진행하시겠습니까?
6. 모든 시각화(`interaction_heatmap.png`, `interaction_network.png`)도 익명화 모드에서는 해시된 키로 노드/축이 표시된다.

### 솔트 적용 함수 예시 (구현 가이드)

```python
import hashlib

def hash_key(key: str, salt: str) -> str:
    if not key or key == "unknown":
        return key
    digest = hashlib.sha256(f"{salt}|{key}".encode("utf-8")).hexdigest()
    return f"h:{digest[:10]}"
```

### 수용 기준

- [ ] 익명화 ON 상태로 분석을 돌렸을 때, 결과 엑셀의 어떤 시트에서도 닉네임/ID/IP 평문이 발견되지 않는다 (시각적 검사 + grep 검증).
- [ ] 같은 솔트로 두 번 분석하면 같은 해시가 나온다.
- [ ] 솔트 재생성 후에는 해시가 달라진다.
- [ ] 매핑 파일이 별도 CSV로 생성되며, 매핑 파일을 삭제해도 결과 엑셀 분석은 그대로 가능하다.
- [ ] 익명화 OFF 토글 시 모달 경고가 표시된다.
- [ ] 히트맵·네트워크 그래프에 평문 식별자가 노출되지 않는다.

---

## A-2. 통계적 유의성 검정

### 목적
`HourlyCoActivityIndex` (구 HourlyCorrelation) 값이 두 사용자가 공유하는 활성 시간대로 인한 단순 우연인지, 우연 대비 유의한지를 구별한다.

### 동작

1. 기존 `safe_correlation`을 수정: NaN을 0으로 환원하지 않는다. `pd.NA` 또는 `float('nan')` 그대로 반환.
2. Co-active hour 임계값 추가:
   - 두 사용자가 모두 활동한 시간대(`AuthorPostCount > 0 AND CommentCount > 0`) 갯수가 4 미만이면 `HourlyCoActivityIndex`를 NaN으로 처리.
3. Permutation test 추가:
   - 댓글 작성자의 `HourBucket` 라벨을 무작위 순열로 셔플한 뒤 상관계수를 재계산. 200회 반복.
   - p-value = `(셔플 corr >= 실제 corr).sum() / 200`
   - 새 컬럼 `CoActivityPValue` 추가.
   - 실제 corr이 NaN이면 p-value도 NaN.
4. 계산 비용 관리:
   - 대상 사용자 쌍이 1,000개를 초과하면, `InteractionCount` 상위 1,000개에 대해서만 permutation test를 수행.
   - 나머지는 p-value NaN.
   - `progress()`로 "Permutation test: 1234개 중 1000개 쌍 대상" 형태의 로그.

### 출력 사양

`TimeCorrelation` 시트에 다음 컬럼이 순서대로 들어간다:

| Column | Description |
|---|---|
| PostAuthorKey | (익명화 시 해시) |
| CommenterKey | (익명화 시 해시) |
| HourlyCoActivityIndex | 공동 활성 시간대 한정 상관계수. 표본 부족 시 NaN |
| CoActivityPValue | Permutation test p-value. 미수행 또는 표본 부족 시 NaN |
| CoActivityPercentile | A-1에서 추가되는 백분위 |
| OverlapHourCount | 공동 활성 시간대 수 |
| AuthorPostHourCount | 글 작성자의 활성 시간대 수 |
| CommentHourCount | 댓글 작성자의 활성 시간대 수 |
| TotalComments | 댓글 총 건수 |
| FastCommentCount | 빠른 댓글 건수 |
| FastCommentRatio | 빠른 댓글 비율 |

### 수용 기준

- [ ] `HourlyCoActivityIndex` NaN과 0이 결과 엑셀에서 시각적으로 구별된다 (NaN은 빈 셀로 출력).
- [ ] OverlapHourCount < 4인 행의 HourlyCoActivityIndex가 NaN이다.
- [ ] 1,000쌍 이하 데이터셋에서 모든 쌍에 대해 p-value가 계산된다.
- [ ] 1,000쌍 초과 시 상위 1,000쌍에만 p-value가 계산되고, 나머지는 NaN이며, 그 사실이 progress 로그와 Summary 시트에 명시된다.
- [ ] 동일 입력 + 동일 random seed로 두 번 돌리면 p-value가 재현된다.

---

## A-5. 키워드 매칭 범위 확장

### 목적
정치 비방 표현은 댓글 본문에도 빈번하게 등장한다. 현재 키워드 매칭은 `PostText`(Title + Body)만 보고 있어 댓글에서 키워드를 사용하는 사용자가 분석에서 누락된다.

### 동작

1. `comments["CommentContent"]`에도 키워드 매칭을 적용.
2. 신규 시트 `CommenterKeywordShare` 추가:

| Column | Description |
|---|---|
| CommenterKey | (익명화 시 해시) |
| TotalComments | 댓글 작성자의 총 댓글 수 |
| KeywordComments | 키워드 매칭 댓글 수 (OR/AND 모드 동일 로직) |
| KeywordCommentRatio | 키워드 댓글 비율 |
| KeywordRatioPercentile | A-1 베이스라인 대비 백분위 |
| FirstCommentAt, LastCommentAt | 활동 기간 |
| MatchedKeywords | 댓글에서 발견된 키워드 목록 (상위 10개) |

3. 기존 `KeywordShareByUser` 시트는 게시글 기준 그대로 유지. 헤더 노트에 "이 시트는 게시글 기준입니다. 댓글 기준은 CommenterKeywordShare 시트를 참조하세요"를 추가.

### AND 모드의 동작 (보너스)

키워드 JSON이 dict 구조 (`{"politics": [...], "pattern_terms": [...]}`)인 경우:
- **OR 모드**: 어느 카테고리든 한 키워드라도 매치되면 hit
- **AND 모드**: 모든 카테고리에서 최소 한 개씩 매치되면 hit (카테고리 간 AND, 카테고리 내 OR)

키워드 JSON이 list 구조인 경우:
- **OR 모드**: 한 키워드라도 매치되면 hit
- **AND 모드**: 모든 키워드가 매치되어야 hit (현재 동작)

이 동작 변경은 `keyword_hit` 함수에 카테고리 정보를 추가로 받게 하는 형태로 구현.

### 수용 기준

- [ ] `CommenterKeywordShare` 시트가 생성된다.
- [ ] 같은 댓글 작성자가 게시글에도 키워드를 쓰면 두 시트에 모두 등장하며, 키 일관성(같은 CommenterKey/PostAuthorKey 해시)이 유지된다.
- [ ] dict 구조의 키워드 JSON + AND 모드에서 카테고리 간 AND가 적용됨을 단위 테스트로 확인.
- [ ] 기존 `KeywordShareByUser` 시트의 동작은 변경되지 않는다 (regression 없음).

---

## A-4. 빠른 댓글 비율 정규화

### 목적
"30분 이내 댓글 비율"의 절대값은 게시글의 인기도에 크게 좌우된다. 인기 글은 누구든 빠르게 댓글이 달리므로, 특정 사용자가 "특별히 빠르다"는 판단을 하려면 그 게시글의 평균 댓글 도달 시간 대비 정규화가 필요하다.

### 동작

1. `build_merged_comments` 결과에 두 가지 정규화 컬럼 추가:
   - `PostMedianDeltaMinutes`: 해당 PostId 전체 댓글의 시간차 중앙값 (게시글 단위 베이스라인)
   - `DeltaRankInPost`: 해당 PostId 내에서 이 댓글의 시간차 백분위 (0~100, 작을수록 빠름)
2. 새 지표 `RelativeSpeedScore` 산출:
   - 한 사용자 쌍 (PostAuthor, Commenter)의 모든 댓글에 대해 `DeltaRankInPost`의 중앙값.
   - 값이 작을수록(예: 5) 그 사용자는 평균보다 일찍 댓글을 다는 경향이 강하다.
3. `Interactions` 시트에 컬럼 추가:
   - `MedianDeltaRankInPost`
   - 의미: "이 댓글 작성자는 같은 글에 달린 댓글 중 평균 N% 분위로 댓글을 달았다"
4. `FastCommentPatterns` 시트의 `BurstRatio` 옆에 `BurstRatioPercentile` 추가 (A-1과 연계).

### 출력 사양

`Interactions` 시트 컬럼 (최종 형태):

| Column | Description |
|---|---|
| PostAuthorKey | |
| CommenterKey | |
| InteractionCount | |
| InteractionCountPercentile | A-1 |
| UniquePostsCommented | |
| WithinWindowCount | |
| FastCommentRatio | |
| FastRatioPercentile | A-1 |
| MedianDeltaRankInPost | A-4 — 글 내 댓글 순서 백분위 중앙값 (낮을수록 일찍 댓글) |
| MedianDeltaMinutes | |
| MinDeltaMinutes | |

### 수용 기준

- [ ] `Interactions` 시트에 `MedianDeltaRankInPost` 컬럼이 추가된다.
- [ ] 한 PostId에 댓글이 1개뿐인 경우, 그 댓글의 `DeltaRankInPost`는 50.0 (중앙)으로 처리되며, 사용자 쌍 집계에 영향을 주지 않는다.
- [ ] ReportGuide 시트에 `MedianDeltaRankInPost` 해석 안내가 추가된다 (낮을수록 일찍 댓글이라는 사실).
- [ ] 정규화 컬럼 추가 후에도 기존 `FastCommentRatio` 값은 변경되지 않는다.

---

## 통합 수용 기준 (전체 작업 완료 시점)

- [ ] 5개 우선순위 각각의 수용 기준이 모두 충족된다.
- [ ] 익명화 ON 기본값으로 분석을 돌려도 모든 신규 지표(백분위, p-value, MedianDeltaRankInPost)가 정상 계산된다.
- [ ] ReportGuide 시트가 신규 시트 4개(`BaselineDistribution`, `BaselineComparison`, `CommenterKeywordShare`) 를 포함하도록 갱신된다.
- [ ] 결과 엑셀의 어떤 컬럼명/시트명/문서에도 단정적 표현("동일인", "공모", "조직적" 등)이 없다.
- [ ] 기존 단위 동작(시트 구조, 컬럼 의미)에 regression이 없다. 이전 버전 결과와 신규 컬럼만 추가된 상태여야 한다.
- [ ] `requirements.txt`에 추가된 의존성이 있다면 모두 기재된다 (예상: 변화 없음. 표준 라이브러리만 사용).
