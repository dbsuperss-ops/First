using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using SoccerlineApp.Models;

namespace SoccerlineApp;

public record SaveResult(int Inserted, int SkippedDuplicate, int SkippedNoPostId);

public record CrawlCheckpointRow(
    string Stage,
    string? BoardName,
    int? RangeStart,
    int? RangeEnd,
    int? LastCompletedPage,
    string? AuthorsFilter,
    string SavedAt);

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
  SavedAt   TEXT NOT NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0
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

CREATE TABLE IF NOT EXISTS CrawlCheckpoint (
  Id                INTEGER PRIMARY KEY CHECK (Id = 1),
  Stage             TEXT NOT NULL,
  BoardName         TEXT,
  RangeStart        INTEGER,
  RangeEnd          INTEGER,
  LastCompletedPage INTEGER,
  AuthorsFilter     TEXT,
  SavedAt           TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS PendingDetail (
  PostId TEXT PRIMARY KEY
);
";
        cmd.ExecuteNonQuery();

        // 마이그레이션: 기존 DB 에 IsDeleted 컬럼이 없으면 추가
        EnsureColumn(conn, "Posts", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
    }

    private static void EnsureColumn(SqliteConnection conn, string table, string column, string definition)
    {
        using var info = conn.CreateCommand();
        info.CommandText = $"PRAGMA table_info({table});";
        using (var reader = info.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return;
            }
        }
        using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
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
        int inserted = 0;
        int skippedDup = 0;
        int skippedNoId = 0;

        using var conn = Open();
        using var tx = conn.BeginTransaction();

        // INSERT OR IGNORE 경로 (DetailsFetched=false 용 — list-only)
        using var insertIgnore = conn.CreateCommand();
        insertIgnore.Transaction = tx;
        insertIgnore.CommandText = @"
INSERT OR IGNORE INTO Posts
(PostId, BoardName, CreatedAt, Title, Author, AuthorId, AuthorIp, Views, Likes, Dislikes, Link, Body, SavedAt, IsDeleted)
VALUES ($pid, $board, $created, $title, $author, $aid, $aip, $views, $likes, $dis, $link, $body, $saved, $del);";
        BindPostParams(insertIgnore, out var iiP);

        // UPSERT 경로 (DetailsFetched=true 용 — 본문/댓글 갱신)
        using var upsert = conn.CreateCommand();
        upsert.Transaction = tx;
        upsert.CommandText = @"
INSERT INTO Posts
(PostId, BoardName, CreatedAt, Title, Author, AuthorId, AuthorIp, Views, Likes, Dislikes, Link, Body, SavedAt, IsDeleted)
VALUES ($pid, $board, $created, $title, $author, $aid, $aip, $views, $likes, $dis, $link, $body, $saved, $del)
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
  SavedAt   = excluded.SavedAt,
  IsDeleted = excluded.IsDeleted;";
        BindPostParams(upsert, out var upP);

        using var deleteCmts = conn.CreateCommand();
        deleteCmts.Transaction = tx;
        deleteCmts.CommandText = "DELETE FROM Comments WHERE PostId = $pid;";
        var dPid = deleteCmts.CreateParameter(); dPid.ParameterName = "$pid"; deleteCmts.Parameters.Add(dPid);

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
            // 이미 archive 와 동기화된 글은 재기록 불필요. FETCH DETAILS / 신규 수집 시 IsArchived=false 로 리셋됨.
            if (post.IsArchived) { skippedDup++; continue; }

            var postId = ExtractPostId(post.Link);
            if (string.IsNullOrEmpty(postId)) { skippedNoId++; continue; }

            if (post.DetailsFetched)
            {
                // UPSERT: 신규 또는 갱신 모두 저장으로 카운트.
                AssignPostParams(upP, postId, post, savedAt);
                upsert.ExecuteNonQuery();
                inserted++;

                // 댓글: 기존 삭제 후 재삽입.
                dPid.Value = postId;
                deleteCmts.ExecuteNonQuery();

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
            else
            {
                // INSERT OR IGNORE: 기존 행이 있으면 보존, 댓글은 더미라 저장 안 함.
                AssignPostParams(iiP, postId, post, savedAt);
                int rows = insertIgnore.ExecuteNonQuery();
                if (rows == 0) skippedDup++;
                else inserted++;
            }

            post.IsArchived = true;
        }

        tx.Commit();
        return new SaveResult(inserted, skippedDup, skippedNoId);
    }

    public List<Post> LoadAll() => LoadPostsCore(null, null);

    public List<Post> LoadPostsByBoard(string boardName)
    {
        return LoadPostsCore("WHERE BoardName = $board",
            cmd =>
            {
                var p = cmd.CreateParameter(); p.ParameterName = "$board"; p.Value = boardName ?? "";
                cmd.Parameters.Add(p);
            });
    }

    public List<Post> LoadPostsByIds(IEnumerable<string> postIds)
    {
        var ids = postIds.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        if (ids.Count == 0) return new List<Post>();

        using var conn = Open();
        using var tx = conn.BeginTransaction();

        BuildPidsTempTable(conn, tx, "__filter_pids", ids);

        var ordered = ReadPostsAndComments(conn, tx,
            "WHERE PostId IN (SELECT PostId FROM __filter_pids)",
            null);

        tx.Commit();
        return ordered;
    }

    private List<Post> LoadPostsCore(string? whereClause, Action<SqliteCommand>? bindWhere)
    {
        using var conn = Open();
        return ReadPostsAndComments(conn, null, whereClause, bindWhere);
    }

    private static void BuildPidsTempTable(SqliteConnection conn, SqliteTransaction tx,
        string tableName, IEnumerable<string> postIds)
    {
        using (var create = conn.CreateCommand())
        {
            create.Transaction = tx;
            create.CommandText = $"DROP TABLE IF EXISTS {tableName}; CREATE TEMP TABLE {tableName} (PostId TEXT PRIMARY KEY);";
            create.ExecuteNonQuery();
        }
        using (var ins = conn.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = $"INSERT OR IGNORE INTO {tableName} (PostId) VALUES ($pid);";
            var p = ins.CreateParameter(); p.ParameterName = "$pid"; ins.Parameters.Add(p);
            foreach (var id in postIds) { p.Value = id; ins.ExecuteNonQuery(); }
        }
    }

    private static List<Post> ReadPostsAndComments(
        SqliteConnection conn, SqliteTransaction? tx,
        string? postsWhere, Action<SqliteCommand>? bindPostsWhere)
    {
        var postById = new Dictionary<string, Post>();
        var ordered = new List<Post>();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = $@"
SELECT PostId, BoardName, CreatedAt, Title, Author, AuthorId, AuthorIp,
       Views, Likes, Dislikes, Link, Body, IsDeleted
FROM Posts
{postsWhere ?? ""}
ORDER BY CreatedAt DESC, PostId DESC;";
            bindPostsWhere?.Invoke(cmd);
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
                    IsDeleted = !reader.IsDBNull(12) && reader.GetInt32(12) != 0,
                    IsSelected = true,
                };
                postById[pid] = post;
                ordered.Add(post);
            }
        }

        if (ordered.Count > 0)
        {
            // 댓글은 로드된 PostId 들에 대해서만 조회. Comments 테이블은 BoardName 등을 모르므로
            // postsWhere 를 그대로 쓸 수 없음 → temp table 로 PostId 집합을 전달.
            var ownsTx = tx == null;
            var localTx = tx ?? conn.BeginTransaction();
            try
            {
                BuildPidsTempTable(conn, localTx, "__cmt_pids", postById.Keys);

                using var cmd = conn.CreateCommand();
                cmd.Transaction = localTx;
                cmd.CommandText = @"
SELECT PostId, Nickname, UserID, AuthorIp, CreatedAt, Content
FROM Comments
WHERE PostId IN (SELECT PostId FROM __cmt_pids)
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

                if (ownsTx) localTx.Commit();
            }
            catch
            {
                if (ownsTx) localTx.Rollback();
                throw;
            }
            finally
            {
                if (ownsTx) localTx.Dispose();
            }
        }

        foreach (var post in ordered)
        {
            post.DetailsFetched = !string.IsNullOrEmpty(post.Body) || post.Comments.Count > 0;
            post.IsArchived = true;  // archive 에서 로드된 글은 동기화 상태로 시작
        }

        return ordered;
    }

    // ==== Checkpoint API ====

    public CrawlCheckpointRow? GetCheckpoint()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT Stage, BoardName, RangeStart, RangeEnd, LastCompletedPage, AuthorsFilter, SavedAt
