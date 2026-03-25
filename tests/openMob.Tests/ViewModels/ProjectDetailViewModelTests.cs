using FluentAssertions;
using NSubstitute;
using openMob.Core.Data.Entities;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;
using Xunit;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ProjectDetailViewModel"/>.
/// Covers initialization, partial failure handling, model override updates, and reset behaviour.
/// </summary>
public sealed class ProjectDetailViewModelTests
{
    private readonly IProjectService _projectService;
    private readonly IOpencodeApiClient _apiClient;
    private readonly IProjectPreferenceService _preferenceService;
    private readonly IAppPopupService _popupService;
    private readonly IDispatcherService _dispatcher;
    private readonly ProjectDetailViewModel _sut;

    public ProjectDetailViewModelTests()
    {
        _projectService = Substitute.For<IProjectService>();
        _apiClient = Substitute.For<IOpencodeApiClient>();
        _preferenceService = Substitute.For<IProjectPreferenceService>();
        _popupService = Substitute.For<IAppPopupService>();
        _dispatcher = Substitute.For<IDispatcherService>();

        _dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(ci => ci.Arg<Action>()());

        _sut = new ProjectDetailViewModel(
            _projectService,
            _apiClient,
            _preferenceService,
            _popupService,
            _dispatcher);
    }

    /// <summary>Builds a sample project DTO for tests.</summary>
    private static ProjectDto BuildProject(string id = "proj-1", string worktree = "/home/user/my-project")
        => new(
            Id: id,
            Worktree: worktree,
            VcsDir: null,
            Vcs: "git",
            Time: new ProjectTimeDto(Created: 1710000000000, Initialized: 1710000005000));

    /// <summary>Builds a sample path DTO for tests.</summary>
    private static PathDto BuildPath() => new(
        State: "/state",
        Config: "/config",
        Worktree: "/home/user/my-project",
        Directory: "/home/user/my-project");

    /// <summary>Builds a sample config DTO for tests.</summary>
    private static ConfigDto BuildConfig(string? model = "server/model") => new(
        Theme: null,
        LogLevel: null,
        Model: model,
        SmallModel: null,
        Username: null,
        Share: null,
        Autoupdate: null,
        Snapshot: null,
        Keybinds: null,
        Tui: null,
        Command: null,
        Agent: null,
        Provider: null,
        Mcp: null,
        Lsp: null,
        Formatter: null,
        Permission: null,
        Tools: null,
        Experimental: null,
        Watcher: null);

    /// <summary>Builds a sample project preference for tests.</summary>
    private static ProjectPreference BuildPreference(string? modelId = null)
        => new()
        {
            ProjectId = "proj-1",
            DefaultModelId = modelId,
            ThinkingLevel = ThinkingLevel.Medium,
            AutoAccept = false,
        };

    [Fact]
    public void Constructor_WhenProjectServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ProjectDetailViewModel(null!, _apiClient, _preferenceService, _popupService, _dispatcher);

