using Avalonia.Controls;
using DupeFinderPro.ViewModels;

namespace DupeFinderPro.Views;

public partial class ScanHistoryView : UserControl
{
    public ScanHistoryView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ScanHistoryViewModel vm)
                vm.Refresh();
        };
    }
}
