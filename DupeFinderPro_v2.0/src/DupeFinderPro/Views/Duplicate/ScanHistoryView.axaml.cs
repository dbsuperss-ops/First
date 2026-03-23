using Avalonia.Controls;
using DupeFinderPro.ViewModels.Duplicate;

namespace DupeFinderPro.Views.Duplicate;

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
