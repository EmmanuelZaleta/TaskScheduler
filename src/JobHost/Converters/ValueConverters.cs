using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;

namespace YCC.SapAutomation.Host.Converters;

public sealed class BoolToRunningStatusConverter : IValueConverter
{
  public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    if (value is bool isRunning)
    {
      return isRunning
        ? new SolidColorBrush(Color.FromRgb(39, 174, 96))
        : new SolidColorBrush(Color.FromRgb(231, 76, 60));
    }
    return new SolidColorBrush(Color.FromRgb(149, 165, 166));
  }

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    => throw new NotImplementedException();
}

public sealed class DateTimeToDisplayConverter : IValueConverter
{
  public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    if (value is DateTime dt)
      return dt.ToString("HH:mm:ss", culture);
    return "N/A";
  }

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    => throw new NotImplementedException();
}

public sealed class BoolToVisibilityInvertedConverter : IValueConverter
{
  public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    if (value is bool boolValue)
    {
      return boolValue
        ? System.Windows.Visibility.Collapsed
        : System.Windows.Visibility.Visible;
    }
    return System.Windows.Visibility.Visible;
  }

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    => throw new NotImplementedException();
}
