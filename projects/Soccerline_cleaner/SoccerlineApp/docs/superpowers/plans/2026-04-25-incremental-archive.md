# 점진적 아카이브(체크포인트/재개) 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stage 2(상세 수집) 한 건이 끝날 때마다 즉시 SQLite에 저장하고, 다음 실행 시 Stage 1 직후 이미 받은 글을 시각적으로 표시 + 체크 자동 해제하여 "끊긴 지점부터 자연스럽게 이어가기"를 가능케 한다.

**Architecture:** `ArchiveStore` 에 `SaveOne(Post)` (UPSERT, 트랜잭션 단위) 와 `GetExistingIds()` (상세까지 받은 PostId 만) 추가, `CrawlerEngine.FetchSelectedDetailsAsync` 에 `onPostCompleted` 콜백 한 건당 호출 추가, `MainWindow` 가 Stage 1 종료 시 일괄 대조 + Stage 2 콜백으로 자동 저장.

**Tech Stack:** C# / .NET 10 / WPF, Microsoft.Data.Sqlite, Microsoft.Playwright

**Spec:** [`SoccerlineApp/docs/superpowers/specs/2026-04-25-incremental-archive-design.md`](../specs/2026-04-25-incremental-archive-design.md)

**테스트 환경:** 본 프로젝트는 단위 테스트 프로젝트가 없는 WPF 데스크탑 앱이다. 검증은 (1) 단계마다 `dotnet build` 통과, (2) 마지막에 수동 시나리오 테스트로 한다. 각 Task 끝에 빌드 검증 + 작은 커밋.

---

## 파일 변경 맵

| 파일 | 변경 |
|------|------|
| `SoccerlineApp/Models.cs` | `Post` 에 `IsArchived` 속성 추가 (Task 1) |
| `SoccerlineApp/ArchiveStore.cs` | `GetExistingIds()` 추가, `Save(IEnumerable)` 제거 + `SaveOne(Post)` 로 교체 (UPSERT) (Task 2) |
| `SoccerlineApp/CrawlerEngine.cs` | `FetchSelectedDetailsAsync` 시그니처에 `Action<Post>? onPostCompleted` 추가 (Task 3) |
| `SoccerlineApp/MainWindow.xaml.cs` | Stage 1 직후 자동 표시, Stage 2 콜백 자동 저장, `btnSaveArchive_Click` 및 `_hasUnsavedWork` 제거 (Task 4–5) |
| `SoccerlineApp/MainWindow.xaml` | [Save to Archive] 버튼 제거, "Archived" 컬럼 추가, `Post.IsArchived` DataTrigger 행 스타일 추가 (Task 6) |

---

## Task 1: Post 모델에 IsArchived 추가

**Files:**
- Modify: `SoccerlineApp/Models.cs`

- [ ] **Step 1: `IsArchived` 속성 추가**

`Models.cs` 의 `Post` 클래스, `DetailsFetched` 속성 정의 바로 아래에 추가:

```csharp
private bool _isArchived = false;
public bool IsArchived
{
    get => _isArchived;
    set { if (_isArchived != value) { _isArchived = value; OnPropertyChanged(); } }
}
```

전체 추가 위치는 `Models.cs:44` (현재 `DetailsFetched` 의 닫는 `}` 바로 다음 빈 줄).

- [ ] **Step 2: 빌드 검증**

```bash
dotnet build SoccerlineApp/SoccerlineApp.csproj -nologo -v quiet
```

Expected: `Build succeeded.` (warning 0~few, error 0)

- [ ] **Step 3: 커밋**

```bash
git add SoccerlineApp/Models.cs
git commit -m "feat: Post 에 IsArchived 속성 추가 (UI 바인딩용)"
```

---

## Task 2: ArchiveStore 를 UPSERT + SaveOne + GetExistingIds 로 재작성

**Files:**
- Modify: `SoccerlineApp/ArchiveStore.cs` (전체 재작성)

