using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AIRoundTable.Models;
using AIRoundTable.Services;

namespace AIRoundTable.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private AiModelConfig?       _current;
    private bool                 _loading;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        RefreshList();
        if (_modelList.Items.Count > 0)
            _modelList.SelectedIndex = 0;
    }

    // ── 목록 새로 고침 ─────────────────────────────────────────────────────
    private void RefreshList()
    {
        _modelList.Items.Clear();
        foreach (var m in _settings.Models)
        {
            var item = new ListBoxItem
            {
                Content = BuildListItemContent(m),
                Tag     = m,
                Padding = new Thickness(12, 8, 12, 8),
            };
            _modelList.Items.Add(item);
        }
    }

    private static StackPanel BuildListItemContent(AiModelConfig m)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };

        var dot = new Border
        {
            Width        = 10, Height = 10,
            CornerRadius = new CornerRadius(5),
            Margin       = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        try { dot.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(m.Color)); }
        catch { dot.Background = Brushes.Gray; }

        var label = new TextBlock
        {
            Text     = m.Name,
            FontSize = 13,
            Foreground = m.Enabled ? Brushes.Black : Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var badge = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding      = new Thickness(6, 1, 6, 1),
            Margin       = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background   = m.Mode switch
            {
                AiMode.Api     => new SolidColorBrush(Color.FromRgb(0xD1, 0xFA, 0xE5)),
                AiMode.Browser => new SolidColorBrush(Color.FromRgb(0xDB, 0xEA, 0xFE)),
                _              => new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6)),
            },
            Child = new TextBlock
            {
                Text     = m.Mode.ToString(),
                FontSize = 10,
                Foreground = m.Mode switch
                {
                    AiMode.Api     => new SolidColorBrush(Color.FromRgb(0x06, 0x5F, 0x46)),
                    AiMode.Browser => new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8)),
                    _              => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
                },
            },
        };

        sp.Children.Add(dot);
        sp.Children.Add(label);
        sp.Children.Add(badge);
        return sp;
    }

    // ── 모델 선택 ──────────────────────────────────────────────────────────
    private void ModelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_modelList.SelectedItem is ListBoxItem { Tag: AiModelConfig config })
            LoadConfig(config);
    }

    private void LoadConfig(AiModelConfig config)
    {
        _loading = true;
        _current = config;
        _editPanel.IsEnabled = true;

        _nameBox.Text  = config.Name;
        _colorBox.Text = config.Color;
        _enabledCheck.IsChecked = config.Enabled;

        switch (config.Mode)
        {
            case AiMode.Api:     _modeApi.IsChecked     = true; break;
            case AiMode.Browser: _modeBrowser.IsChecked = true; break;
            default:             _modeManual.IsChecked  = true; break;
        }

        // API 필드
        _apiKeyBox.Password  = config.ApiKey      ?? string.Empty;
        _endpointBox.Text    = config.ApiEndpoint  ?? string.Empty;
        _modelIdBox.Text     = config.ModelId      ?? string.Empty;
        SelectApiTypeCombo(config.ApiType);

        // 브라우저 필드
        _siteUrlBox.Text     = config.SiteUrl          ?? string.Empty;
        _inputSelBox.Text    = config.InputSelector     ?? string.Empty;
        _submitSelBox.Text   = config.SubmitSelector    ?? string.Empty;
        _responseSelBox.Text = config.ResponseSelector  ?? string.Empty;

        UpdatePanelVisibility(config.Mode);
        _loading = false;
    }

    private void SelectApiTypeCombo(ApiType type)
    {
        foreach (ComboBoxItem item in _apiTypeBox.Items)
        {
            if (item.Tag is string t && t == type.ToString())
            {
                _apiTypeBox.SelectedItem = item;
                return;
            }
        }
        _apiTypeBox.SelectedIndex = 0;
    }

    private void UpdatePanelVisibility(AiMode mode)
    {
        _apiPanel.Visibility     = mode == AiMode.Api     ? Visibility.Visible : Visibility.Collapsed;
        _browserPanel.Visibility = mode == AiMode.Browser ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── 이벤트 ──────────────────────────────────────────────────────────────
    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || _current is null) return;
        if (sender is RadioButton { Tag: string tag } &&
            Enum.TryParse<AiMode>(tag, out var mode))
        {
            UpdatePanelVisibility(mode);
        }
    }

    private void ApiType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        bool isOpenAi = (_apiTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "OpenAiCompat";
        _endpointPanel.Visibility = isOpenAi ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(_colorBox.Text);
            _colorPreview.Background = new SolidColorBrush(color);
        }
        catch { /* 입력 중 무시 */ }
    }

    // ── 저장 ───────────────────────────────────────────────────────────────
    private void ApplyCurrentToConfig()
    {
        if (_current is null) return;

        _current.Name    = _nameBox.Text.Trim();
        _current.Color   = _colorBox.Text.Trim();
        _current.Enabled = _enabledCheck.IsChecked == true;

        if (_modeApi.IsChecked == true)
        {
            _current.Mode = AiMode.Api;

            var tag = (_apiTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "OpenAiCompat";
            _current.ApiType     = Enum.TryParse<ApiType>(tag, out var at) ? at : ApiType.OpenAiCompat;
            _current.ApiKey      = _apiKeyBox.Password;
            _current.ApiEndpoint = string.IsNullOrWhiteSpace(_endpointBox.Text) ? null : _endpointBox.Text.Trim();
            _current.ModelId     = string.IsNullOrWhiteSpace(_modelIdBox.Text)  ? null : _modelIdBox.Text.Trim();
        }
        else if (_modeBrowser.IsChecked == true)
        {
            _current.Mode             = AiMode.Browser;
            _current.SiteUrl          = _siteUrlBox.Text.Trim();
            _current.InputSelector    = _inputSelBox.Text.Trim();
            _current.SubmitSelector   = _submitSelBox.Text.Trim();
            _current.ResponseSelector = _responseSelBox.Text.Trim();
        }
        else
        {
            _current.Mode = AiMode.Manual;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ApplyCurrentToConfig();
        _settings.Save();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => DialogResult = false;

    // ── 추가 / 삭제 ────────────────────────────────────────────────────────
    private void AddModel_Click(object sender, RoutedEventArgs e)
    {
        var config = new AiModelConfig { Name = "새 모델", Color = "#6B7280" };
        _settings.Models.Add(config);
        RefreshList();
        _modelList.SelectedIndex = _modelList.Items.Count - 1;
    }

    private void DeleteModel_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        if (MessageBox.Show($"'{_current.Name}' 모델을 삭제하시겠소?",
                "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _settings.Models.Remove(_current);
        _current = null;
        _editPanel.IsEnabled = false;
        RefreshList();
    }
}
