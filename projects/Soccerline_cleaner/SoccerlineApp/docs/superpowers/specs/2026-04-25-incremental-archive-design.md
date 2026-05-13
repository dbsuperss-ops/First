# SoccerlineApp 점진적 아카이브(체크포인트/재개) 기능 설계

**작성일:** 2026-04-25
**대상 버전:** SoccerlineApp v0.3 (.NET 10 WPF)
**선행 문서:** `2026-04-24-archive-store-design.md` (수동 저장/로드 1차 구현)

---

## 1. 배경 및 목적

직전 단계에서 SQLite 기반 아카이브 저장소와 수동 [Save to Archive] / [Load Archive] 버튼을 도입했다. 그러나 실제 사용에서 의도한 워크플로우 — **대량 상세 크롤 도중 끊겨도 이미 받은 곳까지 자동 보존되고, 다음 실행에서 끊긴 지점부터 자연스럽게 이어가기** — 가 충족되지 않는다.

원인은 단순하다. 현재 [Save to Archive]는 Stage 2 (상세 수집) 가 모두 끝난 뒤 사용자가 명시적으로 누를 때만 동작한다. 도중에 끊기면 메모리(`_posts`)에 있던 결과는 모두 사라진다. 즉 현재 구조는 "안전한 보관소"이지 "체크포인트"가 아니다.

본 설계는 동작 모델을 **자동 점진적 저장 + Stage 1 단계의 자동 진척 표시**로 전환한다.

## 2. 범위

**포함**
- Stage 2 한 건 완료 시마다 SQLite 에 즉시 저장 (Post + 그 댓글들이 한 트랜잭션)
- Stage 1 완료 직후 DB 와 일괄 대조해 이미 받은 글을 시각적으로 표시 + 체크박스 자동 해제
- 같은 PostId 를 다시 만나면 UPSERT (기존 본문/댓글을 새 데이터로 갱신)
- 기존 [Save to Archive] 버튼 및 `_hasUnsavedWork` 플래그 제거
- [Load Archive] 버튼 및 상태바 `archive: N` 카운트는 유지 (실시간 갱신)

**제외 (이번 범위 밖)**
- 작업 파라미터(게시판/페이지 범위/필터) 의 자동 복원 — 사용자가 매번 Stage 1 을 새로 돌리는 워크플로우를 전제
- "강제 재수집 모드" 토글 — UPSERT 정책으로 자연스럽게 갱신되므로 별도 토글 불필요
- 자동 저장 ON/OFF 옵션 — 자동이 기본값이며 끌 이유가 없음 (YAGNI)

## 3. 사용자 관점 동작 흐름

### 3.1 신규 워크플로우

1. **Stage 1** (목록 수집) — 변동 없음.
2. **Stage 1 완료 직후 자동 표시** — 화면에 뜬 목록 중 이미 DB 에 상세까지 들어있는 PostId 는:
   - "Archived" 컬럼에 ✓
   - 행 글자색 회색 (`#888888`)
   - 체크박스 자동 해제
3. **사용자가 받을 것 선택** — 회색 행은 어차피 해제되어 있으므로 자연스럽게 "남은 것" 중에서 고르게 됨.
4. **Stage 2** (상세 수집) — 한 건 완료될 때마다 즉시 DB 에 commit. 진행 중 [Stop] 또는 앱 강제 종료 시 이미 commit 된 건은 보존됨.
5. **다음 날 재개** — 앱 켜고 같은 게시판/페이지 범위로 Stage 1 → 어제 받은 건 회색 처리됨 → 남은 것만 선택해 Stage 2.

### 3.2 안전 보장

- 한 건의 Post + 그 댓글들이 **하나의 트랜잭션** — 댓글까지 모두 들어가야 commit. 부분 저장 없음.
- 트랜잭션 단위가 작아 강제 종료 시 최대 1건 손실.
- 다음 실행에서 같은 글이 다시 큐에 들어오면 UPSERT 로 자연스럽게 보강됨.

### 3.3 동일 PostId 재수집 정책

- **UPSERT** — 기존 레코드의 본문/댓글이 새 데이터로 갱신됨.
- 사용자의 핵심 시나리오는 "끊긴 거 이어가기"이지만, 사이트 변화로 댓글이 늘어난 경우에도 자연스럽게 반영되므로 부수적 이익이 있음.
- 댓글은 PostId 기준 전체 DELETE 후 새로 INSERT (순서/내용을 새 데이터에 맞춤).

## 4. 아키텍처 변경

### 4.1 파일 변경 목록

| 파일 | 변경 종류 | 핵심 |
|------|-----------|------|
| `ArchiveStore.cs` | 수정 | `SaveOne(Post)` 추가, `GetExistingIds()` 추가, 기존 `Save(IEnumerable)` 제거, INSERT 를 UPSERT 로 변경 |
| `CrawlerEngine.cs` | 수정 | `FetchSelectedDetailsAsync` 에 `Action<Post>? onPostCompleted` 콜백 추가 |
| `MainWindow.xaml.cs` | 수정 | Stage 1 종료 시 자동 표시, Stage 2 에 콜백 연결, [Save] 핸들러 및 `_hasUnsavedWork` 제거 |
| `MainWindow.xaml` | 수정 | [Save to Archive] 버튼 제거, Posts 그리드 "Archived" 컬럼 추가, 행 스타일 트리거 |
| `Models.cs` | 수정 | `Post` 에 `IsArchived` 속성 추가 (UI 바인딩 전용, DB 무관) |

