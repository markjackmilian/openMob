using openMob.Core.Services;

namespace openMob.Views.Controls;

/// <summary>Flyout footer with Projects, Settings navigation and app version.</summary>
public partial class FlyoutFooterView : ContentView
{
    private INavigationService? _navigationService;

    /// <summary>Initialises the flyout footer view.</summary>
    public FlyoutFooterView()
    {
        InitializeComponent();
        VersionLabel.Text = $"v{AppInfo.Current.VersionString}";
    }

    /// <summary>Resolves the navigation service from DI on first use.</summary>
    private INavigationService NavigationService =>
        _navigationService ??= Application.Current?.Handler?.MauiContext?.Services
            .GetService<INavigationService>()
            ?? throw new InvalidOperationException("INavigationService not registered in DI.");

    private async void OnProjectsTapped(object? sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = false;
        await NavigationService.GoToAsync("projects");
    }

    private async void OnSettingsTapped(object? sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = false;
        await NavigationService.GoToAsync("settings");
    }
}
