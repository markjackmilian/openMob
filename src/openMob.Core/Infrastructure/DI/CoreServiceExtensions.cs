using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using openMob.Core.Data;
using openMob.Core.Data.Repositories;
using openMob.Core.Infrastructure.Discovery;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Services;
using openMob.Core.Services.Markdown;
using openMob.Core.ViewModels;
using Polly;

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

        // Named HTTP client for opencode API calls — with full resilience pipeline
        // ADR: The resilience pipeline applies to ALL callers of the "opencode" named client,
        // including ISessionService, IProjectService, etc. This is intentional — all regular
        // HTTP calls benefit from retry, circuit breaker, and timeout protection.
        services.AddHttpClient("opencode")
            .AddResilienceHandler("opencode-resilience", builder =>
            {
                // Per-request timeout: 30 seconds
                builder.AddTimeout(TimeSpan.FromSeconds(30));

                // Retry: 3 attempts, exponential backoff with jitter, transient errors only
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromSeconds(1),
                    ShouldHandle = args => args.Outcome switch
                    {
                        { Exception: HttpRequestException } => PredicateResult.True(),
                        { Exception: TaskCanceledException } => PredicateResult.True(),
                        { Result.StatusCode: >= HttpStatusCode.InternalServerError } => PredicateResult.True(),
                        _ => PredicateResult.False(),
                    },
                });

                // Circuit breaker: open after 5 failures in 30s, half-open after 15s
                builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 5,
                    // FailureRatio = 1.0: breaker opens only when ALL requests in the sampling window fail.
                    // With MinimumThroughput = 5, this means 5 consecutive failures are required.
                    // Spec REQ-014 says "5 failures in 30s" — interpreted as 5/5 = 100% failure rate.
                    FailureRatio = 1.0,
                    BreakDuration = TimeSpan.FromSeconds(15),
                });
            });

        // Named HTTP client for SSE long-lived connections — no resilience pipeline, infinite timeout.
        // The resilience pipeline's 30-second timeout would terminate SSE connections prematurely.
        // ChatService manages its own reconnect logic with exponential backoff.
        services.AddHttpClient("opencode-sse", client =>
        {
            client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        });

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
        services.AddTransient<IProjectPreferenceService, ProjectPreferenceService>();

        // Chat service (singleton — maintains SSE connection state and IsConnected)
        services.AddSingleton<IChatService, ChatService>();

        // Markdown parsing (singleton — stateless pipeline, safe to share)
        services.AddSingleton<IMarkdownParser, MarkdigMarkdownParser>();

        // Command service (transient — each consumer owns its own cache lifecycle)
        services.AddTransient<ICommandService, CommandService>();

        // ─── ViewModels ───────────────────────────────────────────────────────
        services.AddTransient<SplashViewModel>();
        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<ProjectsViewModel>();
        services.AddTransient<ProjectDetailViewModel>();
        services.AddTransient<AddProjectViewModel>();
        services.AddTransient<ProjectSwitcherViewModel>();
        services.AddTransient<AgentPickerViewModel>();
        services.AddTransient<ModelPickerViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<ContextSheetViewModel>();
        services.AddTransient<CommandPaletteViewModel>();
        // FlyoutViewModel is Singleton — both FlyoutHeaderView and FlyoutContentView resolve
        // it via Application.Current?.Handler?.MauiContext?.Services.GetService<FlyoutViewModel>()
        // and must share the same instance for consistent binding and messenger subscriptions.
        services.AddSingleton<FlyoutViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ServerManagementViewModel>();
        services.AddTransient<ServerDetailViewModel>();

        return services;
    }
}