### 4.2 ArchiveStore 인터페이스 변경

```csharp
public class ArchiveStore
{
    public ArchiveStore(string dbPath);
    public int Count();
    public List<Post> LoadAll();

    // 신규
    public HashSet<string> GetExistingIds();      // Stage 1 직후 일괄 대조용. "상세까지 받은 것"만 반환.
    public SaveOneResult SaveOne(Post post);      // Stage 2 한 건 완료 시 호출, 한 트랜잭션. UPSERT.

    // 제거
    // public SaveResult Save(IEnumerable<Post> posts);
}

public record SaveOneResult(bool Saved, bool SkippedNoPostId);
```

`GetExistingIds` 는 다음 SQL 로 "상세까지 받은 PostId 만" 반환:

```sql
SELECT PostId FROM Posts
WHERE (Body IS NOT NULL AND Body != '')
   OR PostId IN (SELECT DISTINCT PostId FROM Comments);
```

이로써 직전 단계에서 Stage 1 만 저장된 레코드는 "미수집"으로 분류돼 Stage 2 통과 시 UPSERT 로 자연스럽게 보강된다.

### 4.3 SaveOne 의 UPSERT 구현

```sql
INSERT INTO Posts (PostId, BoardName, ..., SavedAt) VALUES (...)
ON CONFLICT(PostId) DO UPDATE SET
  BoardName = excluded.BoardName,
  CreatedAt = excluded.CreatedAt,
  Title     = excluded.Title,
  Author    = excluded.Author,
  AuthorId  = excluded.AuthorId,
  AuthorIp  = excluded.AuthorIp,
  Views     = excluded.Views,
  Likes     = excluded.Likes,
  Dislikes  = excluded.Dislikes,
  Link      = excluded.Link,
  Body      = excluded.Body,
  SavedAt   = excluded.SavedAt;
```

댓글은 `DELETE FROM Comments WHERE PostId = $pid` 후 새로 INSERT (모두 같은 트랜잭션 안에서).

### 4.4 CrawlerEngine 콜백 추가

```csharp
public async Task FetchSelectedDetailsAsync(
    List<Post> selected,
    CancellationToken ct,
    Action<Post>? onPostCompleted = null)
{
    foreach (var post in selected)
    {
        ct.ThrowIfCancellationRequested();
        await FetchOneDetailAsync(post, ct);    // 기존 로직
        onPostCompleted?.Invoke(post);           // 신규
    }
}
```

콜백이 null 이면 기존 동작과 100% 동일 (호환성 보존).

### 4.5 MainWindow 동작 흐름 (의사코드)

```csharp
// Stage 1 완료 직후 자동 표시
private async void btnStart_Click(...) {
    // 기존 로직 ...
    var existing = _archive?.GetExistingIds() ?? new HashSet<string>();
    foreach (var p in _posts) {
        if (existing.Contains(ExtractPostId(p.Link))) {
            p.IsArchived = true;
            p.IsSelected = false;
        }
    }
}

// Stage 2 — onPostCompleted 콜백으로 자동 저장
private async void btnFetchDetails_Click(...) {
    Action<Post> onPostCompleted = post => {
        Dispatcher.Invoke(() => {
            try {
                _archive!.SaveOne(post);
                post.IsArchived = true;
                UpdateArchiveCount();
            } catch (SqliteException ex) {
                AppendLog($"[ARCHIVE] 저장 실패 postId={ExtractPostId(post.Link)}: {ex.Message}");
                // 진행은 계속
            }
        });
    };
    await engine.FetchSelectedDetailsAsync(selected, _cts.Token, onPostCompleted);
}
```

## 5. DB 스키마 / 마이그레이션

### 5.1 스키마

**변경 없음.** 기존 `Posts` / `Comments` 테이블 그대로.

### 5.2 마이그레이션

`GetExistingIds()` 가 "상세까지 받은 PostId" 만 반환하도록 필터링하므로, **별도의 일회성 마이그레이션 스크립트는 필요 없다.** 직전 단계에서 Stage 1 만 저장된 레코드들은 다음 실행에서 자동으로 "미수집"으로 잡혀 UPSERT 시 보강된다.

## 6. UI 변경 상세

### 6.1 툴바

- **[Save to Archive] 제거**
- **[Load Archive] 유지** — 위치/동작 기존과 동일

### 6.2 Posts 그리드 — "Archived" 컬럼

- 위치: 첫 번째 체크박스 컬럼 바로 다음 (No. 컬럼 옆)
- 표시: `IsArchived == true` 면 ✓, 아니면 빈칸
- 폭: 60px, 헤더 텍스트 `Archived`
- 헤더 ▼ 필터 버튼은 추가하지 않음 (시각적 신호로 충분, YAGNI)

