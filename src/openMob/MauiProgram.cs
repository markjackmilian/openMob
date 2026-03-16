using CommunityToolkit.Maui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using openMob.Core.Data;
using openMob.Core.Infrastructure.Security;
using openMob.Core.Infrastructure.Settings;
using openMob.Core.Services;
using openMob.Infrastructure;
using openMob.Infrastructure.Security;
using openMob.Infrastructure.Settings;
using openMob.Services;
using openMob.Views.Pages;
using openMob.Views.Popups;

namespace openMob;

/// <summary>MAUI application builder and DI composition root.</summary>
public static class MauiProgram
{
    /// <summary>Creates and configures the MAUI application.</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
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
                fonts.AddFont("MaterialSymbols-Outlined.ttf", "MaterialSymbols");
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

        // Register platform services (navigation, popups)
        builder.Services.AddSingleton<INavigationService, MauiNavigationService>();
        builder.Services.AddSingleton<IAppPopupService, MauiPopupService>();

        // Register all Core services (EF Core, HTTP client, etc.)
        builder.Services.AddOpenMobCore();

        // Register Shell for DI resolution
        builder.Services.AddTransient<AppShell>();

        // Register Pages as Transient
        builder.Services.AddTransient<SplashPage>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddTransient<ChatPage>();
        builder.Services.AddTransient<ProjectsPage>();
        builder.Services.AddTransient<ProjectDetailPage>();
        builder.Services.AddTransient<SettingsPage>();

        // Register Popups as Transient
        builder.Services.AddTransient<ProjectSwitcherSheet>();
        builder.Services.AddTransient<AgentPickerSheet>();
        builder.Services.AddTransient<ModelPickerSheet>();
        builder.Services.AddTransient<AddProjectSheet>();

        var app = builder.Build();

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
