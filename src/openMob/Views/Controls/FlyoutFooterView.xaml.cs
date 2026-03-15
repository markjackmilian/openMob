namespace openMob.Views.Controls;

/// <summary>Flyout footer with Projects, Settings navigation and app version.</summary>
public partial class FlyoutFooterView : ContentView
{
    /// <summary>Initialises the flyout footer view.</summary>
    public FlyoutFooterView()
    {
        InitializeComponent();
    }

    private async void OnProjectsTapped(object? sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = false;
        await Shell.Current.GoToAsync("projects");
    }

    private async void OnSettingsTapped(object? sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = false;
        await Shell.Current.GoToAsync("settings");
    }
}
