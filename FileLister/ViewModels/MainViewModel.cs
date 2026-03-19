using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using FileLister.Models;
using FileLister.Services;
using MessageBox = System.Windows.MessageBox;
using Clipboard = System.Windows.Clipboard;

namespace FileLister.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly Action _closeAction;
        private readonly FileScanService _scanService = new FileScanService();
        private AppSettings _settings;

        public ObservableCollection<FileItem> Items { get; } = new ObservableCollection<FileItem>();
        public ICollectionView FilteredItems { get; }
        public ObservableCollection<string> CategoryOptions { get; } = new ObservableCollection<string>();

        // [기능3] 복수 폴더
        public ObservableCollection<string> RootFolders { get; } = new ObservableCollection<string>();

        private string? _selectedFolder;
        public string? SelectedFolder
        {
            get => _selectedFolder;
            set { if (_selectedFolder != value) { _selectedFolder = value; OnPropertyChanged(nameof(SelectedFolder)); } }
        }

        private bool _includeSubfolders = true;
        public bool IncludeSubfolders
        {
            get => _includeSubfolders;
            set { if (_includeSubfolders != value) { _includeSubfolders = value; OnPropertyChanged(nameof(IncludeSubfolders)); } }
        }

        // [기능1] 숨김/시스템 파일 제외
        private bool _excludeHiddenFiles;
        public bool ExcludeHiddenFiles
        {
            get => _excludeHiddenFiles;
            set { if (_excludeHiddenFiles != value) { _excludeHiddenFiles = value; OnPropertyChanged(nameof(ExcludeHiddenFiles)); } }
        }

        private bool _excludeSystemFiles = true;
        public bool ExcludeSystemFiles
        {
            get => _excludeSystemFiles;
            set { if (_excludeSystemFiles != value) { _excludeSystemFiles = value; OnPropertyChanged(nameof(ExcludeSystemFiles)); } }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                    FilteredItems.Refresh();
                }
            }
        }

        private string _selectedCategory = "전체";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory != value)
                {
                    _selectedCategory = value;
                    OnPropertyChanged(nameof(SelectedCategory));
                    FilteredItems.Refresh();
                }
            }
        }

        // [기능4] 날짜 범위 필터
        private DateTime? _filterDateFrom;
        public DateTime? FilterDateFrom
        {
            get => _filterDateFrom;
            set
            {
                if (_filterDateFrom != value)
                {
                    _filterDateFrom = value;
                    OnPropertyChanged(nameof(FilterDateFrom));
                    FilteredItems.Refresh();
                }
            }
        }

        private DateTime? _filterDateTo;
        public DateTime? FilterDateTo
        {
            get => _filterDateTo;
            set
            {
                if (_filterDateTo != value)
                {
                    _filterDateTo = value;
                    OnPropertyChanged(nameof(FilterDateTo));
                    FilteredItems.Refresh();
                }
            }
        }

        // [기능4] 크기 범위 필터 (KB 단위 입력, bytes로 캐싱)
        private string _filterSizeMinKb = "";
        private long? _minBytes;
        public string FilterSizeMinKb
        {
            get => _filterSizeMinKb;
            set
            {
                if (_filterSizeMinKb != value)
                {
                    _filterSizeMinKb = value;
                    _minBytes = long.TryParse(value, out var kb) ? kb * 1024 : null;
                    OnPropertyChanged(nameof(FilterSizeMinKb));
                    FilteredItems.Refresh();
                }
            }
        }

        private string _filterSizeMaxKb = "";
        private long? _maxBytes;
        public string FilterSizeMaxKb
        {
            get => _filterSizeMaxKb;
            set
            {
                if (_filterSizeMaxKb != value)
                {
                    _filterSizeMaxKb = value;
                    _maxBytes = long.TryParse(value, out var kb) ? kb * 1024 : null;
                    OnPropertyChanged(nameof(FilterSizeMaxKb));
                    FilteredItems.Refresh();
                }
            }
        }

        // [기능5] 진행률
        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            set { if (_isScanning != value) { _isScanning = value; OnPropertyChanged(nameof(IsScanning)); } }
        }

        private int _scannedCount;
        public int ScannedCount
        {
            get => _scannedCount;
            set { if (_scannedCount != value) { _scannedCount = value; OnPropertyChanged(nameof(ScannedCount)); } }
        }

        private string _statusText = "대기";
        public string StatusText
        {
            get => _statusText;
            set { if (_statusText != value) { _statusText = value; OnPropertyChanged(nameof(StatusText)); } }
        }

        public ICommand AddFolderCommand { get; }
        public ICommand RemoveFolderCommand { get; }
        public ICommand ScanCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand CopyPathsCommand { get; }
        public ICommand ResetFilterCommand { get; }
        public ICommand CloseCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainViewModel(Action closeAction)
        {
            _closeAction = closeAction;
            _settings = AppSettings.Load();

            // 하위 호환: 구버전 LastFolder → LastFolders 마이그레이션
            if (_settings.LastFolders.Count == 0 && _settings.LastFolder != null)
                _settings.LastFolders.Add(_settings.LastFolder);

            foreach (var folder in _settings.LastFolders)
                RootFolders.Add(folder);

            IncludeSubfolders = _settings.IncludeSubfolders;
            ExcludeHiddenFiles = _settings.ExcludeHiddenFiles;
            ExcludeSystemFiles = _settings.ExcludeSystemFiles;

            FilteredItems = CollectionViewSource.GetDefaultView(Items);
            FilteredItems.Filter = FilterPredicate;

            CategoryOptions.Add("전체");
            CategoryOptions.Add("문서");
            CategoryOptions.Add("이미지");
            CategoryOptions.Add("압축");
            CategoryOptions.Add("코드/설정");
            CategoryOptions.Add("미디어");
            CategoryOptions.Add("실행파일");
            CategoryOptions.Add("기타");

            AddFolderCommand = new RelayCommand(AddFolder);
            RemoveFolderCommand = new RelayCommand(RemoveFolder, () => SelectedFolder != null);
            ScanCommand = new AsyncRelayCommand(ScanAsync);
            ExportCsvCommand = new RelayCommand(ExportCsv, () => Items.Count > 0);
            CopyPathsCommand = new RelayCommand(CopyPaths, () => Items.Count > 0);
            ResetFilterCommand = new RelayCommand(ResetFilter);
            CloseCommand = new RelayCommand(() => _closeAction());
        }

        private void AddFolder()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (!RootFolders.Contains(dialog.SelectedPath))
                    RootFolders.Add(dialog.SelectedPath);
            }
        }

        private void RemoveFolder()
        {
            if (SelectedFolder != null)
                RootFolders.Remove(SelectedFolder);
        }

        private async Task ScanAsync()
        {
            if (RootFolders.Count == 0)
            {
                MessageBox.Show("대상 폴더를 하나 이상 추가하세요.");
                return;
            }

            IsScanning = true;
            ScannedCount = 0;
            StatusText = "스캔 준비 중...";
            Items.Clear();

            try
            {
                // UI 속성을 Task.Run 전에 로컬 변수로 캡처
                var folders = RootFolders.ToList();
                var includeSub = IncludeSubfolders;
                var excludeHidden = ExcludeHiddenFiles;
                var excludeSystem = ExcludeSystemFiles;

                // Progress<T>는 생성 시점 SynchronizationContext(UI 스레드)에서 콜백 실행
                var progress = new Progress<int>(count =>
                {
                    ScannedCount = count;
                    StatusText = $"스캔 중... {count:N0}개 발견";
                });

                var files = await Task.Run(() =>
                    _scanService.ScanFolders(folders, includeSub, excludeHidden, excludeSystem, progress));

                foreach (var file in files)
                    Items.Add(file);

                StatusText = $"완료: {Items.Count:N0}개 파일";

                _settings.LastFolders = RootFolders.ToList();
                _settings.IncludeSubfolders = IncludeSubfolders;
                _settings.ExcludeHiddenFiles = ExcludeHiddenFiles;
                _settings.ExcludeSystemFiles = ExcludeSystemFiles;
                _settings.Save();
            }
            catch (Exception ex)
            {
                StatusText = "오류 발생";
                MessageBox.Show($"오류: {ex.Message}");
            }
            finally
            {
                IsScanning = false;
            }
        }

        private void ExportCsv()
        {
            var dialog = new System.Windows.Forms.SaveFileDialog
            {
                FileName = $"파일목록_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                Filter = "CSV 파일 (*.csv)|*.csv"
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            try
            {
                using var writer = new StreamWriter(dialog.FileName, false, new UTF8Encoding(true));
                writer.WriteLine("파일명,확장자,분류,크기(bytes),크기,생성일,수정일,폴더,전체경로");

                foreach (var item in FilteredItems.Cast<FileItem>())
                {
                    writer.WriteLine(string.Join(",",
                        EscapeCsv(item.FileName),
                        EscapeCsv(item.Extension),
                        EscapeCsv(item.Category),
                        item.SizeBytes,
                        EscapeCsv(item.SizeFormatted),
                        EscapeCsv(item.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss")),
                        EscapeCsv(item.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")),
                        EscapeCsv(item.DirectoryPath),
                        EscapeCsv(item.FullPath)));
                }

                MessageBox.Show($"CSV 파일이 저장되었습니다.\n(필터 적용 {FilteredItems.Cast<FileItem>().Count():N0}개)");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 실패: {ex.Message}");
            }
        }

        private static string EscapeCsv(string value) =>
            $"\"{value.Replace("\"", "\"\"")}\"";

        private void CopyPaths()
        {
            var paths = FilteredItems.Cast<FileItem>()
                .Select(f => f.FullPath)
                .ToArray();

            Clipboard.SetText(string.Join(Environment.NewLine, paths));
            MessageBox.Show($"{paths.Length:N0}개 경로가 클립보드에 복사되었습니다.");
        }

        private void ResetFilter()
        {
            SearchText = "";
            SelectedCategory = "전체";
            FilterDateFrom = null;
            FilterDateTo = null;
            FilterSizeMinKb = "";
            FilterSizeMaxKb = "";
        }

        private bool FilterPredicate(object obj)
        {
            if (obj is not FileItem item) return false;

            if (_selectedCategory != "전체" && item.Category != _selectedCategory)
                return false;

            // 파일명 + 폴더 경로 동시 검색
            if (!string.IsNullOrEmpty(_searchText) &&
                !item.FileName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) &&
                !item.DirectoryPath.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                return false;

            // [기능4] 수정일 범위
            if (_filterDateFrom.HasValue && item.LastWriteTime.Date < _filterDateFrom.Value.Date)
                return false;
            if (_filterDateTo.HasValue && item.LastWriteTime.Date > _filterDateTo.Value.Date)
                return false;

            // [기능4] 크기 범위 (setter에서 파싱 캐싱 → 호출마다 파싱 없음)
            if (_minBytes.HasValue && item.SizeBytes < _minBytes.Value)
                return false;
            if (_maxBytes.HasValue && item.SizeBytes > _maxBytes.Value)
                return false;

            return true;
        }

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
