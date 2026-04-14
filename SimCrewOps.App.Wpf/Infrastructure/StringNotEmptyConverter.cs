using System.Globalization;
using System.Windows.Data;

namespace SimCrewOps.App.Wpf.Infrastructure;

/// <summary>
/// Returns <c>true</c> when the bound string is non-null and non-empty,
/// <c>false</c> otherwise. Used to drive Visibility triggers in XAML.
/// </summary>
[ValueConversion(typeof(string), typeof(bool))]
public sealed class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrEmpty(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
