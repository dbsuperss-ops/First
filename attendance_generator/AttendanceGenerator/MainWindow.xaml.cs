using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AttendanceGenerator.Services;
using AttendanceGenerator.ViewModels;

namespace AttendanceGenerator;

public partial class MainWindow : Window
{
    // 앞쪽 고정 컬럼 수: 순번, 이름, 학년, 반, 참여요일
    private const int LeadingFixedCols = 5;
    // 뒤쪽 고정 컬럼 수: 총출석, 지각, 결석, 연락처, 비고
    private const int TrailingFixedCols = 5;

    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel(new FileDataService());
        DataContext = _vm;
        _vm.ClassDatesChanged += RebuildDateColumns;
    }

    /// <summary>ViewModel의 ClassDates 기준으로 동적 날짜 컬럼을 재구성한다.</summary>
    private void RebuildDateColumns()
    {
        // 기존 동적 컬럼 제거
        int totalFixed = LeadingFixedCols + TrailingFixedCols;
        while (AttendanceGrid.Columns.Count > totalFixed)
            AttendanceGrid.Columns.RemoveAt(LeadingFixedCols);

        // 날짜 컬럼 삽입
        for (int i = 0; i < _vm.ClassDates.Count; i++)
        {
            int idx = i; // 클로저 캡처
            var col = new DataGridTemplateColumn
            {
                Header = _vm.ClassDates[i].ToString("M/d"),
                Width = new DataGridLength(46),
                CellTemplate = CreateAttendanceCellTemplate(idx),
            };
            AttendanceGrid.Columns.Insert(LeadingFixedCols + i, col);
        }
    }

    private static DataTemplate CreateAttendanceCellTemplate(int index)
    {
        var factory = new FrameworkElementFactory(typeof(ComboBox));

        factory.SetBinding(
            ComboBox.SelectedItemProperty,
            new Binding($"Attendances[{index}].Status")
            {
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        factory.SetValue(ComboBox.ItemsSourceProperty, new[] { "", "○", "△", "×" });
        factory.SetValue(ComboBox.BorderThicknessProperty, new Thickness(0));
        factory.SetValue(ComboBox.BackgroundProperty, Brushes.Transparent);
        factory.SetValue(ComboBox.HorizontalContentAlignmentProperty, HorizontalAlignment.Center);
        factory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        factory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
        factory.SetValue(ComboBox.PaddingProperty, new Thickness(2, 0, 2, 0));
        factory.SetValue(ComboBox.FontSizeProperty, 14.0);

        return new DataTemplate { VisualTree = factory };
    }
}
