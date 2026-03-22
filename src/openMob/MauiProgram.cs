using CommunityToolkit.Maui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using openMob.Core.Data;
using openMob.Core.Infrastructure.Security;
using openMob.Core.Infrastructure.Settings;
using openMob.Core.Services;
using openMob.Core.Services.Markdown;
using openMob.Core.ViewModels;
using openMob.Infrastructure;
using openMob.Infrastructure.Security;
using openMob.Infrastructure.Settings;
using openMob.Services;
using openMob.Views.Controls.Markdown;
using openMob.Views.Pages;
using openMob.Views.Popups;
using UXDivers.Popups.Maui;

namespace openMob;

/// <summary>MAUI application builder and DI composition root.</summary>
public static class MauiProgram
{
    /// <summary>Creates and configures the MAUI application.</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

#if DEBUG && ANDROID
        openMob.Core.Infrastructure.Logging.DebugLogger.WriteAction =
            (tag, msg) => Android.Util.Log.Debug(tag, msg);
#endif

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseUXDiversPopups()
            // .UseSentry(options =>
            // {
            //     // DSN will be configured via app settings feature (user-secrets / SecureStorage).
            //     // Never hardcode the DSN here — this is a public repository.
            //     options.Dsn = string.Empty;
            //     options.Debug = false;
            //     options.TracesSampleRate = 1.0;
            // })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Inter-Regular.ttf", "InterRegular");
                fonts.AddFont("Inter-Medium.ttf", "InterMedium");
                fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
                fonts.AddFont("Inter-Bold.ttf", "InterBold");
                fonts.AddFont("TablerIcons.ttf", "TablerIcons");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Register platform-specific path provider
        builder.Services.AddSingleton<IAppDataPathProvider, MauiAppDataPathProvider>();

        // Register platform-specific credential store (SecureStorage)
        builder.Services.AddSingleton<IServerCredentialStore, MauiServerCredentialStore>();

        // Register MAUI-backed settings service for opencode API client timeout
        builder.Services.AddSingleton<IOpencodeSettingsService, MauiOpencodeSettingsService>();

        // Register MAUI-backed theme service (REQ-003, REQ-004)
        builder.Services.AddSingleton<IThemeService, MauiThemeService>();

        // Register platform services (navigation, popups, dispatcher)
        builder.Services.AddSingleton<INavigationService, MauiNavigationService>();
        builder.Services.AddSingleton<IAppPopupService, MauiPopupService>();
        builder.Services.AddSingleton<IDispatcherService, MauiDispatcherService>();

        // Register all Core services (EF Core, HTTP client, etc.)
        builder.Services.AddOpenMobCore();

        // Register Shell for DI resolution
        builder.Services.AddTransient<AppShell>();

        // Register Pages as Transient
        builder.Services.AddTransient<SplashPage>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddTransient<ChatPage>();
        builder.Services.AddTransient<ProjectsPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<ServerManagementPage>();
        builder.Services.AddTransient<ServerDetailPage>();

        // Register Popups as Transient (UXDivers DI extension wires TPopup ↔ TViewModel)
        builder.Services.AddTransientPopup<ProjectSwitcherSheet, ProjectSwitcherViewModel>();
        builder.Services.AddTransientPopup<AgentPickerSheet, AgentPickerViewModel>();
        builder.Services.AddTransientPopup<ModelPickerSheet, ModelPickerViewModel>();
        builder.Services.AddTransientPopup<AddProjectSheet, AddProjectViewModel>();
        builder.Services.AddTransientPopup<ContextSheet, ContextSheetViewModel>();
        builder.Services.AddTransientPopup<CommandPaletteSheet, CommandPaletteViewModel>();

        var app = builder.Build();

        // Initialise the shared Markdown parser singleton for MarkdownView.
        // Done here (after Build) so the DI container is ready before any view is created.
        MarkdownView.SharedParser = app.Services.GetRequiredService<IMarkdownParser>();

        // Apply EF Core migrations on startup.
        // Wrapped in try-catch to prevent startup crash if migrations fail.
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[openMob] EF Core migration failed: {ex.Message}");
        }

        return app;
    }
}
