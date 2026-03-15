using Microsoft.EntityFrameworkCore;
using openMob.Core.Data;
using openMob.Core.Data.Repositories;
using openMob.Core.Infrastructure.Discovery;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Core.Infrastructure.DI;

/// <summary>
/// Extension methods for registering openMob Core services into the DI container.
/// </summary>
public static class CoreServiceExtensions
{
    /// <summary>
    /// Registers all openMob Core services: EF Core DbContext, HTTP client, infrastructure services,
    /// business services, and ViewModels.
    /// Call this from <c>MauiProgram.CreateMauiApp()</c>.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddOpenMobCore(this IServiceCollection services)
    {
        // EF Core — SQLite via IAppDataPathProvider (registered by the MAUI project)
        services.AddDbContext<AppDbContext>();

        // HTTP client factory (base registration)
        services.AddHttpClient();

        // Named HTTP client for opencode API calls (base address resolved at runtime)
        services.AddHttpClient("opencode");

        // Named HTTP client for mDNS health probe — short 5-second timeout pre-configured
        // at registration so ValidateServerAsync never mutates a factory-managed client post-creation.
        services.AddHttpClient("discovery-probe", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        // Typed API client (legacy Claude client)
        services.AddTransient<IClaudeApiClient, ClaudeApiClient>();

        // Server connection repository (scoped — follows DbContext lifetime)
        services.AddScoped<IServerConnectionRepository, ServerConnectionRepository>();

        // opencode connection manager (singleton — holds connection status)
        services.AddSingleton<IOpencodeConnectionManager, OpencodeConnectionManager>();

        // IOpencodeApiClient is registered as Transient: each consumer (ViewModel) owns its own
        // instance and its own IsWaitingForServer state. This is intentional — a ViewModel should
        // only observe the waiting state of its own requests, not requests from other ViewModels.
        // If a shared global waiting indicator is needed in the future, change to Singleton and
        // ensure thread safety.
        services.AddTransient<IOpencodeApiClient, OpencodeApiClient>();

        // mDNS discovery (singleton — stateless, safe to share)
        services.AddSingleton<IZeroconfResolver, ZeroconfResolverAdapter>();
        services.AddSingleton<IOpencodeDiscoveryService, OpencodeDiscoveryService>();

        // ─── Business services ────────────────────────────────────────────────
        // Navigation and popup services (INavigationService, IAppPopupService) are NOT
        // registered here — they have MAUI implementations registered by the MAUI project.
        services.AddTransient<IProjectService, ProjectService>();
        services.AddTransient<ISessionService, SessionService>();
        services.AddTransient<IAgentService, AgentService>();
        services.AddTransient<IProviderService, ProviderService>();

        // ─── ViewModels ───────────────────────────────────────────────────────
        services.AddTransient<SplashViewModel>();
        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<ProjectsViewModel>();
        services.AddTransient<ProjectDetailViewModel>();
        services.AddTransient<AddProjectViewModel>();
        services.AddTransient<ProjectSwitcherViewModel>();
        services.AddTransient<AgentPickerViewModel>();
        services.AddTransient<ModelPickerViewModel>();

        return services;
    }
}