기존 `Save(IEnumerable<Post>)` / `SaveResult` 대신 `SaveOne(Post)` / `SaveOneResult` 만 둔다. UPSERT 정책으로 변경.

- [ ] **Step 1: ArchiveStore.cs 전체를 아래 내용으로 교체**

```csharp
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using SoccerlineApp.Models;

namespace SoccerlineApp;

public record SaveOneResult(bool Saved, bool SkippedNoPostId);

public class ArchiveStore
{
    private readonly string _connectionString;

    public ArchiveStore(string dbPath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
        }.ToString();

        InitializeSchema();
    }

    private void InitializeSchema()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Posts (
  PostId    TEXT PRIMARY KEY,
  BoardName TEXT NOT NULL,
  CreatedAt TEXT,
  Title     TEXT,
  Author    TEXT,
  AuthorId  TEXT,
  AuthorIp  TEXT,
  Views     TEXT,
  Likes     TEXT,
  Dislikes  TEXT,
  Link      TEXT,
  Body      TEXT,
  SavedAt   TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Comments (
  PostId    TEXT NOT NULL,
  OrderIdx  INTEGER NOT NULL,
  Nickname  TEXT,
  UserID    TEXT,
  AuthorIp  TEXT,
  CreatedAt TEXT,
  Content   TEXT,
  PRIMARY KEY (PostId, OrderIdx),
  FOREIGN KEY (PostId) REFERENCES Posts(PostId) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_posts_board_created ON Posts(BoardName, CreatedAt);
";
        cmd.ExecuteNonQuery();
    }

    public int Count()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Posts;";
        var result = cmd.ExecuteScalar();
        return result is long l ? (int)l : Convert.ToInt32(result);
    }

    /// <summary>
    /// 상세까지 수집된 PostId 의 집합. (본문이 있거나 댓글이 하나라도 있는 글)
    /// Stage 1 직후 일괄 대조에 사용한다.
    /// </summary>
    public HashSet<string> GetExistingIds()
    {
        var set = new HashSet<string>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT PostId FROM Posts
WHERE (Body IS NOT NULL AND Body != '')
   OR PostId IN (SELECT DISTINCT PostId FROM Comments);";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            set.Add(reader.GetString(0));
        }
        return set;
    }

    /// <summary>
    /// 한 건의 Post 와 그 댓글들을 단일 트랜잭션으로 UPSERT.
    /// 기존 레코드가 있으면 본문/메타를 갱신, 댓글은 전량 교체.
    /// </summary>
    public SaveOneResult SaveOne(Post post)
    {
        var postId = ExtractPostId(post.Link);
        if (string.IsNullOrEmpty(postId))
            return new SaveOneResult(Saved: false, SkippedNoPostId: true);

        using var conn = Open();
        using var tx = conn.BeginTransaction();

        using (var upsert = conn.CreateCommand())
        {
            upsert.Transaction = tx;
            upsert.CommandText = @"
INSERT INTO Posts
(PostId, BoardName, CreatedAt, Title, Author, AuthorId, AuthorIp, Views, Likes, Dislikes, Link, Body, SavedAt)
VALUES ($pid, $board, $created, $title, $author, $aid, $aip, $views, $likes, $dis, $link, $body, $saved)
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
  SavedAt   = excluded.SavedAt;";
            upsert.Parameters.AddWithValue("$pid",     postId);
            upsert.Parameters.AddWithValue("$board",   post.BoardName ?? "");
            upsert.Parameters.AddWithValue("$created", (object?)post.CreatedAt ?? DBNull.Value);
            upsert.Parameters.AddWithValue("$title",   (object?)post.Title    ?? DBNull.Value);
            upsert.Parameters.AddWithValue("$author",  (object?)post.Author   ?? DBNull.Value);
            upsert.Parameters.AddWithValue("$aid",     (object?)post.AuthorId ?? DBNull.Value);
            upsert.Parameters.AddWithValue("$aip",     (object?)post.AuthorIp ?? DBNull.Value);
            upsert.Parameters.AddWithValue("$views",   (object?)post.Views    ?? DBNull.Value);
            upsert.Parameters.AddWithValue("$likes",   (object?)post.Likes    ?? DBNull.Value);
            upsert.Parameters.AddWithValue("$dis",     (object?)post.Dislikes ?? DBNull.Value);
            upsert.Parameters.AddWithValue("$link",    (object?)post.Link     ?? DBNull.Value);
            upsert.Parameters.AddWithValue("$body",    (object?)post.Body     ?? DBNull.Value);
            upsert.Parameters.AddWithValue("$saved",   DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            upsert.ExecuteNonQuery();
        }

        // 댓글은 PostId 기준 전체 교체 (UPSERT 의미를 댓글까지 일관되게 적용)
        using (var delCmt = conn.CreateCommand())
        {
            delCmt.Transaction = tx;
            delCmt.CommandText = "DELETE FROM Comments WHERE PostId = $pid;";
            delCmt.Parameters.AddWithValue("$pid", postId);
            delCmt.ExecuteNonQuery();
        }

        if (post.Comments.Count > 0)
        {
            using var insertCmt = conn.CreateCommand();
            insertCmt.Transaction = tx;
            insertCmt.CommandText = @"
INSERT INTO Comments (PostId, OrderIdx, Nickname, UserID, AuthorIp, CreatedAt, Content)
VALUES ($pid, $idx, $nick, $uid, $ip, $created, $content);";
            var cPid    = insertCmt.CreateParameter(); cPid.ParameterName    = "$pid";     insertCmt.Parameters.Add(cPid);
            var cIdx    = insertCmt.CreateParameter(); cIdx.ParameterName    = "$idx";     insertCmt.Parameters.Add(cIdx);
            var cNick   = insertCmt.CreateParameter(); cNick.ParameterName   = "$nick";    insertCmt.Parameters.Add(cNick);
            var cUid    = insertCmt.CreateParameter(); cUid.ParameterName    = "$uid";     insertCmt.Parameters.Add(cUid);
            var cIp     = insertCmt.CreateParameter(); cIp.ParameterName     = "$ip";      insertCmt.Parameters.Add(cIp);
            var cCreat  = insertCmt.CreateParameter(); cCreat.ParameterName  = "$created"; insertCmt.Parameters.Add(cCreat);
            var cCont   = insertCmt.CreateParameter(); cCont.ParameterName   = "$content"; insertCmt.Parameters.Add(cCont);

            int order = 1;
            foreach (var c in post.Comments)
            {
                cPid.Value   = postId;
                cIdx.Value   = order++;
                cNick.Value  = (object?)c.Nickname ?? DBNull.Value;
                cUid.Value   = (object?)c.UserID ?? DBNull.Value;
                cIp.Value    = (object?)c.AuthorIp ?? DBNull.Value;
                cCreat.Value = (object?)c.CreatedAt ?? DBNull.Value;
                cCont.Value  = (object?)c.Content ?? DBNull.Value;
                insertCmt.ExecuteNonQuery();
            }
        }

        tx.Commit();
        return new SaveOneResult(Saved: true, SkippedNoPostId: false);
    }

    public List<Post> LoadAll()
    {
        var postById = new Dictionary<string, Post>();
        var ordered = new List<Post>();

        using var conn = Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
SELECT PostId, BoardName, CreatedAt, Title, Author, AuthorId, AuthorIp,
       Views, Likes, Dislikes, Link, Body
FROM Posts
ORDER BY CreatedAt DESC, PostId DESC;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string pid = reader.GetString(0);
                var post = new Post
                {
                    BoardName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    CreatedAt = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Title     = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Author    = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    AuthorId  = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    AuthorIp  = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    Views     = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    Likes     = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    Dislikes  = reader.IsDBNull(9) ? "" : reader.GetString(9),
                    Link      = reader.IsDBNull(10) ? "" : reader.GetString(10),
                    Body      = reader.IsDBNull(11) ? "" : reader.GetString(11),
                    IsSelected = true,
                    IsArchived = true,
                };
                postById[pid] = post;
                ordered.Add(post);
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
SELECT PostId, Nickname, UserID, AuthorIp, CreatedAt, Content
FROM Comments
ORDER BY PostId, OrderIdx;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string pid = reader.GetString(0);
                if (!postById.TryGetValue(pid, out var post)) continue;
                post.Comments.Add(new Comment
                {
                    Nickname  = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    UserID    = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    AuthorIp  = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    CreatedAt = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Content   = reader.IsDBNull(5) ? "" : reader.GetString(5),
                });
            }
        }

        foreach (var post in ordered)
            post.DetailsFetched = !string.IsNullOrEmpty(post.Body) || post.Comments.Count > 0;

        return ordered;
    }

    private static string ExtractPostId(string? link)
    {
        if (string.IsNullOrEmpty(link)) return "";
        var m = System.Text.RegularExpressions.Regex.Match(link, @"/board/(\d+)");
        return m.Success ? m.Groups[1].Value : "";
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
```