### 6.3 행 스타일

- DataGridRow 의 `Style.Triggers` 에 `DataTrigger Property="IsArchived" Value="True"` → `Foreground = #888888`.
- 회색 행도 클릭/스크롤은 정상 동작. "비활성화"가 아니라 "이미 받음" 표시.

### 6.4 상태바 갱신 시점

| 시점 | 동작 |
|------|------|
| 앱 시작 | `Count()` 1회 |
| Stage 2 한 건 완료 | `Count()` 1회 (텍스트 한 줄 업데이트라 부담 없음) |
| [Load Archive] 직후 | `Count()` 1회 |

### 6.5 IsArchived 변경 알림

- `Post` 모델이 이미 `INotifyPropertyChanged` 구현 중이면 `IsArchived` 만 알림 추가.
- 미구현 시 `IsArchived` 와 `IsSelected` 만 알림 처리 (최소 변경).
- 구현 시점에 코드 확인 후 가벼운 쪽으로 결정.

### 6.6 진행 로그

- 기존 `[STAGE2] N/M fetched: ...` 패턴 유지.
- 추가: 한 건 완료 시 `[ARCHIVE] saved postId=12345` 1줄. 디버깅 및 진척 가시성 향상.

## 7. 에러 / 엣지케이스

| 상황 | 처리 |
|------|------|
| Stage 2 도중 1건 저장 실패 (`SqliteException`) | 로그 `[ARCHIVE] 저장 실패 postId=...: {메시지}`, 진행 계속. 그 한 건만 손실, 다음번에 자동 보강. |
| Stage 2 도중 [Stop] | 진행 중 1건 포기, commit 된 건 보존. 다음 실행에서 이어감. |
| 앱 강제 종료 (프로세스 킬) | 마지막 commit ~ 다음 commit 사이의 1건만 손실. |
| `PostId` 추출 실패한 글 통과 | `SaveOne` 이 `SkippedNoPostId` 반환, 로그 `[ARCHIVE] postId 없음, 저장 스킵: {제목}`. 진행 계속. |
| Stage 1 후 `GetExistingIds` 실패 | 로그 `[ARCHIVE] 기존 ID 조회 실패: ...`, 모든 행을 "미수집"으로 간주. 자동 표시만 안 될 뿐 동작 가능. UPSERT 가 중복 갱신을 안전하게 처리. |
| DB 락 (다른 프로세스 점유) | 첫 SaveOne 실패 시 한 번만 MessageBox 안내, 이후 시도는 로그만 (반복 팝업 방지). 사용자가 앱 재시작 후 재시도. |
| DB 파일 삭제됨 (수동) | 다음 작업 시점에 `CREATE TABLE IF NOT EXISTS` 자동 생성. 모든 글 "미수집" 표시. |
| 직전 단계에서 Stage 1 만 저장된 레코드 | `GetExistingIds()` 의 필터 조건 덕에 "미수집"으로 분류됨. Stage 2 통과 시 UPSERT 로 보강. |

## 8. 테스트 관점

수동 시나리오 테스트 (단위 테스트 프로젝트 없음):

1. **자동 저장 기본**: 라커룸 1페이지 Stage 1 → 5건 선택 → Stage 2 도중 3번째에서 [Stop] → DB 에 2~3건 들어가있는지 DB 뷰어로 확인 + `archive:` 카운트 확인.
2. **자동 표시**: 같은 게시판 Stage 1 재실행 → 위 2~3건이 회색 + ✓ + 체크 해제 상태로 표시되는지.
3. **이어가기**: 회색 외 항목만 선택해 Stage 2 → 정상 진행, archive 카운트 누적 증가.
4. **UPSERT**: Stage 1 만 들어있던 PostId 가 Stage 2 통과 시 본문/댓글이 채워지는지 (직전 단계에서 만든 DB 로 검증).
5. **Stage 2 도중 강제 종료**: 진행 중 작업 관리자로 종료 → 재시작 후 archive 카운트 보존 확인.
6. **로드 호환**: [Load Archive] → 자동 저장된 데이터가 정상 로드되는지.
7. **PostId 없는 글**: Link 가 비정상인 가짜 Post 를 Stage 2 통과시켜 → 해당 1건만 스킵, 나머지 정상.

## 9. 향후 확장 여지 (범위 밖)

- "강제 재수집" 토글 → 현재 UPSERT 정책으로 자연스럽게 갱신되므로 사실상 불필요. 굳이 분리하려면 ArchiveStore 에 `SaveOne(post, force: bool)` 추가 가능.
- 작업 파라미터 자동 복원 (게시판/페이지/필터) → `LastSession` 테이블 추가.
- 기간/게시판 조건부 로드 → `LoadAll()` → `Load(filter)` 로 확장.
- Archived 컬럼 헤더의 ▼ 필터 → 다른 컬럼과 일관되게 추가 가능 (현재는 YAGNI).
