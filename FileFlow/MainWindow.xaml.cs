using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace FileFlow
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<string, object> _pageCache = new();
        private bool _isNavigating;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => NavigateToPage("home");
        }

        private void NavMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isNavigating) return;
            if (sender is ListBox lb && lb.SelectedItem is ListBoxItem item)
            {
                string? tag = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag)) NavigateToPage(tag);
            }
        }

        private void NavFooter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isNavigating) return;
            if (sender is ListBox lb && lb.SelectedItem is ListBoxItem item)
            {
                string? tag = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag)) NavigateToPage(tag);
            }
        }

        public void NavigateToPage(string page)
        {
            if (_isNavigating) return;
            _isNavigating = true;

            SetMenuSelection(NavMenu, page);
            SetMenuSelection(NavFooter, page);

            if (!_pageCache.ContainsKey(page))
            {
                _pageCache[page] = page switch
                {
                    "home" => new Pages.HomePage(),
                    "classify" => new Pages.ClassifyPage(),
                    "scenario" => new Pages.ScenarioPage(),
                    "duplicate" => new Pages.DuplicatePage(),
                    "log" => new Pages.LogPage(),
                    "statistics" => new Pages.StatisticsPage(),
                    "settings" => new Pages.SettingsPage(),
                    _ => new Pages.HomePage()
                };
            }
            if (_pageCache[page] is Page p)
            {
                if (p is IRefreshable r) r.Refresh();
                RootFrame.Navigate(p);
            }
            else RootFrame.Navigate(new Pages.HomePage());

            _isNavigating = false;
        }

        private void SetMenuSelection(ListBox lb, string page)
        {
            if (lb == null) return;
            foreach (var item in lb.Items)
            {
                if (item is ListBoxItem lbi && lbi.Tag?.ToString() == page)
                {
                    lbi.IsSelected = true;
                    return;
                }
            }
            lb.SelectedItem = null;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Services.WatcherService.StopAll();
        }
    }
}
