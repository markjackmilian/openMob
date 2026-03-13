using CommunityToolkit.Maui;
using Microsoft.EntityFrameworkCore;
using openMob.Core.Data;
using openMob.Infrastructure;
using Sentry;

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
            .UseSentry(options =>
            {
                // DSN will be configured via app settings feature (user-secrets / SecureStorage).
                // Never hardcode the DSN here — this is a public repository.
                options.Dsn = string.Empty;
                options.Debug = false;
                options.TracesSampleRate = 1.0;
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register platform-specific path provider
        builder.Services.AddSingleton<IAppDataPathProvider, MauiAppDataPathProvider>();

        // Register all Core services (EF Core, HTTP client, etc.)
        builder.Services.AddOpenMobCore();

        var app = builder.Build();

        // Apply EF Core migrations on startup
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        return app;
    }
}
