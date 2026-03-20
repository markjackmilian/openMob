using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Infrastructure.Logging;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the AddProjectSheet popup. Collects project name and path
/// for creating a new project (REQ-027).
/// </summary>
/// <remarks>
/// The opencode server manages projects by directory. This ViewModel prepares
/// the data; the actual "add project" is done by opening a session in that directory.
/// </remarks>
public sealed partial class AddProjectViewModel : ObservableObject
{
    private readonly IAppPopupService _popupService;

    /// <summary>Initialises the AddProjectViewModel with required dependencies.</summary>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    public AddProjectViewModel(IAppPopupService popupService)
    {
        ArgumentNullException.ThrowIfNull(popupService);
        _popupService = popupService;
    }

    /// <summary>Gets or sets the project name entered by the user.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    [NotifyCanExecuteChangedFor(nameof(AddProjectCommand))]
    private string _projectName = string.Empty;

    /// <summary>Gets or sets the project path entered by the user.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    [NotifyCanExecuteChangedFor(nameof(AddProjectCommand))]
    private string _projectPath = string.Empty;

    /// <summary>Gets whether the "Add" button should be enabled (both name and path are non-empty).</summary>
    public bool CanAdd =>
        !string.IsNullOrWhiteSpace(ProjectName) && !string.IsNullOrWhiteSpace(ProjectPath);

    /// <summary>
    /// Adds the project. Currently closes the popup and shows a toast,
    /// as the opencode server manages projects by directory.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddProjectAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(AddProjectAsync), "start");
        try
        {
#endif
        await _popupService.PopPopupAsync(ct);
        await _popupService.ShowToastAsync($"Project '{ProjectName}' added.", ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(AddProjectAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(AddProjectAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>Cancels the add project operation and closes the popup.</summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task CancelAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(CancelAsync), "start");
        try
        {
#endif
        await _popupService.PopPopupAsync(ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(CancelAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(CancelAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }
}
