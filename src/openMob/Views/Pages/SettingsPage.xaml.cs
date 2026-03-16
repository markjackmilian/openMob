using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Infrastructure.Settings;
using openMob.Core.ViewModels;

namespace openMob.Views.Pages;

/// <summary>Settings page — allows the user to configure app preferences.</summary>
public partial class SettingsPage : ContentPage
{
    /// <summary>Gets the ViewModel bound to this page.</summary>
    internal SettingsViewModel ViewModel { get; }

    /// <summary>Initialises the settings page.</summary>
    /// <param name="viewModel">The settings ViewModel.</param>
    public SettingsPage(SettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        BindingContext = ViewModel;
        InitializeComponent();
    }

    /// <summary>
    /// Handles the Appearance row tap: presents a native action sheet and applies the selected theme.
    /// </summary>
    private async void OnAppearanceTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var result = await DisplayActionSheet(
                "Appearance",
                "Cancel",
                null,
                "Light", "Dark", "Follow System");

            var preference = result switch
            {
                "Light"         => (AppThemePreference?)AppThemePreference.Light,
                "Dark"          => (AppThemePreference?)AppThemePreference.Dark,
                "Follow System" => (AppThemePreference?)AppThemePreference.System,
                _               => null   // Cancel or dismissed
            };

            if (preference.HasValue)
                await ViewModel.ApplyThemeCommand.ExecuteAsync(preference.Value);
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "SettingsPage.OnAppearanceTapped",
            });
        }
    }
}