핵심 변경사항:
- `Save(IEnumerable<Post>)` 와 `SaveResult` 제거
- `SaveOne(Post)` + `SaveOneResult` 추가, UPSERT (`ON CONFLICT(PostId) DO UPDATE`)
- 댓글은 PostId 기준 DELETE 후 새로 INSERT (전체 교체)
- `GetExistingIds()` 추가, 본문/댓글 둘 중 하나라도 있는 PostId 만 반환
- `LoadAll()` 결과의 모든 Post 에 `IsArchived = true` 세팅 (DB 에서 왔으므로)

- [ ] **Step 2: 빌드 검증**

```bash
dotnet build SoccerlineApp/SoccerlineApp.csproj -nologo -v quiet
```

Expected: `Build succeeded.` 단, 다음 단계까지는 `MainWindow.xaml.cs` 가 옛 `Save(IEnumerable)` 시그니처를 호출하므로 **컴파일 에러 발생 가능**. 그 경우 다음 Task 의 변경에서 함께 해결하기로 하고, 이번 단계에서는 ArchiveStore 자체만 컴파일 OK 인지 확인.

**컴파일 에러 발생 시:** `MainWindow.xaml.cs:486` 의 `_archive.Save(candidates)` 호출 한 줄을 `// _archive.Save(candidates); // TODO Task4` 로 임시 주석 처리 후 빌드. 다음 Task 에서 해당 핸들러 자체를 제거함.