FROM CrawlCheckpoint WHERE Id = 1;";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new CrawlCheckpointRow(
            Stage:             reader.GetString(0),
            BoardName:         reader.IsDBNull(1) ? null : reader.GetString(1),
            RangeStart:        reader.IsDBNull(2) ? null : reader.GetInt32(2),
            RangeEnd:          reader.IsDBNull(3) ? null : reader.GetInt32(3),
            LastCompletedPage: reader.IsDBNull(4) ? null : reader.GetInt32(4),
            AuthorsFilter:     reader.IsDBNull(5) ? null : reader.GetString(5),
            SavedAt:           reader.GetString(6));
    }

    public void WriteListCheckpoint(string boardName, int rangeStart, int rangeEnd,
        int lastCompletedPage, string authorsFilter)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();

        using (var del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM PendingDetail;";
            del.ExecuteNonQuery();
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
INSERT OR REPLACE INTO CrawlCheckpoint
(Id, Stage, BoardName, RangeStart, RangeEnd, LastCompletedPage, AuthorsFilter, SavedAt)
VALUES (1, 'List', $board, $rs, $re, $lp, $auth, $saved);";
        AddParam(cmd, "$board", boardName ?? "");
        AddParam(cmd, "$rs", rangeStart);
        AddParam(cmd, "$re", rangeEnd);
        AddParam(cmd, "$lp", lastCompletedPage);
        AddParam(cmd, "$auth", (object?)authorsFilter ?? DBNull.Value);
        AddParam(cmd, "$saved", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();

        tx.Commit();
    }

    public void WriteDetailCheckpoint(IEnumerable<string> pendingPostIds)
    {
        var ids = pendingPostIds.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

        using var conn = Open();
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT OR REPLACE INTO CrawlCheckpoint
(Id, Stage, BoardName, RangeStart, RangeEnd, LastCompletedPage, AuthorsFilter, SavedAt)
VALUES (1, 'Detail', NULL, NULL, NULL, NULL, NULL, $saved);";
            AddParam(cmd, "$saved", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();
        }
        using (var del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM PendingDetail;";
            del.ExecuteNonQuery();
        }
        using (var ins = conn.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = "INSERT OR IGNORE INTO PendingDetail (PostId) VALUES ($pid);";
            var p = ins.CreateParameter(); p.ParameterName = "$pid"; ins.Parameters.Add(p);
            foreach (var id in ids) { p.Value = id; ins.ExecuteNonQuery(); }
        }

        tx.Commit();
    }

    public void ClearCheckpoint()
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM CrawlCheckpoint; DELETE FROM PendingDetail;";
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public List<string> GetPendingPostIds()
    {
        var result = new List<string>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PostId FROM PendingDetail;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(reader.GetString(0));
        return result;
    }

    // ==== 내부 유틸 ====

    private struct PostParams
    {
        public SqliteParameter Pid, Board, Created, Title, Author, AuthorId, AuthorIp,
                               Views, Likes, Dislikes, Link, Body, Saved, Deleted;
    }

    private static void BindPostParams(SqliteCommand cmd, out PostParams p)
    {
        p = new PostParams
        {
            Pid      = AddParam(cmd, "$pid",     DBNull.Value),
            Board    = AddParam(cmd, "$board",   DBNull.Value),
            Created  = AddParam(cmd, "$created", DBNull.Value),
            Title    = AddParam(cmd, "$title",   DBNull.Value),
            Author   = AddParam(cmd, "$author",  DBNull.Value),
            AuthorId = AddParam(cmd, "$aid",     DBNull.Value),
            AuthorIp = AddParam(cmd, "$aip",     DBNull.Value),
            Views    = AddParam(cmd, "$views",   DBNull.Value),
            Likes    = AddParam(cmd, "$likes",   DBNull.Value),
            Dislikes = AddParam(cmd, "$dis",     DBNull.Value),
            Link     = AddParam(cmd, "$link",    DBNull.Value),
            Body     = AddParam(cmd, "$body",    DBNull.Value),
            Saved    = AddParam(cmd, "$saved",   DBNull.Value),
            Deleted  = AddParam(cmd, "$del",     0),
        };
    }

    private static void AssignPostParams(PostParams p, string postId, Post post, string savedAt)
    {
        p.Pid.Value      = postId;
        p.Board.Value    = post.BoardName ?? "";
        p.Created.Value  = (object?)post.CreatedAt ?? DBNull.Value;
        p.Title.Value    = (object?)post.Title ?? DBNull.Value;
        p.Author.Value   = (object?)post.Author ?? DBNull.Value;
        p.AuthorId.Value = (object?)post.AuthorId ?? DBNull.Value;
        p.AuthorIp.Value = (object?)post.AuthorIp ?? DBNull.Value;
        p.Views.Value    = (object?)post.Views ?? DBNull.Value;
        p.Likes.Value    = (object?)post.Likes ?? DBNull.Value;
        p.Dislikes.Value = (object?)post.Dislikes ?? DBNull.Value;
        p.Link.Value     = (object?)post.Link ?? DBNull.Value;
        p.Body.Value     = (object?)post.Body ?? DBNull.Value;
        p.Saved.Value    = savedAt;
        p.Deleted.Value  = post.IsDeleted ? 1 : 0;
    }

    private static SqliteParameter AddParam(SqliteCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
        return p;
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
        // WAL: 동시성·쓰기 처리량 향상. synchronous=NORMAL: 트랜잭션 커밋 시 fsync 생략(WAL 환경에서 안전).
        // PRAGMA journal_mode=WAL 은 DB 헤더에 영구 저장되지만 매 연결마다 호출해도 무해.
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA temp_store=MEMORY;";
        pragma.ExecuteNonQuery();
        return conn;
    }
}
