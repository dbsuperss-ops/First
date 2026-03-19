using Avalonia.Controls;
using DupeFinderPro.ViewModels;

namespace DupeFinderPro.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is DashboardViewModel vm)
                vm.Refresh();
        };
    }
}