- [ ] **Step 3: 커밋**

```bash
git add SoccerlineApp/ArchiveStore.cs SoccerlineApp/MainWindow.xaml.cs
git commit -m "feat: ArchiveStore SaveOne(UPSERT) + GetExistingIds 로 재작성"
```

---

## Task 3: CrawlerEngine.FetchSelectedDetailsAsync 에 onPostCompleted 콜백 추가

**Files:**
- Modify: `SoccerlineApp/CrawlerEngine.cs:146-211`

- [ ] **Step 1: 시그니처에 콜백 인자 추가**

`CrawlerEngine.cs:146` 의 메서드 시그니처를 변경:

```csharp
public async Task FetchSelectedDetailsAsync(IList<Post> selected, CancellationToken ct,
    Action<Post>? onPostCompleted = null)
```

(기존 `(IList<Post> selected, CancellationToken ct)` 에 인자 1개 추가, 기본값 null 이라 기존 호출자도 호환)

- [ ] **Step 2: 한 건 완료 직후 콜백 호출**

`CrawlerEngine.cs:194` (`post.DetailsFetched = true;` 다음 줄) 직후에 추가:

```csharp
                    post.DetailsFetched = true;
                    onPostCompleted?.Invoke(post);   // 신규: 자동 저장 등 한 건 완료 알림

                    int cur = Interlocked.Increment(ref doneCount);
```

