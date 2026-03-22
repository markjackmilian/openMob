using openMob.Views.Pages;
using openMob.Views.Popups;

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

        // Register popup routes (for Shell-based modal navigation)
        Routing.RegisterRoute("project-switcher", typeof(ProjectSwitcherSheet));
        Routing.RegisterRoute("agent-picker", typeof(AgentPickerSheet));
        Routing.RegisterRoute("model-picker", typeof(ModelPickerSheet));
        Routing.RegisterRoute("add-project", typeof(AddProjectSheet));
    }
}
