using System;
using System.Globalization;
using System.Windows.Data;

namespace WorkMonitorWpf;

[ValueConversion(typeof(double), typeof(double))]
public sealed class RatioToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double ratio) return 0.0;
        double maxWidth = parameter is string s && double.TryParse(s, out double p) ? p : 200.0;
        return Math.Clamp(ratio / 100.0 * maxWidth, 0.0, maxWidth);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