콜백 위치 근거: `DetailsFetched = true` 직후 → "본문/댓글이 다 들어찬 직후" 라 SaveOne 이 완전한 상태를 받게 됨.

- [ ] **Step 3: 빌드 검증**

```bash
dotnet build SoccerlineApp/SoccerlineApp.csproj -nologo -v quiet
```

Expected: `Build succeeded.`

- [ ] **Step 4: 커밋**

```bash
git add SoccerlineApp/CrawlerEngine.cs
git commit -m "feat: FetchSelectedDetailsAsync 에 onPostCompleted 콜백 추가"
```

---

## Task 4: MainWindow — Stage 1 종료 시 자동 표시

**Files:**
- Modify: `SoccerlineApp/MainWindow.xaml.cs:128-131` (Stage 1 완료 직후)

- [ ] **Step 1: Stage 1 완료 직후 GetExistingIds 호출 + IsArchived/IsSelected 자동 설정**

`MainWindow.xaml.cs:128` 의 다음 블록을:

```csharp
            AppendLog($"[SYSTEM] Stage 1 complete. {_posts.Count} posts. 헤더의 ▼ 버튼으로 필터 후 FETCH DETAILS.");
            btnFetchDetails.IsEnabled = _posts.Count > 0;
            if (_posts.Count > 0) _hasUnsavedWork = true;
```

다음으로 교체:

```csharp
            AppendLog($"[SYSTEM] Stage 1 complete. {_posts.Count} posts. 헤더의 ▼ 버튼으로 필터 후 FETCH DETAILS.");

            // 이미 DB 에 상세까지 들어있는 글은 회색 + 체크 해제로 표시
            if (_archive != null && _posts.Count > 0)
            {
                try
                {
                    var existing = _archive.GetExistingIds();
                    int marked = 0;
                    foreach (var p in _posts)
                    {
                        var pid = ExtractPostId(p.Link);
                        if (!string.IsNullOrEmpty(pid) && existing.Contains(pid))
                        {
                            p.IsArchived = true;
                            p.IsSelected = false;
                            marked++;
                        }
                    }
                    if (marked > 0)
                        AppendLog($"[ARCHIVE] {marked}건은 이미 수집됨 (회색 + 체크 해제).");
                }
                catch (SqliteException ex)
                {
                    AppendLog($"[ARCHIVE] 기존 ID 조회 실패: {ex.Message}");
                }
            }

            btnFetchDetails.IsEnabled = _posts.Count > 0;
```

(`_hasUnsavedWork = true` 라인 제거 — 다음 Task 에서 플래그 자체 제거.)

- [ ] **Step 2: 빌드 검증**

```bash
dotnet build SoccerlineApp/SoccerlineApp.csproj -nologo -v quiet
```

Expected: `Build succeeded.` (이 단계까지 `_hasUnsavedWork` 필드는 남아있어도 무방, 미사용 경고만)

- [ ] **Step 3: 커밋**

```bash
git add SoccerlineApp/MainWindow.xaml.cs
git commit -m "feat: Stage 1 종료 시 이미 받은 글 자동 표시 (회색+체크해제)"
```

---

## Task 5: MainWindow — Stage 2 자동 저장 콜백 + 수동 Save 핸들러 제거

**Files:**
- Modify: `SoccerlineApp/MainWindow.xaml.cs:147-186` (Stage 2 핸들러)
- Modify: `SoccerlineApp/MainWindow.xaml.cs:471-496` (btnSaveArchive_Click 제거)
- Modify: `SoccerlineApp/MainWindow.xaml.cs:27` (`_hasUnsavedWork` 필드 제거)
- Modify: `SoccerlineApp/MainWindow.xaml.cs:506-512` (Load 시 미저장 변경 확인 팝업 제거)

- [ ] **Step 1: Stage 2 핸들러에 콜백 연결**

`MainWindow.xaml.cs:166` 의 다음 블록을:

