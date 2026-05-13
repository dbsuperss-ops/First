using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using SoccerlineApp.Models;

namespace SoccerlineApp;

public class CrawlerEngine
{
    private readonly IProgress<string> _progress;
    private readonly HttpClient _httpClient;

    private static readonly Dictionary<string, int> BoardCategoryMap = new()
    {
        { "라커룸",   5 },
        { "축구소식", 1 },
        { "국외게시판", 3 },
    };

    private const string BaseUrl = "https://soccerline.kr";
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    public CrawlerEngine(IProgress<string> progress) 
    { 
        _progress = progress; 
        
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ko-KR,ko;q=0.9,en-US;q=0.8,en;q=0.7");
    }

    private void Log(string message) => _progress.Report($"[{DateTime.Now:HH:mm:ss}] {message}");

    // ==== Stage 1: 목록만 빠르게 수집 ====
    public async Task RunListOnlyAsync(string board, int startPage, int endPage,
        List<string> targetAuthors, DateOnly? startDate, DateOnly? endDate,
        CancellationToken ct, Action<Post> onPost,
        Action<int>? onPageComplete = null)
    {
        if (!BoardCategoryMap.TryGetValue(board, out var categoryId))
        {
            Log($"[ERROR] Unknown board: {board}");
            return;
        }
        Log($"[LIST] Board: {board} (cat={categoryId}), Pages: {startPage}-{endPage}");

        // 목록 페이지를 병렬로 가져오기 위한 풀 (너무 많이 한 번에 요청하면 차단될 수 있으므로 동시성 제한)
        const int MAX_CONCURRENT_PAGES = 5;
        var semaphore = new SemaphoreSlim(MAX_CONCURRENT_PAGES);
        var pageTasks = new List<Task>();

        for (int p = startPage; p <= endPage; p++)
        {
            if (ct.IsCancellationRequested) break;
            
            int currentPage = p;
            pageTasks.Add(Task.Run(async () => 
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    string listUrl = $"{BaseUrl}/board?categoryDepth01={categoryId}&page={currentPage - 1}";
                    Log($"[FETCH] {listUrl}");
                    
                    using var response = await _httpClient.GetAsync(listUrl, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        Log($"[HTTP] Page {currentPage} Status: {(int)response.StatusCode}");
                        return;
                    }

                    var html = await response.Content.ReadAsStringAsync(ct);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    var rows = doc.DocumentNode.SelectNodes("//section[contains(@class, 'brdList')]//table/tbody/tr");
                    if (rows == null)
                    {
                        Log($"[WARN] Page {currentPage}: Table rows did not appear.");
                        return;
                    }

                    int added = 0;
                    foreach (var tr in rows)
                    {
                        if (ct.IsCancellationRequested) break;
                        var tds = tr.SelectNodes("td");
                        if (tds == null || tds.Count < 6) continue;

                        string idStr = tds[0].InnerText.Trim();
                        if (!Regex.IsMatch(idStr, @"^\d+$")) continue;

                        var anchor = tds[1].SelectSingleNode(".//a[contains(@href, '/board/')]");
                        var titleEl = tds[1].SelectSingleNode(".//span[contains(@class, 'title')]");
                        var replyEl = tds[1].SelectSingleNode(".//span[contains(@class, 'reply')]");

                        string title = (titleEl != null ? titleEl.InnerText : (anchor != null ? anchor.InnerText : "")).Trim();
                        title = HtmlEntity.DeEntitize(title);
                        string commentCountStr = replyEl != null ? Regex.Replace(replyEl.InnerText, @"[\[\]\s]", "") : "";
                        string href = anchor?.GetAttributeValue("href", "") ?? "";
                        string author = HtmlEntity.DeEntitize(tds[2].InnerText.Trim());
                        string date = tds[3].InnerText.Trim();
                        string views = tds[4].InnerText.Trim();
                        string likes = tds[5].InnerText.Trim();

                        if (targetAuthors.Any() && !targetAuthors.Any(a => author.Contains(a, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        // 날짜 범위 필터
                        if (startDate.HasValue && endDate.HasValue)
                        {
                            var postDate = ParseListDate(date);
                            if (postDate == null) continue;
                            if (postDate.Value < startDate.Value || postDate.Value > endDate.Value) continue;
                        }

                        var fullLink = href.StartsWith("http") ? href : BaseUrl + href;
                        int.TryParse(commentCountStr, out int cc);
                        var post = new Post
                        {
                            BoardName = board,
                            Title = title,
                            Author = author,
                            CreatedAt = date,
                            Views = views,
                            Likes = likes,
                            Link = fullLink,
                            IsSelected = true,
                        };
                        for (int i = 0; i < cc; i++) post.Comments.Add(new Comment());

                        lock (onPost) { onPost(post); }
                        added++;
                    }
                    Log($"[PAGE {currentPage}] Added {added} posts.");
                    onPageComplete?.Invoke(currentPage);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { Log($"[EXCEPTION] Page {currentPage}: {ex.Message}"); }
                finally
                {
                    semaphore.Release();
                }
            }, ct));
        }

        await Task.WhenAll(pageTasks);
        Log("[LIST] Done.");
    }

    // ==== Stage 2: 체크된 Post 의 상세(본문/댓글/IP) 수집 ====
    public async Task FetchSelectedDetailsAsync(IList<Post> selected, CancellationToken ct)
    {
        if (selected.Count == 0) { Log("[DETAIL] No posts selected."); return; }
        Log($"[DETAIL] Fetching details for {selected.Count} posts.");

        const int MAX_CONCURRENT_DETAILS = 15; // 브라우저 엔진이 아니므로 동시 요청 수를 늘림
        var gate = new SemaphoreSlim(MAX_CONCURRENT_DETAILS);
        int doneCount = 0;
        int successCount = 0, deletedCount = 0, failCount = 0;
        const int MAX_RETRIES = 2;

        var tasks = selected.Select(async post =>
        {
            await gate.WaitAsync(ct);
            try
            {
                FetchOutcome outcome = FetchOutcome.TransientFailure;
                for (int attempt = 1; attempt <= MAX_RETRIES + 1; attempt++)
                {
                    outcome = await FetchPostDetailAsync(post, ct);
                    if (outcome != FetchOutcome.TransientFailure) break;
                    if (attempt <= MAX_RETRIES)
                    {
                        Log($" - Retry {attempt}/{MAX_RETRIES}: {post.Link}");
                        await Task.Delay(500 * attempt, ct);
                    }
                }

                switch (outcome)
                {
                    case FetchOutcome.Success:
                        post.DetailsFetched = true;
                        post.IsArchived = false;
                        Interlocked.Increment(ref successCount);
                        break;
                    case FetchOutcome.Deleted:
                        post.IsDeleted = true;
                        post.DetailsFetched = true;
                        post.IsArchived = false;
                        Interlocked.Increment(ref deletedCount);
                        Log($" - Deleted: {post.Link}");
                        break;
                    case FetchOutcome.TransientFailure:
                        Interlocked.Increment(ref failCount);
                        Log($" - Detail FAIL after {MAX_RETRIES + 1} attempts: {post.Link}");
                        break;
                }

                int cur = Interlocked.Increment(ref doneCount);
                if (cur % 10 == 0 || cur == selected.Count)
                    Log($"[DETAIL] Progress: {cur}/{selected.Count}");
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();

        await Task.WhenAll(tasks);
        Log($"[DETAIL] Done. Success: {successCount}, Deleted: {deletedCount}, Failed: {failCount}");
    }

    public enum FetchOutcome { Success, Deleted, TransientFailure }

    private async Task<FetchOutcome> FetchPostDetailAsync(Post post, CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.GetAsync(post.Link, ct);
            
            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone) 
                return FetchOutcome.Deleted;
            if ((int)response.StatusCode >= 500) 
                return FetchOutcome.TransientFailure;

            var finalUrl = response.RequestMessage?.RequestUri?.ToString();
            if (finalUrl != null && !finalUrl.Contains("/board/")) 
                return FetchOutcome.Deleted;

            var html = await response.Content.ReadAsStringAsync(ct);
            if (html.Contains("삭제된") || html.Contains("존재하지 않") || html.Contains("찾을 수 없"))
                return FetchOutcome.Deleted;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var writerBox = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'writerBox')]");
            if (writerBox == null) return FetchOutcome.TransientFailure;

            // 작성자 정보 추출
            string author = "", userId = "", authorIp = "", datetime = "";
            var nameBtn = writerBox.SelectSingleNode(".//a[contains(@class, 'btnUser')]");
            if (nameBtn != null) author = nameBtn.InnerText.Trim();

            var idSpan = writerBox.SelectSingleNode(".//*[contains(@class, 'nameBox')]/span");
            if (idSpan != null)
            {
                string raw = Regex.Replace(idSpan.InnerText, @"^[(\s]+|[)\s]+$", "").Trim();
                if (raw.Contains(","))
                {
                    var parts = raw.Split(',').Select(s => s.Trim()).ToArray();
                    userId = parts[0];
                    authorIp = parts.FirstOrDefault(p => Regex.IsMatch(p, @"\d+\.\d+\.\S+")) ?? "";
                }
                else { userId = raw; }
            }

            var dataSpans = writerBox.SelectNodes(".//div[contains(@class, 'dataBox')]/span");
            if (dataSpans != null)
            {
                foreach (var s in dataSpans)
                {
                    var t = s.InnerText.Trim();
                    if (t.StartsWith("작성일")) datetime = Regex.Replace(t, @"^작성일\s*:\s*", "").Trim();
                    else if (t.StartsWith("IP")) authorIp = Regex.Replace(t, @"^IP\s*:\s*", "").Trim();
                }
            }

            // 본문 추출
            var txtBox = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'txtBox')]");
            string bodyText = "";
            if (txtBox != null)
            {
                // 줄바꿈 등을 보존하기 위해 간단한 처리
                bodyText = HtmlEntity.DeEntitize(txtBox.InnerText).Trim();
                if (string.IsNullOrEmpty(bodyText))
                {
                    bool hasImg = txtBox.SelectSingleNode(".//img") != null;
                    bool hasVideo = txtBox.SelectSingleNode(".//video") != null || txtBox.SelectSingleNode(".//iframe") != null;
                    var tags = new List<string>();
                    if (hasImg) tags.Add("[image]");
                    if (hasVideo) tags.Add("[video]");
                    bodyText = string.Join("", tags);
                }
                if (bodyText.Length > 10000) bodyText = bodyText.Substring(0, 10000);
            }

            // 댓글 추출
            var comments = new List<Comment>();
            var commentLis = doc.DocumentNode.SelectNodes("//*[@id='board-view-comment-list']//ul[contains(@class, 'cList')]/li");
            if (commentLis != null)
            {
                foreach (var li in commentLis)
                {
                    string cNick = "", cUid = "", cIp = "", cCreatedAt = "";
                    var cNameBtn = li.SelectSingleNode(".//*[contains(@class, 'btnUser')]") 
                                ?? li.SelectSingleNode(".//*[contains(@class, 'nameBox')]//a")
                                ?? li.SelectSingleNode(".//b")
                                ?? li.SelectSingleNode(".//strong");
                    if (cNameBtn != null) cNick = HtmlEntity.DeEntitize(cNameBtn.InnerText.Trim());

                    var cIdSpan = li.SelectSingleNode(".//*[contains(@class, 'nameBox')]/span");
                    if (cIdSpan != null)
                    {
                        string raw = Regex.Replace(cIdSpan.InnerText, @"^[(\s]+|[)\s]+$", "").Trim();
                        if (raw.Contains(","))
                        {
                            var parts = raw.Split(',').Select(s => s.Trim()).ToArray();
                            cUid = parts[0];
                            cIp = parts.FirstOrDefault(p => Regex.IsMatch(p, @"\d+\.\d+\.\S+")) ?? "";
                        }
                        else cUid = raw;
                    }

                    var allSpans = li.SelectNodes(".//span");
                    if (allSpans != null)
                    {
                        foreach (var span in allSpans)
                        {
                            string s = span.InnerText.Trim();
                            if (Regex.IsMatch(s, @"\d{4}-\d{2}-\d{2}") || 
                                Regex.IsMatch(s, @"^\d{2}:\d{2}(:\d{2})?$") || 
                                Regex.IsMatch(s, @"^\d{2}-\d{2}\s+\d{2}:\d{2}"))
                            {
                                cCreatedAt = Regex.Replace(s, @"^작성일\s*:\s*", "").Trim();
                                break;
                            }
                        }
                    }

                    // Content 추출 (메타 텍스트 제거)
                    string content = HtmlEntity.DeEntitize(li.InnerText);
                    var metaParts = new List<string> { cNick, $"({cUid})", $"({cUid}, {cIp})", $"({cIp})", $"작성일: {cCreatedAt}", cCreatedAt, "신고", "답글", "삭제", "수정" };
                    foreach (var m in metaParts.Where(m => !string.IsNullOrEmpty(m)))
                    {
                        content = content.Replace(m, " ");
                    }
                    content = Regex.Replace(content, @"\s+", " ").Trim();
                    
                    if (string.IsNullOrEmpty(content) && string.IsNullOrEmpty(cNick)) continue;

                    comments.Add(new Comment
                    {
                        Nickname = cNick,
                        UserID = cUid,
                        AuthorIp = cIp,
                        Content = content,
                        CreatedAt = cCreatedAt
                    });
                }
            }

            post.Body = bodyText;
            if (!string.IsNullOrEmpty(userId)) post.AuthorId = userId;
            if (!string.IsNullOrEmpty(authorIp)) post.AuthorIp = authorIp;
            if (!string.IsNullOrEmpty(datetime)) post.CreatedAt = datetime;
            if (!string.IsNullOrEmpty(author)) post.Author = HtmlEntity.DeEntitize(author);

            post.Comments.Clear();
            foreach (var c in comments) post.Comments.Add(c);

            post.IsDeleted = false;
            return FetchOutcome.Success;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Log($" - Fetch failed: {post.Link} ({ex.Message})");
            return FetchOutcome.TransientFailure;
        }
    }

    private static DateOnly? ParseListDate(string dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        var now = DateTime.Now;
        try
        {
            if (dateStr.Contains(':'))
                return DateOnly.FromDateTime(now);

            if (dateStr.Contains('.'))
            {
                var parts = dateStr.Split('.');
                if (parts.Length == 3)
                {
                    int year = parts[0].Length == 4 ? int.Parse(parts[0]) : 2000 + int.Parse(parts[0]);
                    return new DateOnly(year, int.Parse(parts[1]), int.Parse(parts[2]));
                }
                if (parts.Length == 2)
                    return new DateOnly(now.Year, int.Parse(parts[0]), int.Parse(parts[1]));
            }
        }
        catch { }
        return null;
    }
}