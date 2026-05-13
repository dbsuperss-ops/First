# Archive Store (SQLite 영구 저장소) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** SoccerlineApp 에 수동 저장/로드식 SQLite 아카이브를 추가해 크롤 결과가 앱 종료 후에도 보존되고 재로드 가능하게 만든다.

**Architecture:** `ArchiveStore` 클래스가 SQLite 파일 하나(`soccerline_archive.db`)에 대한 init/Save/LoadAll/Count 를 제공한다. `MainWindow` 는 툴바 버튼 2개(Save/Load) 와 상태바 개수 표시만 추가하고 나머지는 기존 `_posts` 컬렉션을 그대로 사용한다.

**Tech Stack:** .NET 10 WPF, `Microsoft.Data.Sqlite` 9.0.0, 기존 `SoccerlineApp.Models.Post` / `Comment`

**Spec:** [docs/superpowers/specs/2026-04-24-archive-store-design.md](../specs/2026-04-24-archive-store-design.md)

**Testing note:** 프로젝트에 테스트 프로젝트가 없다. 각 태스크는 `dotnet build` 성공 + 명시된 수동 스모크 시나리오로 검증한다. 테스트 프로젝트 도입은 이 작업의 범위가 아니다.

---

## 파일 구조

| 파일 | 상태 | 책임 |
|------|------|------|
| `SoccerlineApp/SoccerlineApp.csproj` | 수정 | `Microsoft.Data.Sqlite` PackageReference 추가 |
| `SoccerlineApp/ArchiveStore.cs` | 신규 | SQLite 에 대한 얇은 래퍼 — 스키마 init, Count, Save, LoadAll |
| `SoccerlineApp/MainWindow.xaml` | 수정 | 사이드바에 Save/Load 버튼, 그리드 상단에 아카이브 개수 |
| `SoccerlineApp/MainWindow.xaml.cs` | 수정 | 버튼 핸들러, `_hasUnsavedWork`, `_archive` 필드, 상태바 갱신 |

---

## Task 1: 패키지 추가 및 빌드 확인

**Files:**
- Modify: `SoccerlineApp/SoccerlineApp.csproj`

- [ ] **Step 1: `SoccerlineApp.csproj` 에 `Microsoft.Data.Sqlite` 참조 추가**

`<PackageReference Include="Microsoft.Playwright" Version="1.59.0" />` 라인 바로 밑에 추가:

```xml
    <PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.0" />
```

- [ ] **Step 2: 빌드 확인**

Run: `dotnet build SoccerlineApp/SoccerlineApp.csproj`
Expected: `Build succeeded.` (경고 몇 개 허용, 오류 0)

- [ ] **Step 3: 커밋**

```bash
git add SoccerlineApp/SoccerlineApp.csproj
git commit -m "chore: add Microsoft.Data.Sqlite package reference"
```

---

## Task 2: ArchiveStore 뼈대 — 스키마 init + Count

**Files:**
- Create: `SoccerlineApp/ArchiveStore.cs`

- [ ] **Step 1: `SoccerlineApp/ArchiveStore.cs` 새 파일 생성, 스키마 init 과 Count 만 우선 구현**

```csharp
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using SoccerlineApp.Models;

namespace SoccerlineApp;

public record SaveResult(int Inserted, int SkippedDuplicate, int SkippedNoPostId);

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

    public SaveResult Save(IEnumerable<Post> posts)
    {
        throw new NotImplementedException("Task 3");
    }

    public List<Post> LoadAll()
    {
        throw new NotImplementedException("Task 4");
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
```

- [ ] **Step 2: 빌드 확인**

Run: `dotnet build SoccerlineApp/SoccerlineApp.csproj`
Expected: `Build succeeded.` 오류 0.

- [ ] **Step 3: 커밋**

```bash
git add SoccerlineApp/ArchiveStore.cs
git commit -m "feat: ArchiveStore skeleton with schema init and Count"
```

---

## Task 3: ArchiveStore.Save 구현

**Files:**
- Modify: `SoccerlineApp/ArchiveStore.cs` (Save 메서드 본체)

- [ ] **Step 1: `Save()` 메서드 구현 — 단일 트랜잭션, 중복 스킵, PostId 없음 스킵**

`ArchiveStore.cs` 의 `public SaveResult Save(IEnumerable<Post> posts)` 메서드를 다음으로 완전히 교체:

