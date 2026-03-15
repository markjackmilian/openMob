using System.Windows.Input;
using openMob.Core.Models;

namespace openMob.Views.Controls;

/// <summary>
/// Conditional status banner displayed below the chat header.
/// Shows contextual messages for server offline, no provider, tool errors, etc.
/// Uses SetAppThemeColor for runtime theme-responsive background colors.
/// </summary>
public partial class StatusBannerView : ContentView
{
    /// <summary>Bindable property for the banner message.</summary>
    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(StatusBannerView), string.Empty);

    /// <summary>Bindable property for the optional action label.</summary>
    public static readonly BindableProperty ActionLabelProperty =
        BindableProperty.Create(nameof(ActionLabel), typeof(string), typeof(StatusBannerView));

    /// <summary>Bindable property for the action command.</summary>
    public static readonly BindableProperty ActionCommandProperty =
        BindableProperty.Create(nameof(ActionCommand), typeof(ICommand), typeof(StatusBannerView));

    /// <summary>Bindable property for the banner type.</summary>
    public static readonly BindableProperty BannerTypeProperty =
        BindableProperty.Create(nameof(BannerType), typeof(StatusBannerType), typeof(StatusBannerView),
            StatusBannerType.None, propertyChanged: OnBannerTypeChanged);

    /// <summary>Initialises the status banner view.</summary>
    public StatusBannerView()
    {
        InitializeComponent();
    }

    /// <summary>Gets or sets the banner message text.</summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>Gets or sets the optional action label.</summary>
    public string? ActionLabel
    {
        get => (string?)GetValue(ActionLabelProperty);
        set => SetValue(ActionLabelProperty, value);
    }

    /// <summary>Gets or sets the action command.</summary>
    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    /// <summary>Gets or sets the banner type for color theming.</summary>
    public StatusBannerType BannerType
    {
        get => (StatusBannerType)GetValue(BannerTypeProperty);
        set => SetValue(BannerTypeProperty, value);
    }

    private static void OnBannerTypeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is StatusBannerView view && newValue is StatusBannerType bannerType)
        {
            view.UpdateBannerAppearance(bannerType);
        }
    }

    private void UpdateBannerAppearance(StatusBannerType bannerType)
    {
        IsVisible = bannerType != StatusBannerType.None;

        // Resolve light and dark color keys based on banner type.
        var (lightKey, darkKey) = bannerType switch
        {
            StatusBannerType.ServerOffline => ("ColorWarningContainerLight", "ColorWarningContainerDark"),
            StatusBannerType.NoProvider => ("ColorWarningContainerLight", "ColorWarningContainerDark"),
            StatusBannerType.ToolError => ("ColorErrorContainerLight", "ColorErrorContainerDark"),
            StatusBannerType.ContextOverflow => ("ColorWarningContainerLight", "ColorWarningContainerDark"),
            _ => ("ColorSurfaceSecondaryLight", "ColorSurfaceSecondaryDark"),
        };

        // Use SetAppThemeColor so the banner responds to runtime theme changes.
        if (Application.Current?.Resources.TryGetValue(lightKey, out var lightColor) == true
            && Application.Current.Resources.TryGetValue(darkKey, out var darkColor) == true)
        {
            BannerContainer.SetAppThemeColor(
                Border.BackgroundColorProperty,
                (Color)lightColor,
                (Color)darkColor);
        }
    }
}
