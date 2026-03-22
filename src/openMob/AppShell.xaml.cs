using openMob.Views.Pages;

namespace openMob;

/// <summary>Application Shell — navigation host with Flyout and route registration.</summary>
public partial class AppShell : Shell
{
    /// <summary>Initialises the Shell and registers all navigation routes.</summary>
    public AppShell()
    {
        InitializeComponent();

        // Register routes for pages not declared as ShellContent
        Routing.RegisterRoute("projects", typeof(ProjectsPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("server-detail", typeof(ServerDetailPage));

        // Popup routes removed — all popups are now presented via UXDivers IPopupService.Current
        // through MauiPopupService, not Shell navigation.
    }
}