```csharp
    public SaveResult Save(IEnumerable<Post> posts)
    {
        int inserted = 0;
        int skippedDup = 0;
        int skippedNoId = 0;

        using var conn = Open();
        using var tx = conn.BeginTransaction();

        using var insertPost = conn.CreateCommand();
        insertPost.Transaction = tx;
        insertPost.CommandText = @"
INSERT OR IGNORE INTO Posts
(PostId, BoardName, CreatedAt, Title, Author, AuthorId, AuthorIp, Views, Likes, Dislikes, Link, Body, SavedAt)
VALUES ($pid, $board, $created, $title, $author, $aid, $aip, $views, $likes, $dis, $link, $body, $saved);";
        var pPid    = insertPost.CreateParameter(); pPid.ParameterName    = "$pid";     insertPost.Parameters.Add(pPid);
        var pBoard  = insertPost.CreateParameter(); pBoard.ParameterName  = "$board";   insertPost.Parameters.Add(pBoard);
        var pCreat  = insertPost.CreateParameter(); pCreat.ParameterName  = "$created"; insertPost.Parameters.Add(pCreat);
        var pTitle  = insertPost.CreateParameter(); pTitle.ParameterName  = "$title";   insertPost.Parameters.Add(pTitle);
        var pAuthor = insertPost.CreateParameter(); pAuthor.ParameterName = "$author";  insertPost.Parameters.Add(pAuthor);
        var pAid    = insertPost.CreateParameter(); pAid.ParameterName    = "$aid";     insertPost.Parameters.Add(pAid);
        var pAip    = insertPost.CreateParameter(); pAip.ParameterName    = "$aip";     insertPost.Parameters.Add(pAip);
        var pViews  = insertPost.CreateParameter(); pViews.ParameterName  = "$views";   insertPost.Parameters.Add(pViews);
        var pLikes  = insertPost.CreateParameter(); pLikes.ParameterName  = "$likes";   insertPost.Parameters.Add(pLikes);
        var pDis    = insertPost.CreateParameter(); pDis.ParameterName    = "$dis";     insertPost.Parameters.Add(pDis);
        var pLink   = insertPost.CreateParameter(); pLink.ParameterName   = "$link";    insertPost.Parameters.Add(pLink);
        var pBody   = insertPost.CreateParameter(); pBody.ParameterName   = "$body";    insertPost.Parameters.Add(pBody);
        var pSaved  = insertPost.CreateParameter(); pSaved.ParameterName  = "$saved";   insertPost.Parameters.Add(pSaved);

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

        string savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        foreach (var post in posts)
        {
            var postId = ExtractPostId(post.Link);
            if (string.IsNullOrEmpty(postId)) { skippedNoId++; continue; }

            pPid.Value    = postId;
            pBoard.Value  = post.BoardName ?? "";
            pCreat.Value  = (object?)post.CreatedAt ?? DBNull.Value;
            pTitle.Value  = (object?)post.Title ?? DBNull.Value;
            pAuthor.Value = (object?)post.Author ?? DBNull.Value;
            pAid.Value    = (object?)post.AuthorId ?? DBNull.Value;
            pAip.Value    = (object?)post.AuthorIp ?? DBNull.Value;
            pViews.Value  = (object?)post.Views ?? DBNull.Value;
            pLikes.Value  = (object?)post.Likes ?? DBNull.Value;
            pDis.Value    = (object?)post.Dislikes ?? DBNull.Value;
            pLink.Value   = (object?)post.Link ?? DBNull.Value;
            pBody.Value   = (object?)post.Body ?? DBNull.Value;
            pSaved.Value  = savedAt;

            int rows = insertPost.ExecuteNonQuery();
            if (rows == 0) { skippedDup++; continue; }

            inserted++;

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
        return new SaveResult(inserted, skippedDup, skippedNoId);
    }

    private static string ExtractPostId(string? link)
    {
        if (string.IsNullOrEmpty(link)) return "";
        var m = System.Text.RegularExpressions.Regex.Match(link, @"/board/(\d+)");
        return m.Success ? m.Groups[1].Value : "";
    }
```

- [ ] **Step 2: 빌드 확인**

Run: `dotnet build SoccerlineApp/SoccerlineApp.csproj`
Expected: `Build succeeded.` 오류 0.