```csharp
        try
        {
            var engine = new CrawlerEngine(progress);
            await engine.FetchSelectedDetailsAsync(selected, _cts.Token);
            _hasUnsavedWork = true;
            AppendLog("[SYSTEM] Stage 2 complete.");
        }
```

다음으로 교체:

```csharp
        try
        {
            var engine = new CrawlerEngine(progress);

            Action<Post> onPostCompleted = post =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_archive == null) return;
                    try
                    {
                        var result = _archive.SaveOne(post);
                        if (result.Saved)
                        {
                            post.IsArchived = true;
                            UpdateArchiveCount();
                        }
                        else if (result.SkippedNoPostId)
                        {
                            AppendLog($"[ARCHIVE] postId 없음, 저장 스킵: {post.Title}");
                        }
                    }
                    catch (SqliteException ex)
                    {
                        AppendLog($"[ARCHIVE] 저장 실패 postId={ExtractPostId(post.Link)}: {ex.Message}");
                    }
                });
            };

            await engine.FetchSelectedDetailsAsync(selected, _cts.Token, onPostCompleted);
            AppendLog("[SYSTEM] Stage 2 complete.");
        }
```

(`_hasUnsavedWork = true` 제거. `engine.FetchSelectedDetailsAsync` 호출에 콜백 추가.)

- [ ] **Step 2: btnSaveArchive_Click 핸들러 전체 제거**

`MainWindow.xaml.cs:471-496` 의 `// ==== Archive: Save ====` 주석 줄부터 핸들러 닫는 `}` 까지 통째로 삭제:

```csharp
    // ==== Archive: Save ====
    private void btnSaveArchive_Click(object sender, RoutedEventArgs e)
    {
        // ... 본문 전체 ...
    }
```

→ 위 블록을 모두 제거. `// ==== Archive: Load ====` 주석은 유지.

- [ ] **Step 3: `_hasUnsavedWork` 필드 제거**

`MainWindow.xaml.cs:27` 의 다음 줄 삭제:

```csharp
    private bool _hasUnsavedWork;
```

- [ ] **Step 4: Load 시 미저장 변경 확인 팝업 제거**

`MainWindow.xaml.cs:506-512` 의 다음 블록을:

```csharp
        if (_hasUnsavedWork)
        {
            var confirm = MessageBox.Show(
                "현재 목록에 저장되지 않은 변경이 있습니다. 아카이브로 교체하시겠습니까?",
                "Confirm", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.OK) return;
        }
```

다음으로 교체 (현재 _posts 가 비어있지 않을 때만 간단 확인):

```csharp
        if (_posts.Count > 0)
        {
            var confirm = MessageBox.Show(
                "현재 목록을 아카이브 내용으로 교체하시겠습니까?",
                "Confirm", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.OK) return;
        }
```

- [ ] **Step 5: Load 후 `_hasUnsavedWork = false` 라인 제거**

`MainWindow.xaml.cs:522` 의 다음 줄 삭제:

```csharp
            _hasUnsavedWork = false;
```

- [ ] **Step 6: 빌드 검증**

```bash
dotnet build SoccerlineApp/SoccerlineApp.csproj -nologo -v quiet
```

Expected: `Build succeeded.`

만약 `_hasUnsavedWork` 잔존 참조로 인한 에러가 보이면 추가로 제거 (검색: `_hasUnsavedWork`).

- [ ] **Step 7: 커밋**

```bash
git add SoccerlineApp/MainWindow.xaml.cs
git commit -m "feat: Stage 2 자동 저장 콜백 연결, 수동 Save 핸들러/플래그 제거"
```

---

## Task 6: XAML — Save 버튼 제거, Archived 컬럼 추가, 행 스타일 트리거

**Files:**
- Modify: `SoccerlineApp/MainWindow.xaml:58` (Save 버튼)
- Modify: `SoccerlineApp/MainWindow.xaml:135-217` (DataGrid)

- [ ] **Step 1: [Save to Archive] 버튼 제거**

