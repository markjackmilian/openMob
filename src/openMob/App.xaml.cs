using Microsoft.Maui.ApplicationModel;
using openMob.Core.Infrastructure.Localization;
using openMob.Core.Infrastructure.Settings;

namespace openMob;

/// <summary>Application entry point.</summary>
public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IThemeService _themeService;
    private readonly ILanguageService _languageService;

    /// <summary>Initialises the application.</summary>
    /// <param name="serviceProvider">The DI service provider.</param>
    /// <param name="themeService">Service used to read the persisted theme preference at startup.</param>
    /// <param name="languageService">Service used to read the persisted language preference at startup.</param>
    public App(IServiceProvider serviceProvider, IThemeService themeService, ILanguageService languageService)
    {
        _serviceProvider = serviceProvider;
        _themeService = themeService;
        _languageService = languageService;
        // InitializeComponent() must run BEFORE any MAUI Essentials API (Preferences, etc.)
        // is accessed. ApplyCulture reads Preferences.Default — calling it before
        // InitializeComponent() risks a crash on iOS if Essentials is not yet bootstrapped.
        InitializeComponent();

        // Register TablerIconsFont as a global resource in code-behind.
        // OnPlatform as a standalone ResourceDictionary entry in XAML crashes on iOS
        // with MAUI 10 (XamlParseException during inflation). Defining it here in C#
        // after InitializeComponent() avoids the XAML parser bug while making the
        // resource available to all 52 Label elements that reference it via
        // FontFamily="{StaticResource TablerIconsFont}".
        var tablerFont = DeviceInfo.Platform == DevicePlatform.Android
            ? "tabler-icons.ttf#tabler-icons"
            : "TablerIcons";
        Resources["TablerIconsFont"] = tablerFont;

        LocalizationHelper.ApplyCulture(_languageService.GetLanguageCode());
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Apply persisted theme before the window is created (REQ-005).
        // UserAppTheme is set here (on the App/Application instance directly) rather than via
        // Application.Current to avoid a null-reference during early startup.
        UserAppTheme = _themeService.GetTheme() switch
        {
            AppThemePreference.Light => AppTheme.Light,
            AppThemePreference.Dark  => AppTheme.Dark,
            _                        => AppTheme.Unspecified,
        };

        // AppShell must be resolved AFTER InitializeComponent() so that
        // Colors.xaml and Styles.xaml are loaded before Shell XAML parses
        // StaticResource references.
        var shell = _serviceProvider.GetRequiredService<AppShell>();
        return new Window(shell);
    }
}