- [ ] **Step 3: 커밋**

```bash
git add SoccerlineApp/ArchiveStore.cs
git commit -m "feat: ArchiveStore.Save — transactional, skip duplicate PostId"
```

---

## Task 4: ArchiveStore.LoadAll 구현

**Files:**
- Modify: `SoccerlineApp/ArchiveStore.cs` (LoadAll 메서드 본체)

- [ ] **Step 1: `LoadAll()` 메서드 구현 — Posts 전체 + 관련 Comments 조인 로드**

`ArchiveStore.cs` 의 `public List<Post> LoadAll()` 메서드를 다음으로 완전히 교체:

```csharp
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
                    DetailsFetched = true,
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

        return ordered;
    }
```

- [ ] **Step 2: 빌드 확인**

Run: `dotnet build SoccerlineApp/SoccerlineApp.csproj`
Expected: `Build succeeded.` 오류 0.

- [ ] **Step 3: 커밋**

```bash
git add SoccerlineApp/ArchiveStore.cs
git commit -m "feat: ArchiveStore.LoadAll — load posts and joined comments"
```

---

## Task 5: MainWindow.xaml — 버튼과 상태바 추가

**Files:**
- Modify: `SoccerlineApp/MainWindow.xaml`

- [ ] **Step 1: 사이드바 `btnExport` 뒤에 Save/Load 버튼 추가**

기존 블록:

```xml
                    <StackPanel Margin="0,24,0,0">
                        <Button x:Name="btnOpenFolder" Content="Open Folder" Background="Transparent" Foreground="{StaticResource BrushTextSecondary}" BorderThickness="0" HorizontalAlignment="Left" Margin="0,0,0,8"/>
                        <Button x:Name="btnExport" Content="Export Excel" Background="Transparent" Foreground="{StaticResource BrushTextSecondary}" BorderThickness="0" HorizontalAlignment="Left"/>
                    </StackPanel>
```

을 다음으로 교체:

```xml
                    <StackPanel Margin="0,24,0,0">
                        <Button x:Name="btnOpenFolder" Content="Open Folder" Background="Transparent" Foreground="{StaticResource BrushTextSecondary}" BorderThickness="0" HorizontalAlignment="Left" Margin="0,0,0,8"/>
                        <Button x:Name="btnExport" Content="Export Excel" Background="Transparent" Foreground="{StaticResource BrushTextSecondary}" BorderThickness="0" HorizontalAlignment="Left" Margin="0,0,0,8"/>
                        <Button x:Name="btnSaveArchive" Content="Save to Archive" Click="btnSaveArchive_Click" Background="Transparent" Foreground="{StaticResource BrushTextSecondary}" BorderThickness="0" HorizontalAlignment="Left" Margin="0,0,0,8"/>
                        <Button x:Name="btnLoadArchive" Content="Load Archive" Click="btnLoadArchive_Click" Background="Transparent" Foreground="{StaticResource BrushTextSecondary}" BorderThickness="0" HorizontalAlignment="Left"/>
                    </StackPanel>
```

- [ ] **Step 2: Results 상단 DockPanel 에 아카이브 개수 표시 추가**

기존 블록:

```xml
                <DockPanel Grid.Row="0" Margin="0,0,0,6" LastChildFill="False">
                    <TextBlock Text="Results — 헤더 클릭으로 정렬, 헤더의 ▼ 버튼으로 엑셀 스타일 필터" Style="{StaticResource BodySmall}" DockPanel.Dock="Left"/>
                    <TextBlock x:Name="txtGridStatus" Text="" Style="{StaticResource BodySmall}" DockPanel.Dock="Right"/>
                </DockPanel>
```

을 다음으로 교체:

```xml
                <DockPanel Grid.Row="0" Margin="0,0,0,6" LastChildFill="False">
                    <TextBlock Text="Results — 헤더 클릭으로 정렬, 헤더의 ▼ 버튼으로 엑셀 스타일 필터" Style="{StaticResource BodySmall}" DockPanel.Dock="Left"/>
                    <TextBlock x:Name="txtGridStatus" Text="" Style="{StaticResource BodySmall}" DockPanel.Dock="Right"/>
                    <TextBlock x:Name="txtArchiveCount" Text="archive: 0" Style="{StaticResource BodySmall}" DockPanel.Dock="Right" Margin="0,0,16,0"/>
                </DockPanel>
```

