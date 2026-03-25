using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using openMob.Core.Data;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Infrastructure.Security;
using openMob.Core.Infrastructure.Settings;
using openMob.Core.Infrastructure.Localization;
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
            // DSN is supplied at compile-time via AppSecrets.Local.cs (gitignored).
            // When AppSecrets.Local.cs is absent the DSN is empty and Sentry runs in no-op mode.
            // See AppSecrets.cs for the template to create AppSecrets.Local.cs locally.
            .UseSentry(options =>
            {
                options.Dsn = AppSecrets.SentryDsn;
                options.Debug = false;
                options.TracesSampleRate = 0.2;
                options.IsGlobalModeEnabled = true;
                options.AttachStacktrace = true;
                options.MinimumBreadcrumbLevel = LogLevel.Trace;
            })
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

        // Register MAUI-backed language service for persisted UI culture selection.
        builder.Services.AddSingleton<ILanguageService, MauiLanguageService>();

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
        builder.Services.AddTransientPopup<ProjectDetailSheet, ProjectDetailViewModel>();
        builder.Services.AddTransientPopup<MessageComposerSheet, MessageComposerViewModel>();
        builder.Services.AddTransientPopup<FilePickerSheet, FilePickerViewModel>();
        builder.Services.AddTransientPopup<FolderPickerSheet, FolderPickerViewModel>();

        var app = builder.Build();

        // Initialise the shared Markdown parser singleton for MarkdownView.
        // Done here (after Build) so the DI container is ready before any view is created.
        MarkdownView.SharedParser = app.Services.GetRequiredService<IMarkdownParser>();

        // Initialise the sqlite-net-pcl database on startup.
        // Creates the DB file and all tables (or adds missing columns via ALTER TABLE ADD COLUMN).
        // Wrapped in try-catch to prevent startup crash if initialisation fails.
        // Any failure is captured to Sentry so it is visible in production builds.
        try
        {
            SentryHelper.AddBreadcrumb("Database initialisation starting", "startup");
            var appDatabase = app.Services.GetRequiredService<IAppDatabase>();
            appDatabase.InitializeAsync().GetAwaiter().GetResult();
            SentryHelper.AddBreadcrumb("Database initialisation completed", "startup");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[openMob] Database initialisation failed: {ex.Message}");
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "MauiProgram.DatabaseInitialisation",
                ["exceptionType"] = ex.GetType().FullName ?? "Unknown",
                ["message"] = ex.Message,
            });
        }

        return app;
    }
}