`MainWindow.xaml:58` 의 다음 한 줄 삭제:

```xml
                        <Button x:Name="btnSaveArchive" Content="Save to Archive" Click="btnSaveArchive_Click" Background="Transparent" Foreground="{StaticResource BrushTextSecondary}" BorderThickness="0" HorizontalAlignment="Left" Margin="0,0,0,8"/>
```

- [ ] **Step 2: DataGrid 의 RowStyle 에 IsArchived 트리거 추가**

`MainWindow.xaml:135-151` 의 DataGrid 여는 태그 직후 (`ColumnHeaderStyle="{StaticResource FilterHeaderStyle}">` 다음 줄, `<DataGrid.CellStyle>` 직전)에 `<DataGrid.RowStyle>` 블록을 추가:

```xml
                    <DataGrid.RowStyle>
                        <Style TargetType="DataGridRow">
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding IsArchived}" Value="True">
                                    <Setter Property="Foreground" Value="#888888"/>
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </DataGrid.RowStyle>
```

- [ ] **Step 3: "Archived" 컬럼 추가**

`MainWindow.xaml:165` 의 다음 줄 (체크박스 컬럼) 바로 다음에 새 컬럼 추가:

```xml
                        <DataGridCheckBoxColumn Header="✓" Binding="{Binding IsSelected, UpdateSourceTrigger=PropertyChanged}" Width="36"/>
                        <DataGridCheckBoxColumn Header="Archived" Binding="{Binding IsArchived}" Width="70" IsReadOnly="True"/>
```

(체크박스 컬럼으로 표시 — ✓ 표시 의도와 동일하고 기존 "상세" 컬럼과 일관됨)

- [ ] **Step 4: 빌드 검증**

```bash
dotnet build SoccerlineApp/SoccerlineApp.csproj -nologo -v quiet
```

Expected: `Build succeeded.`

- [ ] **Step 5: 커밋**

```bash
git add SoccerlineApp/MainWindow.xaml
git commit -m "feat: XAML — Save 버튼 제거, Archived 컬럼 + 행 회색 스타일 추가"
```

---

## Task 7: 통합 검증 — 빌드 + 수동 시나리오 테스트

빌드만 통과해도 코드 정합성은 확인되지만, 실제 사용 워크플로우는 사용자가 GUI 로 직접 검증해야 한다. 본 Task 는 그 시나리오 안내.

- [ ] **Step 1: 클린 빌드**

```bash
dotnet build SoccerlineApp/SoccerlineApp.csproj -nologo
```

Expected: `Build succeeded.` + 0 Warnings + 0 Errors (또는 직전과 동일 수준의 무관한 경고만).

- [ ] **Step 2: 앱 실행 및 시나리오 1 — 자동 저장 기본**

```bash
dotnet run --project SoccerlineApp/SoccerlineApp.csproj
```

수동 검증:
1. 라커룸 / 1 - 1 / authors 빈 칸으로 Stage 1 → 목록 표시 확인.
2. 5건 정도 체크 후 [FETCH DETAILS] → 진행 도중 [STOP] 누르기 (중간에 끊기).
3. 로그에 `[ARCHIVE] saved postId=...` 같은 줄이 한 건씩 찍히는지 확인.
4. 상태바 `archive: N` 카운트가 한 건 완료마다 증가하는지 확인.

- [ ] **Step 3: 시나리오 2 — 자동 표시 (재시작 후 이어가기)**

1. 앱 종료 후 재실행.
2. 같은 게시판 / 같은 페이지로 Stage 1 재실행.
3. 어제 받은 건이 회색 글씨 + Archived 컬럼 ✓ + 체크박스 자동 해제 상태로 표시되는지 확인.
4. 로그에 `[ARCHIVE] N건은 이미 수집됨 (회색 + 체크 해제).` 출력 확인.

- [ ] **Step 4: 시나리오 3 — 이어가기**

