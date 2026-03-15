using System.Globalization;

namespace openMob.Converters;

/// <summary>
/// Converts a step number (int) to visibility (bool).
/// Returns <c>true</c> when the value equals the ConverterParameter.
/// Used by OnboardingPage to show/hide step content views.
/// </summary>
public sealed class StepToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int currentStep && parameter is string stepStr && int.TryParse(stepStr, out var targetStep))
        {
            return currentStep == targetStep;
        }

        return false;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