- [ ] **Step 3: 빌드 확인 — 핸들러가 아직 없어 실패 예상**

Run: `dotnet build SoccerlineApp/SoccerlineApp.csproj`
Expected: **FAIL**, 오류 메시지: `'MainWindow' does not contain a definition for 'btnSaveArchive_Click'` (또는 `btnLoadArchive_Click`). 다음 태스크에서 핸들러를 추가한다.

- [ ] **Step 4: 커밋은 다음 태스크와 함께 한다**

이 태스크의 수정은 Task 6 완료 후 함께 커밋한다. 지금 커밋하지 말 것.

---

## Task 6: MainWindow.xaml.cs — 핸들러, 플래그, 상태바 초기화

**Files:**
- Modify: `SoccerlineApp/MainWindow.xaml.cs`

- [ ] **Step 1: using 문 추가**

파일 상단 `using SoccerlineApp.Models;` 아래에 추가:

```csharp
using Microsoft.Data.Sqlite;
```

- [ ] **Step 2: 필드 추가**

클래스 상단 `private readonly ObservableCollection<Post> _posts = new();` 아래에 추가:

```csharp
    private readonly string _dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "soccerline_archive.db");
    private ArchiveStore? _archive;
    private bool _hasUnsavedWork;
```

- [ ] **Step 3: `MainWindow_Loaded` 수정 — ArchiveStore 초기화 + 개수 표시**

기존:

```csharp
    private void MainWindow_Loaded(object sender, RoutedEventArgs e) => LoadSettings();
```

을 다음으로 교체:

```csharp
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSettings();
        try
        {
            _archive = new ArchiveStore(_dbPath);
            UpdateArchiveCount();
        }
        catch (SqliteException ex)
        {
            AppendLog($"[CRITICAL] Archive DB 초기화 실패: {ex.Message}");
            MessageBox.Show($"아카이브 DB 초기화 실패: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateArchiveCount()
    {
        try
        {
            int n = _archive?.Count() ?? 0;
            txtArchiveCount.Text = $"archive: {n:N0}";
        }
        catch (SqliteException ex)
        {
            txtArchiveCount.Text = "archive: ?";
            AppendLog($"[ARCHIVE] count failed: {ex.Message}");
        }
    }
```

- [ ] **Step 4: `btnFetchDetails_Click` 끝부분에 `_hasUnsavedWork = true` 세팅**

기존 블록:

```csharp
            var engine = new CrawlerEngine(progress);
            await engine.FetchSelectedDetailsAsync(selected, _cts.Token);
            AppendLog("[SYSTEM] Stage 2 complete.");
```

을 다음으로 교체:

```csharp
            var engine = new CrawlerEngine(progress);
            await engine.FetchSelectedDetailsAsync(selected, _cts.Token);
            _hasUnsavedWork = true;
            AppendLog("[SYSTEM] Stage 2 complete.");
```

- [ ] **Step 5: Save/Load 핸들러 추가**

`ExtractPostId` 메서드 바로 위에(또는 클래스 끝자락의 아무 적절한 위치에) 다음 메서드 2개 추가:

```csharp
    // ==== Archive: Save ====
    private void btnSaveArchive_Click(object sender, RoutedEventArgs e)
    {
        if (_archive == null)
        {
            MessageBox.Show("아카이브가 초기화되지 않았습니다.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        var candidates = _posts.Where(p => p.DetailsFetched).ToList();
        if (candidates.Count == 0)
        {
            MessageBox.Show("상세 수집된 항목이 없습니다.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            var result = _archive.Save(candidates);
            AppendLog($"[ARCHIVE] 신규 {result.Inserted}건 저장, {result.SkippedDuplicate}건 스킵(중복), {result.SkippedNoPostId}건 스킵(PostId 없음)");
            _hasUnsavedWork = false;
            UpdateArchiveCount();
        }
        catch (SqliteException ex)
        {
            AppendLog($"[CRITICAL] Archive 저장 실패: {ex.Message}");
            MessageBox.Show($"아카이브 저장 실패: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==== Archive: Load ====
    private void btnLoadArchive_Click(object sender, RoutedEventArgs e)
    {
        if (_archive == null)
        {
            MessageBox.Show("아카이브가 초기화되지 않았습니다.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        if (_hasUnsavedWork)
        {
            var confirm = MessageBox.Show(
                "현재 목록에 저장되지 않은 변경이 있습니다. 아카이브로 교체하시겠습니까?",
                "Confirm", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.OK) return;
        }
        try
        {
            var loaded = _archive.LoadAll();
            _posts.Clear();
            _columnFilters.Clear();
            foreach (var p in loaded) _posts.Add(p);
            CollectionViewSource.GetDefaultView(grdPosts.ItemsSource).Refresh();
            UpdateSelectionCount();
            AppendLog($"[ARCHIVE] {loaded.Count}건 로드됨");
            _hasUnsavedWork = false;
            btnFetchDetails.IsEnabled = _posts.Count > 0;
        }
        catch (SqliteException ex)
        {
            AppendLog($"[CRITICAL] Archive 로드 실패: {ex.Message}");
            MessageBox.Show($"아카이브 로드 실패: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
```

