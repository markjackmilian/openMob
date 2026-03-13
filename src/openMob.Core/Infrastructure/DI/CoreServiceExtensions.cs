using Microsoft.EntityFrameworkCore;
using openMob.Core.Data;
using openMob.Core.Infrastructure.Http;

namespace openMob.Core.Infrastructure.DI;

/// <summary>
/// Extension methods for registering openMob Core services into the DI container.
/// </summary>
public static class CoreServiceExtensions
{
    /// <summary>
    /// Registers all openMob Core services: EF Core DbContext, HTTP client, and infrastructure services.
    /// Call this from <c>MauiProgram.CreateMauiApp()</c>.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddOpenMobCore(this IServiceCollection services)
    {
        // EF Core — SQLite via IAppDataPathProvider (registered by the MAUI project)
        services.AddDbContext<AppDbContext>();

        // HTTP client factory
        services.AddHttpClient();

        // Typed API client
        services.AddTransient<IClaudeApiClient, ClaudeApiClient>();

        return services;
    }
}
