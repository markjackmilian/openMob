using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Localization;
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

    /// <summary>Navigates back when the back button is tapped.</summary>
    private async void OnBackButtonTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>
    /// Handles the Appearance row tap: presents a native action sheet and applies the selected theme.
    /// </summary>
    private async void OnAppearanceTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var result = await DisplayActionSheetAsync(
                AppResources.Get("Appearance"),
                AppResources.Get("Cancel"),
                null,
                AppResources.Get("Light"), AppResources.Get("Dark"), AppResources.Get("FollowSystem"));

            var preference = result switch
            {
                var light when light == AppResources.Get("Light") => (AppThemePreference?)AppThemePreference.Light,
                var dark when dark == AppResources.Get("Dark") => (AppThemePreference?)AppThemePreference.Dark,
                var system when system == AppResources.Get("FollowSystem") => (AppThemePreference?)AppThemePreference.System,
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