- [ ] **Step 6: 빌드 확인**

Run: `dotnet build SoccerlineApp/SoccerlineApp.csproj`
Expected: `Build succeeded.` 오류 0.

- [ ] **Step 7: 커밋 (Task 5 의 XAML 변경과 함께)**

```bash
git add SoccerlineApp/MainWindow.xaml SoccerlineApp/MainWindow.xaml.cs
git commit -m "feat: Save to Archive / Load Archive buttons and archive count"
```

---

## Task 7: 수동 스모크 테스트

**Files:** 변경 없음. 실행 검증.

- [ ] **Step 1: 앱 실행 + DB 자동 생성 확인**

Run: `dotnet run --project SoccerlineApp/SoccerlineApp.csproj`
Expected:
- 앱 창이 뜨고 `archive: 0` 이 그리드 상단에 보임.
- `SoccerlineApp/bin/Debug/net10.0-windows/soccerline_archive.db` 파일이 생성됨.

- [ ] **Step 2: 저장 없이 Save 버튼 → "상세 수집된 항목이 없습니다" 팝업 확인**

START LIST 실행 전 [Save to Archive] 클릭 → 정보 팝업 확인.

- [ ] **Step 3: 기본 저장 시나리오**

1. 라커룸 / 페이지 1-1 / START LIST
2. 아무 5건 정도 체크 → FETCH DETAILS
3. [Save to Archive] → 로그에 `[ARCHIVE] 신규 5건 저장, 0건 스킵(중복), 0건 스킵(PostId 없음)` 유사 출력
4. 상태바 `archive: 5` (숫자 일치) 확인

- [ ] **Step 4: 중복 스킵 확인**

같은 조건으로 1-3 단계 반복. Expected: `신규 0건 저장, 5건 스킵(중복)`. 상태바 숫자 변동 없음.

- [ ] **Step 5: 로드 교체 확인**

1. 앱 종료 후 재실행
2. 상태바에 Step 3 의 숫자만큼 `archive: N` 표시되는지 확인
3. [Load Archive] 클릭 → 그리드에 N건 표시, 모두 `상세=체크` 상태
4. 로그에 `[ARCHIVE] N건 로드됨`

- [ ] **Step 6: 미저장 변경 확인 팝업**

1. 재실행 후 새로 크롤+FETCH DETAILS 까지 수행 (저장은 안 함)
2. [Load Archive] 클릭 → "현재 목록에 저장되지 않은 변경이 있습니다..." 확인팝업
3. 취소 → 그리드 그대로, OK → 아카이브 내용으로 교체

- [ ] **Step 7: 로드된 데이터 Excel 내보내기 확인**

Load Archive 후 Export Excel → 기존과 동일하게 xlsx 생성, 본문/댓글 시트에 내용 존재.

- [ ] **Step 8: 결과 정리 + 커밋**

수동 테스트 중 발견된 버그가 있으면 수정 + 커밋. 없으면 태스크 완료 처리.

```bash
# 필요 시:
git commit -am "fix: <발견된 문제>"
```

---

## 완료 조건

- 모든 Task 의 체크박스가 `[x]` 로 마크됨.
- `dotnet build SoccerlineApp/SoccerlineApp.csproj` 경고 수 증가 없이 성공.
- Task 7 의 스모크 시나리오 전부 통과.
- `soccerline_archive.db` 가 앱 기동 시 자동 생성되고 Save/Load 가 예상대로 동작.