1. 위 상태에서 회색 외 항목 (체크 가능한 것) 만 선택.
2. [FETCH DETAILS] → 정상 진행, 한 건씩 자동 저장.
3. archive 카운트가 누적 증가하는지 확인.

- [ ] **Step 5: 시나리오 4 — UPSERT (직전 단계 호환성)**

직전 커밋 `6e822c7` 이후 Stage 1 만 저장한 레코드가 DB 에 있을 가능성 있음. 이 경우:
1. 같은 게시판 Stage 1 → Stage 1-only 레코드는 회색 표시되지 **않아야** 함 (본문/댓글 없으니 GetExistingIds 가 제외).
2. 해당 항목 체크 후 Stage 2 → 정상 진행, DB 에 본문/댓글 채워짐.
3. 같은 게시판 Stage 1 다시 → 이번엔 회색 표시됨 (본문이 채워졌으니).

- [ ] **Step 6: 시나리오 5 — Load Archive**

1. [Load Archive] 버튼 클릭.
2. 현재 _posts 가 있으면 확인 팝업, OK.
3. DB 의 모든 글이 로드되며 모두 회색 + Archived ✓ + DetailsFetched ✓ 상태로 표시되는지 확인.
4. Excel 내보내기 정상 동작 확인.

- [ ] **Step 7: 모든 시나리오 통과 시 마무리 커밋 (선택)**

별도 코드 변경 없으면 커밋 생략. 만약 시나리오 도중 작은 수정이 들어갔다면 그 단위로 커밋.

---

## 자체 검토 (구현 전 확인)

### Spec 커버리지

| Spec 섹션 | 처리 위치 |
|-----------|-----------|
| §3.1 Stage 1 자동 표시 | Task 4 |
| §3.1 Stage 2 한 건 자동 저장 | Task 3 + Task 5 |
| §3.2 트랜잭션 단위 안전 보장 | Task 2 (`SaveOne` 내 `BeginTransaction` ~ `Commit`) |
| §3.3 UPSERT 동일 PostId 갱신 | Task 2 |
| §4.1 파일 변경 목록 5개 | Task 1 (Models) / Task 2 (ArchiveStore) / Task 3 (CrawlerEngine) / Task 4-5 (MainWindow.cs) / Task 6 (xaml) |
| §4.2 GetExistingIds 필터 SQL | Task 2 |
| §4.3 SaveOne UPSERT SQL | Task 2 |
| §4.4 콜백 추가 | Task 3 |
| §4.5 MainWindow 의사코드 | Task 4 + Task 5 |
| §5.2 일회성 마이그레이션 불요 | (구조적으로 GetExistingIds 의 필터로 처리됨) |
| §6.1 Save 버튼 제거, Load 유지 | Task 5 + Task 6 |
| §6.2 Archived 컬럼 | Task 6 |
| §6.3 행 스타일 회색 | Task 6 |
| §6.4 상태바 갱신 | Task 5 (콜백 안 `UpdateArchiveCount()`) |
| §7 에러 처리 | Task 4 (catch SqliteException) + Task 5 (catch in 콜백) |
| §8 테스트 시나리오 | Task 7 |

### 모호성/일관성 체크

- `SaveOneResult(bool Saved, bool SkippedNoPostId)` — Task 2 와 Task 5 에서 같은 필드명 사용 ✓
- `GetExistingIds()` 시그니처 일관 ✓
- `FetchSelectedDetailsAsync(IList<Post>, CancellationToken, Action<Post>?)` — Task 3 정의, Task 5 사용 ✓
- `ExtractPostId` — `MainWindow.xaml.cs:532` 에 이미 존재 (private static), Task 4/5 에서 그대로 사용
- Task 5 에서 _hasUnsavedWork 잔존 참조 가능성 → grep 으로 확인 안내 포함

### YAGNI 확인

- "강제 재수집" 옵션, 자동 저장 토글, Archived 컬럼 ▼ 필터 — 모두 spec §2 에 "범위 밖"으로 명시됨, 구현 안 함 ✓
