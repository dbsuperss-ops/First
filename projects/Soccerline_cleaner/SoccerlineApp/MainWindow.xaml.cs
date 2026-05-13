using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Data.Sqlite;
using SoccerlineApp.Models;

namespace SoccerlineApp;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _cts;
    private readonly ObservableCollection<Post> _posts = new();

    // 컬럼별 허용값 집합. null/missing = 필터 없음(전체 통과).
    private readonly Dictionary<string, HashSet<string>> _columnFilters = new();

    private readonly string _dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "soccerline_archive.db");
    private ArchiveStore? _archive;
    private bool _hasUnsavedWork;

    // 진행 중인 크롤 세션 상태 (Save 시점에 DB 체크포인트로 옮겨 적힘)
    private string _currentStage = "Idle";  // "Idle" | "List" | "Detail"
    private string _currentBoard = "";
    private int _currentRangeStart;
    private int _currentRangeEnd;
    private int _lastCompletedPage;
    private string _currentAuthorsFilter = "";

    private System.Windows.Threading.DispatcherTimer? _autoSaveTimer;
    private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromMinutes(5);

    // bulk add/select 동안 N²-급 UI 갱신을 막기 위한 가드.
    private bool _suspendUiUpdates;

    public MainWindow()
    {
        InitializeComponent();
        btnOpenFolder.Click += (s, e) => OpenOutputFolder();
        btnExport.Click += (s, e) => ExportToExcel();
        this.Loaded += MainWindow_Loaded;
        this.Closing += MainWindow_Closing;

        grdPosts.ItemsSource = _posts;
        var view = CollectionViewSource.GetDefaultView(_posts);
        view.Filter = FilterPredicate;
        _posts.CollectionChanged += OnPostsCollectionChanged;
    }

    private void OnPostsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // PropertyChanged 구독은 항상 유지 (가벼우며 누락되면 향후 변화에 반응 못 함).
        if (e.OldItems != null)
            foreach (Post p in e.OldItems) p.PropertyChanged -= OnPostPropertyChanged;
        if (e.NewItems != null)
            foreach (Post p in e.NewItems) p.PropertyChanged += OnPostPropertyChanged;

        if (_suspendUiUpdates) return;
        UpdateSelectionCount();
        UpdateFetchDetailsButtonState();
    }

    private void OnPostPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_suspendUiUpdates) return;
        if (e.PropertyName == nameof(Post.IsSelected) || e.PropertyName == nameof(Post.DetailsFetched))
        {
            UpdateSelectionCount();
            UpdateFetchDetailsButtonState();
        }
    }

    // bulk 작업(로드/일괄 선택)을 감싸는 헬퍼. body 실행 중 OnPosts/OnPostPropertyChanged 의 UI 갱신을 건너뛰고,
    // 종료 후 한 번만 갱신한다.
    private void RunWithoutUiUpdates(Action body)
    {
        _suspendUiUpdates = true;
        try { body(); }
        finally
        {
            _suspendUiUpdates = false;
            UpdateSelectionCount();
            UpdateFetchDetailsButtonState();
        }
    }

    // FETCH DETAILS 버튼 상태를 데이터에 동기화. 크롤 진행 중에는 변경하지 않는다.
    private void UpdateFetchDetailsButtonState()
    {
        Dispatcher.Invoke(() =>
        {
            if (_cts != null) return;
            btnFetchDetails.IsEnabled = _posts.Any(p => p.IsSelected && !p.DetailsFetched);
        });
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSettings();
        try
        {
            _archive = new ArchiveStore(_dbPath);
            UpdateArchiveCount();
            RefreshResumeButtonState();
        }
        catch (SqliteException ex)
        {
            AppendLog($"[CRITICAL] Archive DB 초기화 실패: {ex.Message}");
            MessageBox.Show($"아카이브 DB 초기화 실패: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        _autoSaveTimer = new System.Windows.Threading.DispatcherTimer { Interval = AutoSaveInterval };
        _autoSaveTimer.Tick += AutoSaveTick;
        _autoSaveTimer.Start();

        // 창이 완전히 표시된 뒤 미완료 작업 안내 다이얼로그를 띄운다.
        Dispatcher.BeginInvoke(new Action(PromptResumeIfAvailable),
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void PromptResumeIfAvailable()
    {
        if (_archive == null) return;
        var cp = SafeGetCheckpoint();
        if (cp == null || !IsResumeAvailable(cp)) return;

        string summary;
        if (cp.Stage == "List")
        {
            summary = $"[Stage 1] {cp.BoardName} 페이지 {cp.RangeStart}-{cp.RangeEnd}, " +
                      $"페이지 {cp.LastCompletedPage}까지 완료";
        }
        else // Detail
        {
            int pending = 0;
            try { pending = _archive.GetPendingPostIds().Count; } catch { }
            summary = $"[Stage 2] 상세 수집 미완료 {pending}건";
        }

        AppendLog($"[RESUME] 미완료 작업 감지: {summary}");

        var conf = MessageBox.Show(
            $"이전 작업이 미완료 상태입니다.\n{summary}\n\n이전 데이터를 복원하시겠습니까?\n(복원 후 RESUME 버튼을 눌러 이어서 진행할 수 있습니다.)",
            "이어가기", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
        if (conf != MessageBoxResult.Yes) return;

        if (cp.Stage == "List")
        {
            RestorePostsForListResume(cp);
        }
        else
        {
            AppendLog("[RESUME] Stage 2 복원은 RESUME 버튼을 눌러 진행하세요.");
        }
    }

    private void RestorePostsForListResume(CrawlCheckpointRow cp)
    {
        if (_archive == null) return;

        // UI 컨피그 자동 채움
        if (!string.IsNullOrEmpty(cp.BoardName))
        {
            foreach (ComboBoxItem item in comboBoard.Items)
            {
                if ((item.Content as string) == cp.BoardName) { comboBoard.SelectedItem = item; break; }
            }
        }
        if (cp.RangeStart.HasValue && cp.RangeEnd.HasValue)
            txtPageRange.Text = $"{cp.RangeStart} - {cp.RangeEnd}";
        txtAuthors.Text = cp.AuthorsFilter ?? "";

        // 같은 보드의 archive 데이터를 grid 로 복원
        try
        {
            var loaded = _archive.LoadPostsByBoard(cp.BoardName ?? "");
            RunWithoutUiUpdates(() =>
            {
                _posts.Clear();
                _columnFilters.Clear();
                foreach (var p in loaded) _posts.Add(p);
            });
            CollectionViewSource.GetDefaultView(grdPosts.ItemsSource).Refresh();
            AppendLog($"[RESUME] archive 에서 {loaded.Count}건 복원. RESUME 버튼으로 이어서 진행하세요.");

            // 복원된 진행 상태를 메모리에 반영 (종료 시 체크포인트 일관성 유지)
            _currentStage = "List";
            _currentBoard = cp.BoardName ?? "";
            _currentRangeStart = cp.RangeStart ?? 0;
            _currentRangeEnd = cp.RangeEnd ?? 0;
            _lastCompletedPage = cp.LastCompletedPage ?? 0;
            _currentAuthorsFilter = cp.AuthorsFilter ?? "";
        }
        catch (SqliteException ex)
        {
            AppendLog($"[WARN] archive 복원 실패: {ex.Message}");
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
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _autoSaveTimer?.Stop();
        SaveSettings();

        // 진행 중 크롤이 있으면 취소 신호만 보냄. 이미 _posts에 추가된 항목은 그대로 저장된다.
        try { _cts?.Cancel(); } catch { }

        if (_archive != null && _hasUnsavedWork && _posts.Count > 0)
        {
            try
            {
                var result = _archive.Save(_posts.ToList());
                WriteCheckpointForCurrentStage();
                _hasUnsavedWork = false;
                AppendLog($"[AUTOSAVE] 종료 시 자동 저장 완료 ({result.Inserted}건)");
            }
            catch (SqliteException) { /* 종료 차단하지 않음 */ }
        }
    }

    private void LoadSettings()
    {
        try
        {
            string configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crawler_config.txt");
            if (System.IO.File.Exists(configPath))
            {
                var lines = System.IO.File.ReadAllLines(configPath);
                if (lines.Length >= 2) { txtDateStart.Text = lines[0]; txtDateEnd.Text = lines[1]; }
            }
        }
        catch { }
    }

    private void SaveSettings()
    {
        try
        {
            string configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crawler_config.txt");
            System.IO.File.WriteAllLines(configPath, new[] { txtDateStart.Text, txtDateEnd.Text });
        }
        catch { }
    }

    // ==== Stage 1: 목록 수집 ====
    private async void btnStart_Click(object sender, RoutedEventArgs e)
    {
        // 미완료 체크포인트 경고
        var existing = SafeGetCheckpoint();
        if (existing != null && IsResumeAvailable(existing))
        {
            var conf = MessageBox.Show(
                "이전 작업이 미완료입니다 (RESUME 으로 이어갈 수 있음).\n새로 시작하면 진행도가 초기화됩니다. 계속?",
                "Confirm", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (conf != MessageBoxResult.OK) return;
        }

        _posts.Clear();
        _columnFilters.Clear();

        string board = (comboBoard.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "라커룸";
        var pageRange = txtPageRange.Text.Split('-').Select(s => s.Trim()).ToList();
        int startPage = 1, endPage = 1;
        if (pageRange.Count >= 1) int.TryParse(pageRange[0], out startPage);
        if (pageRange.Count >= 2) int.TryParse(pageRange[1], out endPage); else endPage = startPage;

        var authors = txtAuthors.Text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(a => a.Trim()).ToList();

        DateOnly? startDate = null, endDate = null;
        if (!string.IsNullOrWhiteSpace(txtDateStart.Text) && DateOnly.TryParse(txtDateStart.Text, out var sd)) startDate = sd;
        if (!string.IsNullOrWhiteSpace(txtDateEnd.Text)   && DateOnly.TryParse(txtDateEnd.Text,   out var ed)) endDate   = ed;

        await RunStage1Async(board, startPage, endPage, authors, startDate, endDate);
    }

    private async Task RunStage1Async(string board, int startPage, int endPage, List<string> authors,
        DateOnly? startDate = null, DateOnly? endDate = null)
    {
        btnStart.IsEnabled = false;
        btnResume.IsEnabled = false;
        btnFetchDetails.IsEnabled = false;
        btnStop.IsEnabled = true;

        _currentStage = "List";
        _currentBoard = board;
        _currentRangeStart = startPage;
        _currentRangeEnd = endPage;
        _lastCompletedPage = startPage - 1;
        _currentAuthorsFilter = string.Join(", ", authors);

        AppendLog($"[SYSTEM] Stage 1: {board} pages {startPage}-{endPage}" +
                  (startDate.HasValue ? $", 날짜: {startDate}~{endDate}" : ""));

        _cts = new CancellationTokenSource();
        IProgress<string> progress = new Progress<string>(AppendLog);

        try
        {
            var engine = new CrawlerEngine(progress);
            Action<Post> onPost = post => Dispatcher.Invoke(() => _posts.Add(post));
            Action<int> onPageComplete = page => Dispatcher.Invoke(() => _lastCompletedPage = page);
            await engine.RunListOnlyAsync(board, startPage, endPage, authors, startDate, endDate, _cts.Token, onPost, onPageComplete);

            AppendLog($"[SYSTEM] Stage 1 complete. {_posts.Count} posts. 헤더의 ▼ 버튼으로 필터 후 FETCH DETAILS.");
            if (_posts.Count > 0) _hasUnsavedWork = true;
            if (_lastCompletedPage >= endPage) _currentStage = "Idle";
        }
        catch (OperationCanceledException) { AppendLog("[SYSTEM] Cancelled."); }
        catch (Exception ex)
        {
            AppendLog($"[CRITICAL] {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
            _cts = null;
            UpdateFetchDetailsButtonState();
            RefreshResumeButtonState();
        }
    }

    // ==== Stage 2: 선택된 상세 수집 ====
    // 필터 ∩ IsSelected ∩ !DetailsFetched 교집합만 수집. 즉 사용자가 그리드에서 보는 그대로.
    private async void btnFetchDetails_Click(object sender, RoutedEventArgs e)
    {
        var view = CollectionViewSource.GetDefaultView(grdPosts.ItemsSource);
        var visible = new HashSet<Post>(view.Cast<Post>());
        var selected = _posts.Where(p => visible.Contains(p) && p.IsSelected && !p.DetailsFetched).ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show(
                "수집 대상이 없습니다.\n(필터에 보이는 행 중 체크되어 있고 아직 수집되지 않은 항목이 없음)",
                "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        bool filterActive = _columnFilters.Count > 0;
        var msg = $"{selected.Count}건의 상세 페이지를 수집합니다." +
                  (filterActive ? $"\n(현재 필터에 보이는 글만 대상)" : "") +
                  "\n계속?";
        var confirm = MessageBox.Show(msg, "Confirm", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        await RunStage2Async(selected);
    }

    private async Task RunStage2Async(IList<Post> selected)
    {
        btnStart.IsEnabled = false;
        btnResume.IsEnabled = false;
        btnFetchDetails.IsEnabled = false;
        btnStop.IsEnabled = true;

        _currentStage = "Detail";

        AppendLog($"[SYSTEM] Stage 2: fetching details for {selected.Count} selected posts.");

        _cts = new CancellationTokenSource();
        IProgress<string> progress = new Progress<string>(AppendLog);
        try
        {
            var engine = new CrawlerEngine(progress);
            await engine.FetchSelectedDetailsAsync(selected, _cts.Token);
            _hasUnsavedWork = true;
            var stillPending = _posts.Any(p => p.IsSelected && !p.DetailsFetched);
            if (!stillPending) _currentStage = "Idle";
            AppendLog("[SYSTEM] Stage 2 complete.");
        }
        catch (OperationCanceledException) { AppendLog("[SYSTEM] Cancelled."); }
        catch (Exception ex)
        {
            AppendLog($"[CRITICAL] {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
            _cts = null;
            UpdateFetchDetailsButtonState();
            RefreshResumeButtonState();
        }
    }

    private void btnStop_Click(object sender, RoutedEventArgs e) { _cts?.Cancel(); btnStop.IsEnabled = false; }

    // ==== 선택 도우미 ====
    private void btnSelAll_Click(object sender, RoutedEventArgs e)
        => RunWithoutUiUpdates(() => { foreach (var p in _posts) p.IsSelected = true; });
    private void btnSelNone_Click(object sender, RoutedEventArgs e)
        => RunWithoutUiUpdates(() => { foreach (var p in _posts) p.IsSelected = false; });
    private void btnSelFiltered_Click(object sender, RoutedEventArgs e)
    {
        var view = CollectionViewSource.GetDefaultView(grdPosts.ItemsSource);
        var visible = new HashSet<Post>(view.Cast<Post>());
        RunWithoutUiUpdates(() =>
        {
            foreach (var p in _posts) p.IsSelected = visible.Contains(p);
        });
    }

    private void btnClearFilters_Click(object sender, RoutedEventArgs e)
    {
        _columnFilters.Clear();
        CollectionViewSource.GetDefaultView(grdPosts.ItemsSource).Refresh();
        UpdateSelectionCount();
    }

    // ==== 필터 Predicate ====
    private bool FilterPredicate(object o)
    {
        var p = (Post)o;
        foreach (var (col, allowed) in _columnFilters)
        {
            if (allowed.Count == 0) return false;
            var val = GetColumnValue(p, col) ?? "";
            if (!allowed.Contains(val)) return false;
        }
        return true;
    }

    private static string? GetColumnValue(Post p, string col) => col switch
    {
        "CreatedAt" => p.CreatedAt,
        "Title"     => p.Title,
        "Author"    => p.Author,
        "AuthorId"  => p.AuthorId,
        "Views"     => p.Views,
        "Likes"     => p.Likes,
        _ => null,
    };

    // ==== ▼ 버튼 클릭: 체크박스 팝업 ====
    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        var col = (string)btn.Tag;

        // 현재 데이터에서 이 컬럼의 고유값 수집
        var allValues = _posts.Select(p => GetColumnValue(p, col) ?? "").Distinct().OrderBy(v => v).ToList();
        var currentFilter = _columnFilters.TryGetValue(col, out var f) ? f : new HashSet<string>(allValues);

        var popup = BuildFilterPopup(btn, col, allValues, currentFilter);
        popup.IsOpen = true;
    }

    private Popup BuildFilterPopup(Button target, string col, List<string> allValues, HashSet<string> currentFilter)
    {
        var popup = new Popup
        {
            PlacementTarget = target,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
        };

        // 내부 컨트롤들
        var searchBox = new TextBox
        {
            Margin = new Thickness(6),
            Padding = new Thickness(4),
            Background = Brushes.White,
        };
        var selectAll = new CheckBox
        {
            Content = "(모두 선택)",
            Margin = new Thickness(8, 4, 8, 6),
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black,
            IsChecked = currentFilter.Count == allValues.Count,
        };
        var listBox = new ListBox
        {
            MaxHeight = 300,
            Background = Brushes.White,
            BorderThickness = new Thickness(0),
        };

        void PopulateList(string? searchText)
        {
            listBox.Items.Clear();
            var filtered = string.IsNullOrWhiteSpace(searchText)
                ? allValues
                : allValues.Where(v => v.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            foreach (var v in filtered)
            {
                var cb = new CheckBox
                {
                    Content = string.IsNullOrEmpty(v) ? "(빈 값)" : v,
                    Tag = v,
                    IsChecked = currentFilter.Contains(v),
                    Margin = new Thickness(8, 2, 8, 2),
                    Foreground = Brushes.Black,
                };
                listBox.Items.Add(cb);
            }
        }
        PopulateList(null);
        searchBox.TextChanged += (s, e) => PopulateList(searchBox.Text);

        selectAll.Click += (s, e) =>
        {
            bool check = selectAll.IsChecked == true;
            foreach (var it in listBox.Items.Cast<CheckBox>()) it.IsChecked = check;
        };

        var okBtn = new Button { Content = "확인", Width = 60, Margin = new Thickness(4), Padding = new Thickness(4) };
        var cancelBtn = new Button { Content = "취소", Width = 60, Margin = new Thickness(4), Padding = new Thickness(4) };
        var clearBtn = new Button { Content = "해제", Width = 60, Margin = new Thickness(4), Padding = new Thickness(4) };

        okBtn.Click += (s, e) =>
        {
            // 체크된 값만 통과. 검색중이라 보이지 않는 값은 currentFilter 의 기존 상태 유지.
            var visibleValues = listBox.Items.Cast<CheckBox>().Select(cb => (string)cb.Tag).ToHashSet();
            var checkedSet = listBox.Items.Cast<CheckBox>()
                .Where(cb => cb.IsChecked == true)
                .Select(cb => (string)cb.Tag)
                .ToHashSet();
            // 화면에 안 보였던 값들: 기존 currentFilter 에 있으면 유지
            var preservedHidden = currentFilter.Where(v => !visibleValues.Contains(v));
            foreach (var v in preservedHidden) checkedSet.Add(v);

            if (checkedSet.Count == allValues.Count) _columnFilters.Remove(col);
            else _columnFilters[col] = checkedSet;

            CollectionViewSource.GetDefaultView(grdPosts.ItemsSource).Refresh();
            UpdateSelectionCount();
            UpdateFilterButtonStyle(target, _columnFilters.ContainsKey(col));
            popup.IsOpen = false;
        };
        cancelBtn.Click += (s, e) => popup.IsOpen = false;
        clearBtn.Click += (s, e) =>
        {
            _columnFilters.Remove(col);
            CollectionViewSource.GetDefaultView(grdPosts.ItemsSource).Refresh();
            UpdateSelectionCount();
            UpdateFilterButtonStyle(target, false);
            popup.IsOpen = false;
        };

        var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttonsPanel.Children.Add(clearBtn);
        buttonsPanel.Children.Add(cancelBtn);
        buttonsPanel.Children.Add(okBtn);

        var stack = new StackPanel { Background = Brushes.White };
        stack.Children.Add(searchBox);
        stack.Children.Add(selectAll);
        stack.Children.Add(new Separator());
        stack.Children.Add(listBox);
        stack.Children.Add(new Separator());
        stack.Children.Add(buttonsPanel);

        var border = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            BorderThickness = new Thickness(1),
            Child = stack,
            MinWidth = 240,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { ShadowDepth = 2, BlurRadius = 8, Opacity = 0.3 },
        };
        popup.Child = border;
        return popup;
    }

    private void UpdateFilterButtonStyle(Button btn, bool active)
    {
        btn.Background = active ? new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)) : new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
    }

    private void UpdateSelectionCount()
    {
        var sel = _posts.Count(p => p.IsSelected);
        var visible = CollectionViewSource.GetDefaultView(grdPosts.ItemsSource)?.Cast<object>().Count() ?? _posts.Count;
        txtSelCount.Text = $"{sel} selected / {_posts.Count} total";
        txtGridStatus.Text = $"{visible} visible (filtered)";
    }

    private void AppendLog(string msg)
    {
        Dispatcher.Invoke(() =>
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            txtLog.ScrollToEnd();
        });
    }

    private void OpenOutputFolder()
    {
        try { System.Diagnostics.Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory); }
        catch (Exception ex) { MessageBox.Show($"Could not open folder: {ex.Message}"); }
    }

    private void ExportToExcel()
    {
        if (!_posts.Any())
        {
            MessageBox.Show("No data to export.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"Soccerline_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
        };
        if (sfd.ShowDialog() != true) return;

        try
        {
            using var wb = new ClosedXML.Excel.XLWorkbook();

            var postsSheet = wb.AddWorksheet("Posts");
            string[] postHeaders = { "PostId", "Board", "CreatedAt", "Title", "Author", "AuthorId", "AuthorIp", "Views", "Likes", "Dislikes", "CommentCount", "Body", "Link" };
            for (int i = 0; i < postHeaders.Length; i++) postsSheet.Cell(1, i + 1).Value = postHeaders[i];

            int row = 2;
            foreach (var post in _posts.Where(p => p.IsSelected))
            {
                var postId = ExtractPostId(post.Link);
                postsSheet.Cell(row, 1).Value = postId;
                postsSheet.Cell(row, 2).Value = post.BoardName;
                postsSheet.Cell(row, 3).Value = post.CreatedAt;
                postsSheet.Cell(row, 4).Value = post.Title;
                postsSheet.Cell(row, 5).Value = post.Author;
                postsSheet.Cell(row, 6).Value = post.AuthorId;
                postsSheet.Cell(row, 7).Value = post.AuthorIp;
                postsSheet.Cell(row, 8).Value = post.Views;
                postsSheet.Cell(row, 9).Value = post.Likes;
                postsSheet.Cell(row, 10).Value = post.Dislikes;
                postsSheet.Cell(row, 11).Value = post.Comments.Count;
                postsSheet.Cell(row, 12).Value = post.Body;
                postsSheet.Cell(row, 13).Value = post.Link;
                row++;
            }
            StylizeSheet(postsSheet, postHeaders.Length);

            var commentsSheet = wb.AddWorksheet("Comments");
            string[] commentHeaders = { "PostId", "Order", "Nickname", "UserID", "AuthorIp", "CreatedAt", "Content" };
            for (int i = 0; i < commentHeaders.Length; i++) commentsSheet.Cell(1, i + 1).Value = commentHeaders[i];

            int crow = 2;
            foreach (var post in _posts.Where(p => p.IsSelected))
            {
                var postId = ExtractPostId(post.Link);
                int order = 1;
                foreach (var c in post.Comments)
                {
                    commentsSheet.Cell(crow, 1).Value = postId;
                    commentsSheet.Cell(crow, 2).Value = order++;
                    commentsSheet.Cell(crow, 3).Value = c.Nickname;
                    commentsSheet.Cell(crow, 4).Value = c.UserID;
                    commentsSheet.Cell(crow, 5).Value = c.AuthorIp;
                    commentsSheet.Cell(crow, 6).Value = c.CreatedAt;
                    commentsSheet.Cell(crow, 7).Value = c.Content;
                    crow++;
                }
            }
            StylizeSheet(commentsSheet, commentHeaders.Length);

            wb.SaveAs(sfd.FileName);
            MessageBox.Show($"Exported {_posts.Count(p => p.IsSelected)} posts to Excel.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}");
        }
    }

    private async void btnRunAnalysis_Click(object sender, RoutedEventArgs e)
    {
        if (!_posts.Any(p => p.IsSelected))
        {
            MessageBox.Show("분석할 데이터를 먼저 선택해주세요.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string tempDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp_export");
        System.IO.Directory.CreateDirectory(tempDir);
        string tempFilePath = System.IO.Path.Combine(tempDir, $"export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        
        AppendLog("분석을 위해 데이터를 임시 저장합니다...");
        try
        {
            using var wb = new ClosedXML.Excel.XLWorkbook();

            var postsSheet = wb.AddWorksheet("Posts");
            string[] postHeaders = { "PostId", "Board", "CreatedAt", "Title", "Author", "AuthorId", "AuthorIp", "Views", "Likes", "Dislikes", "CommentCount", "Body", "Link" };
            for (int i = 0; i < postHeaders.Length; i++) postsSheet.Cell(1, i + 1).Value = postHeaders[i];

            int row = 2;
            foreach (var post in _posts.Where(p => p.IsSelected))
            {
                var postId = ExtractPostId(post.Link);
                postsSheet.Cell(row, 1).Value = postId;
                postsSheet.Cell(row, 2).Value = post.BoardName;
                postsSheet.Cell(row, 3).Value = post.CreatedAt;
                postsSheet.Cell(row, 4).Value = post.Title;
                postsSheet.Cell(row, 5).Value = post.Author;
                postsSheet.Cell(row, 6).Value = post.AuthorId;
                postsSheet.Cell(row, 7).Value = post.AuthorIp;
                postsSheet.Cell(row, 8).Value = post.Views;
                postsSheet.Cell(row, 9).Value = post.Likes;
                postsSheet.Cell(row, 10).Value = post.Dislikes;
                postsSheet.Cell(row, 11).Value = post.Comments.Count;
                postsSheet.Cell(row, 12).Value = post.Body;
                postsSheet.Cell(row, 13).Value = post.Link;
                row++;
            }
            StylizeSheet(postsSheet, postHeaders.Length);

            var commentsSheet = wb.AddWorksheet("Comments");
            string[] commentHeaders = { "PostId", "Order", "Nickname", "UserID", "AuthorIp", "CreatedAt", "Content" };
            for (int i = 0; i < commentHeaders.Length; i++) commentsSheet.Cell(1, i + 1).Value = commentHeaders[i];

            int crow = 2;
            foreach (var post in _posts.Where(p => p.IsSelected))
            {
                int order = 1;
                foreach (var c in post.Comments)
                {
                    var postId = ExtractPostId(post.Link);
                    commentsSheet.Cell(crow, 1).Value = postId;
                    commentsSheet.Cell(crow, 2).Value = order++;
                    commentsSheet.Cell(crow, 3).Value = c.Nickname;
                    commentsSheet.Cell(crow, 4).Value = c.UserID;
                    commentsSheet.Cell(crow, 5).Value = c.AuthorIp;
                    commentsSheet.Cell(crow, 6).Value = c.CreatedAt;
                    commentsSheet.Cell(crow, 7).Value = c.Content;
                    crow++;
                }
            }
            StylizeSheet(commentsSheet, commentHeaders.Length);
            wb.SaveAs(tempFilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"엑셀 임시 저장 실패: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        AppendLog("백그라운드에서 파이썬 분석을 시작합니다...");
        
        string pythonExe = "python";
        string scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "analyze_soccerline_interactions.py");
        string outDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "analysis_outputs");
        
        if (!System.IO.File.Exists(scriptPath))
        {
            scriptPath = System.IO.Path.Combine(System.IO.Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName ?? AppDomain.CurrentDomain.BaseDirectory, "analyze_soccerline_interactions.py");
            if (!System.IO.File.Exists(scriptPath))
            {
                MessageBox.Show($"파이썬 스크립트를 찾을 수 없습니다: {scriptPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        btnRunAnalysis.IsEnabled = false;
        try
        {
            await Task.Run(() =>
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = $"\"{scriptPath}\" --files \"{tempFilePath}\" --output-dir \"{outDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                };

                using var process = System.Diagnostics.Process.Start(psi);
                if (process != null)
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    Dispatcher.Invoke(() =>
                    {
                        if (process.ExitCode == 0)
                        {
                            AppendLog("파이썬 분석 완료!");
                            AppendLog(output);
                            ShowAnalysisResults(outDir);
                        }
                        else
                        {
                            AppendLog($"파이썬 에러:\n{error}");
                            MessageBox.Show($"분석 중 오류 발생:\n{error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });
                }
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"분석 스크립트 실행 실패: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btnRunAnalysis.IsEnabled = true;
        }
    }

    private void ShowAnalysisResults(string outputDir)
    {
        var win = new Window
        {
            Title = "Analysis Results",
            Width = 1000,
            Height = 800,
            Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e)),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var stack = new StackPanel { Margin = new Thickness(20) };
        scroll.Content = stack;
        win.Content = scroll;

        string heatmapPath = System.IO.Path.Combine(outputDir, "interaction_heatmap.png");
        string networkPath = System.IO.Path.Combine(outputDir, "interaction_network.png");

        if (System.IO.File.Exists(heatmapPath))
        {
            stack.Children.Add(new TextBlock { Text = "User Interaction Heatmap", Foreground = Brushes.White, FontSize = 20, Margin = new Thickness(0, 0, 0, 10) });
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(heatmapPath);
                bmp.EndInit();
                stack.Children.Add(new Image { Source = bmp, Stretch = System.Windows.Media.Stretch.Uniform, Margin = new Thickness(0, 0, 0, 30) });
            }
            catch (Exception ex) { AppendLog("히트맵 로드 실패: " + ex.Message); }
        }

        if (System.IO.File.Exists(networkPath))
        {
            stack.Children.Add(new TextBlock { Text = "Network Graph", Foreground = Brushes.White, FontSize = 20, Margin = new Thickness(0, 0, 0, 10) });
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(networkPath);
                bmp.EndInit();
                stack.Children.Add(new Image { Source = bmp, Stretch = System.Windows.Media.Stretch.Uniform, Margin = new Thickness(0, 0, 0, 30) });
            }
            catch (Exception ex) { AppendLog("네트워크 그래프 로드 실패: " + ex.Message); }
        }
        
        if (stack.Children.Count == 0)
        {
            stack.Children.Add(new TextBlock { Text = "생성된 이미지 결과가 없습니다.", Foreground = Brushes.White, FontSize = 16 });
        }

        win.Show();
    }

    // ==== Archive: Save ====
    private void btnSaveArchive_Click(object sender, RoutedEventArgs e)
    {
        if (_archive == null)
        {
            MessageBox.Show("아카이브가 초기화되지 않았습니다.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        var candidates = _posts.ToList();
        if (candidates.Count == 0)
        {
            MessageBox.Show("저장할 항목이 없습니다.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            var result = _archive.Save(candidates);
            AppendLog($"[ARCHIVE] 저장/갱신 {result.Inserted}건, {result.SkippedDuplicate}건 스킵(중복), {result.SkippedNoPostId}건 스킵(PostId 없음)");
            WriteCheckpointForCurrentStage();
            _hasUnsavedWork = false;
            UpdateArchiveCount();
            RefreshResumeButtonState();
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
            RunWithoutUiUpdates(() =>
            {
                _posts.Clear();
                _columnFilters.Clear();
                foreach (var p in loaded) _posts.Add(p);
            });
            CollectionViewSource.GetDefaultView(grdPosts.ItemsSource).Refresh();
            AppendLog($"[ARCHIVE] {loaded.Count}건 로드됨");
            _hasUnsavedWork = false;
            _currentStage = "Idle";
            try { _archive.ClearCheckpoint(); } catch (SqliteException) { /* best-effort */ }
            RefreshResumeButtonState();
        }
        catch (SqliteException ex)
        {
            AppendLog($"[CRITICAL] Archive 로드 실패: {ex.Message}");
            MessageBox.Show($"아카이브 로드 실패: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==== Archive: Resume ====
    private async void btnResume_Click(object sender, RoutedEventArgs e)
    {
        if (_archive == null) return;
        var cp = SafeGetCheckpoint();
        if (cp == null || !IsResumeAvailable(cp))
        {
            MessageBox.Show("이어갈 작업이 없습니다.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            try { _archive.ClearCheckpoint(); } catch { }
            RefreshResumeButtonState();
            return;
        }

        if (cp.Stage == "Detail")
        {
            var pendingIds = _archive.GetPendingPostIds();
            if (pendingIds.Count == 0) { RefreshResumeButtonState(); return; }
            var conf = MessageBox.Show(
                $"상세 수집 미완료 {pendingIds.Count}건을 이어가시겠습니까?",
                "Resume Stage 2", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (conf != MessageBoxResult.OK) return;

            List<Post> loaded;
            try { loaded = _archive.LoadPostsByIds(pendingIds); }
            catch (SqliteException ex) { AppendLog($"[CRITICAL] Resume 로드 실패: {ex.Message}"); return; }

            RunWithoutUiUpdates(() =>
            {
                _posts.Clear();
                _columnFilters.Clear();
                foreach (var p in loaded)
                {
                    p.IsSelected = true;
                    p.DetailsFetched = false;  // 상세 재수집을 위해 강제 미완료 표시
                    p.Comments.Clear();        // 다시 받을 거라 비움
                    p.Body = "";
                    p.IsArchived = false;      // 다시 받을 것이므로 archive 동기화 상태 해제
                    _posts.Add(p);
                }
            });
            CollectionViewSource.GetDefaultView(grdPosts.ItemsSource).Refresh();
            AppendLog($"[RESUME] Stage 2 재개: {loaded.Count}건");

            await RunStage2Async(loaded);
        }
        else // List
        {
            var conf = MessageBox.Show(
                $"{cp.BoardName} 페이지 {cp.RangeStart}-{cp.RangeEnd}, 페이지 {cp.LastCompletedPage}까지 완료.\n페이지 {cp.LastCompletedPage + 1} 부터 이어가시겠습니까?",
                "Resume Stage 1", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (conf != MessageBoxResult.OK) return;

            // UI 컨피그 자동 채움
            if (!string.IsNullOrEmpty(cp.BoardName))
            {
                foreach (ComboBoxItem item in comboBoard.Items)
                {
                    if ((item.Content as string) == cp.BoardName) { comboBoard.SelectedItem = item; break; }
                }
            }
            txtPageRange.Text = $"{cp.RangeStart} - {cp.RangeEnd}";
            txtAuthors.Text = cp.AuthorsFilter ?? "";

            // 같은 보드의 archive 데이터를 grid 로 복원
            try
            {
                var loaded = _archive.LoadPostsByBoard(cp.BoardName ?? "");
                RunWithoutUiUpdates(() =>
                {
                    _posts.Clear();
                    _columnFilters.Clear();
                    foreach (var p in loaded) _posts.Add(p);
                });
                CollectionViewSource.GetDefaultView(grdPosts.ItemsSource).Refresh();
                AppendLog($"[RESUME] archive 에서 {loaded.Count}건 복원");
            }
            catch (SqliteException ex) { AppendLog($"[WARN] archive 복원 실패: {ex.Message}"); }

            var authors = (cp.AuthorsFilter ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim()).ToList();

            await RunStage1Async(cp.BoardName ?? "", cp.LastCompletedPage!.Value + 1, cp.RangeEnd!.Value, authors);
        }
    }

    // ==== Checkpoint helpers ====

    private CrawlCheckpointRow? SafeGetCheckpoint()
    {
        try { return _archive?.GetCheckpoint(); } catch { return null; }
    }

    private bool IsResumeAvailable(CrawlCheckpointRow cp)
    {
        if (cp.Stage == "List")
            return cp.LastCompletedPage.HasValue && cp.RangeEnd.HasValue && cp.LastCompletedPage < cp.RangeEnd;
        if (cp.Stage == "Detail")
        {
            try { return (_archive?.GetPendingPostIds().Count ?? 0) > 0; } catch { return false; }
        }
        return false;
    }

    private void RefreshResumeButtonState()
    {
        Dispatcher.Invoke(() =>
        {
            var cp = SafeGetCheckpoint();
            btnResume.IsEnabled = cp != null && IsResumeAvailable(cp);
        });
    }

    private void WriteCheckpointForCurrentStage()
    {
        if (_archive == null) return;
        try
        {
            if (_currentStage == "List")
            {
                _archive.WriteListCheckpoint(_currentBoard, _currentRangeStart, _currentRangeEnd, _lastCompletedPage, _currentAuthorsFilter);
                AppendLog($"[CHECKPOINT] List 저장: {_currentBoard} {_currentRangeStart}-{_currentRangeEnd}, 완료 페이지 {_lastCompletedPage}");
            }
            else if (_currentStage == "Detail")
            {
                var pendingIds = _posts
                    .Where(p => p.IsSelected && !p.DetailsFetched)
                    .Select(p => ExtractPostId(p.Link))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToList();
                _archive.WriteDetailCheckpoint(pendingIds);
                AppendLog($"[CHECKPOINT] Detail 저장: 미완료 {pendingIds.Count}건");
            }
            else
            {
                _archive.ClearCheckpoint();
            }
        }
        catch (SqliteException ex)
        {
            AppendLog($"[WARN] 체크포인트 작성 실패: {ex.Message}");
        }
    }

    // ==== 30분 자동저장 ====
    private void AutoSaveTick(object? sender, EventArgs e)
    {
        if (_archive == null) return;
        if (_posts.Count == 0) return;
        if (!_hasUnsavedWork) return;
        if (_cts != null)
        {
            AppendLog("[AUTOSAVE] skipped (crawl in progress)");
            return;
        }
        try
        {
            var result = _archive.Save(_posts.ToList());
            WriteCheckpointForCurrentStage();
            _hasUnsavedWork = false;
            UpdateArchiveCount();
            RefreshResumeButtonState();
            AppendLog($"[AUTOSAVE] 저장/갱신 {result.Inserted}건");
        }
        catch (SqliteException ex)
        {
            AppendLog($"[AUTOSAVE] 실패: {ex.Message}");
        }
    }

    private static string ExtractPostId(string link)
    {
        var m = System.Text.RegularExpressions.Regex.Match(link ?? "", @"/board/(\d+)");
        return m.Success ? m.Groups[1].Value : "";
    }

    private static void StylizeSheet(ClosedXML.Excel.IXLWorksheet sheet, int columnCount)
    {
        var header = sheet.Range(1, 1, 1, columnCount);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#E0E0E0");
        sheet.RangeUsed()?.SetAutoFilter();
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(1, 50);
    }
}