        // Assert
        act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("projectService");
    }

    [Fact]
    public async Task InitializeAsync_WhenServicesReturnData_PopulatesDisplayProperties()
    {
        // Arrange
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>()).Returns(BuildProject());
        _apiClient.GetVcsInfoAsync(Arg.Any<CancellationToken>()).Returns(OpencodeResult<VcsInfoDto>.Success(new VcsInfoDto("main")));
        _apiClient.GetPathAsync(Arg.Any<CancellationToken>()).Returns(OpencodeResult<PathDto>.Success(BuildPath()));
        _apiClient.GetConfigAsync(Arg.Any<CancellationToken>()).Returns(OpencodeResult<ConfigDto>.Success(BuildConfig()));
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>()).Returns(BuildPreference());

        // Act
        await _sut.InitializeAsync("proj-1");

        // Assert
        _sut.IsLoading.Should().BeFalse();
        _sut.ProjectName.Should().Be("my-project");
        _sut.WorktreePath.Should().Be("/home/user/my-project");
        _sut.VcsType.Should().Be("git");
        _sut.GitBranch.Should().Be("main");
        _sut.WorkingDirectory.Should().Be("/home/user/my-project");
        _sut.ConfigPath.Should().Be("/config");
        _sut.CreatedAt.Should().NotBeNullOrWhiteSpace();
        _sut.InitializedAt.Should().NotBeNullOrWhiteSpace();
        _sut.EffectiveModelId.Should().Be("server/model");
        _sut.IsModelOverridden.Should().BeFalse();
        _sut.ModelSourceLabel.Should().Be("Server default");
    }

    [Fact]
    public async Task InitializeAsync_WhenVcsCallFails_SetsBranchToNullAndKeepsOtherValues()
    {
        // Arrange
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>()).Returns(BuildProject());
        _apiClient.GetVcsInfoAsync(Arg.Any<CancellationToken>()).Returns(OpencodeResult<VcsInfoDto>.Failure(new OpencodeApiError(ErrorKind.ServerError, "boom", 500, null)));
        _apiClient.GetPathAsync(Arg.Any<CancellationToken>()).Returns(OpencodeResult<PathDto>.Success(BuildPath()));
        _apiClient.GetConfigAsync(Arg.Any<CancellationToken>()).Returns(OpencodeResult<ConfigDto>.Success(BuildConfig()));
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>()).Returns(BuildPreference());

        // Act
        await _sut.InitializeAsync("proj-1");

        // Assert
        _sut.GitBranch.Should().BeNull();
        _sut.ProjectName.Should().Be("my-project");
        _sut.WorktreePath.Should().Be("/home/user/my-project");
        _sut.ConfigPath.Should().Be("/config");
    }

    [Fact]
    public async Task ChangeModelCommand_WhenModelSelected_PersistsOverrideAndUpdatesLabels()
    {
        // Arrange
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>()).Returns(BuildProject());
        _apiClient.GetVcsInfoAsync(Arg.Any<CancellationToken>()).Returns(OpencodeResult<VcsInfoDto>.Success(new VcsInfoDto("main")));
        _apiClient.GetPathAsync(Arg.Any<CancellationToken>()).Returns(OpencodeResult<PathDto>.Success(BuildPath()));
        _apiClient.GetConfigAsync(Arg.Any<CancellationToken>()).Returns(OpencodeResult<ConfigDto>.Success(BuildConfig()));
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>()).Returns(BuildPreference());
        _preferenceService.SetDefaultModelAsync("proj-1", "anthropic/claude", Arg.Any<CancellationToken>()).Returns(true);

        _popupService.ShowModelPickerAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                ci.Arg<Action<string>>()("anthropic/claude");
                return Task.CompletedTask;
            });

        await _sut.InitializeAsync("proj-1");

        // Act
        await _sut.ChangeModelCommand.ExecuteAsync(null);

        // Assert
        await _preferenceService.Received(1).SetDefaultModelAsync("proj-1", "anthropic/claude", Arg.Any<CancellationToken>());
        _sut.EffectiveModelId.Should().Be("anthropic/claude");
        _sut.IsModelOverridden.Should().BeTrue();
        _sut.ModelSourceLabel.Should().Be("Project override");
    }

    [Fact]
    public async Task ResetModelCommand_WhenOverrideExists_ClearsOverrideAndRestoresServerDefault()
    {
        // Arrange
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>()).Returns(BuildProject());
        _apiClient.GetVcsInfoAsync(Arg.Any<CancellationToken>()).Returns(OpencodeResult<VcsInfoDto>.Success(new VcsInfoDto("main")));
        _apiClient.GetPathAsync(Arg.Any<CancellationToken>()).Returns(OpencodeResult<PathDto>.Success(BuildPath()));
        _apiClient.GetConfigAsync(Arg.Any<CancellationToken>()).Returns(OpencodeResult<ConfigDto>.Success(BuildConfig()));
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>()).Returns(BuildPreference("local/model"));
        _preferenceService.ClearDefaultModelAsync("proj-1", Arg.Any<CancellationToken>()).Returns(true);

        await _sut.InitializeAsync("proj-1");

        // Act
        await _sut.ResetModelCommand.ExecuteAsync(null);

        // Assert
        await _preferenceService.Received(1).ClearDefaultModelAsync("proj-1", Arg.Any<CancellationToken>());
        _sut.EffectiveModelId.Should().Be("server/model");
        _sut.IsModelOverridden.Should().BeFalse();
        _sut.ModelSourceLabel.Should().Be("Server default");
    }
}
