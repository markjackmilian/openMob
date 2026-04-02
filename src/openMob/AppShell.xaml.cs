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

        // Push route for ServerManagementPage when navigated to from within a page
        // (e.g. from the reconnection modal). Using a separate route name avoids
        // conflicting with the ShellContent "server-management" root declaration.
        // The page's custom back button calls ".." which pops back to the caller.
        Routing.RegisterRoute("server-management-push", typeof(ServerManagementPage));

        // Popup routes removed — all popups are now presented via UXDivers IPopupService.Current
        // through MauiPopupService, not Shell navigation.
    }
}
