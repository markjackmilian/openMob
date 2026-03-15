using CommunityToolkit.Maui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using openMob.Core.Data;
using openMob.Core.Infrastructure.Security;
using openMob.Infrastructure;
using openMob.Infrastructure.Security;

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
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Register platform-specific path provider
        builder.Services.AddSingleton<IAppDataPathProvider, MauiAppDataPathProvider>();

        // Register platform-specific credential store (SecureStorage)
        builder.Services.AddSingleton<IServerCredentialStore, MauiServerCredentialStore>();

        // Register all Core services (EF Core, HTTP client, etc.)
        builder.Services.AddOpenMobCore();

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
