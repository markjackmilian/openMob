using CommunityToolkit.Maui.Core;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;
using openMob.Views.Popups;
using UXDivers.Popups.Maui;
using UXDivers.Popups.Services;
using Toast = CommunityToolkit.Maui.Alerts.Toast;

namespace openMob.Services;

/// <summary>
/// MAUI implementation of <see cref="IAppPopupService"/> using UXDivers Popups
/// for custom popup presentation and CommunityToolkit.Maui toasts.
/// Native <c>DisplayAlert</c> / <c>DisplayPrompt</c> / <c>DisplayActionSheet</c>
/// are retained for simple confirm, rename, and option-sheet dialogs where
/// UXDivers built-in popups do not support typed results.
/// </summary>
internal sealed class MauiPopupService : IAppPopupService
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>Initialises the popup service with the DI service provider.</summary>
    /// <param name="serviceProvider">
    /// The application service provider for resolving popup pages that require
    /// pre-push ViewModel initialisation (callbacks, data loading).
    /// </param>
    public MauiPopupService(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task<bool> ShowConfirmDeleteAsync(string title, string message, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Pragmatic decision: SimpleActionPopup does not extend PopupResultPage<T>,
        // so it cannot return a typed bool result. We keep DisplayAlertAsync for
        // confirmation dialogs until a custom PopupResultPage<bool> is warranted.
        var page = Shell.Current?.CurrentPage;
        if (page is null)
            return false;

        return await page.DisplayAlertAsync(title, message, "Delete", "Cancel");
    }

    /// <inheritdoc />
    public async Task<string?> ShowRenameAsync(string currentName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Pragmatic decision: FormPopup returns List<string?> which adds integration
        // complexity not justified for a simple single-field rename dialog.
        // We keep DisplayPromptAsync for rename dialogs.
        var page = Shell.Current?.CurrentPage;
        if (page is null)
            return null;

        return await page.DisplayPromptAsync("Rename", "Enter a new name:", initialValue: currentName, accept: "Save", cancel: "Cancel");
    }

    /// <inheritdoc />
    public async Task ShowToastAsync(string message, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var toast = Toast.Make(message, ToastDuration.Short, 14);
        await toast.Show(ct);
    }

    /// <inheritdoc />
    public async Task ShowErrorAsync(string title, string message, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var page = Shell.Current?.CurrentPage;
        if (page is null)
            return;

        await page.DisplayAlertAsync(title, message, "OK");
    }

    /// <inheritdoc />
    public async Task<string?> ShowOptionSheetAsync(string title, IReadOnlyList<string> options, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Pragmatic decision: OptionSheetPopup integration complexity is not justified
        // for a simple action sheet. We keep DisplayActionSheetAsync.
        var page = Shell.Current?.CurrentPage;
        if (page is null)
            return null;

        var result = await page.DisplayActionSheetAsync(title, "Cancel", null, options.ToArray());
        return result == "Cancel" ? null : result;
    }

    /// <inheritdoc />
    public async Task ShowModelPickerAsync(Action<string> onModelSelected, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Resolve from DI for pre-push ViewModel callback configuration
        var sheet = _serviceProvider.GetRequiredService<ModelPickerSheet>();

        // Set the callback on the ViewModel before presenting
        if (sheet.BindingContext is ModelPickerViewModel vm)
        {
            vm.OnModelSelected = onModelSelected;
        }

        // Present via UXDivers popup stack
        await IPopupService.Current.PushAsync(sheet);
    }

    /// <inheritdoc />
    public async Task ShowAgentPickerAsync(Action<string?> onAgentSelected, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Resolve from DI for pre-push ViewModel callback configuration
        var sheet = _serviceProvider.GetRequiredService<AgentPickerSheet>();

        // Set the callback on the ViewModel before presenting; ensure primary mode
        if (sheet.BindingContext is AgentPickerViewModel vm)
        {
            vm.OnAgentSelected = onAgentSelected;
            vm.PickerMode = PickerMode.Primary;
        }

        // Present via UXDivers popup stack
        await IPopupService.Current.PushAsync(sheet);
    }

    /// <inheritdoc />
    public async Task PushPopupAsync(object popup, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Push the popup instance onto the UXDivers popup stack.
        // The caller is responsible for providing a valid PopupPage instance.
        await IPopupService.Current.PushAsync((PopupPage)popup);
    }

    /// <inheritdoc />
    public async Task PopPopupAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Pop the topmost popup from the UXDivers popup stack
        await IPopupService.Current.PopAsync();
    }

    /// <inheritdoc />
    public async Task ShowContextSheetAsync(string projectId, string sessionId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var sheet = _serviceProvider.GetRequiredService<ContextSheet>();

        // Initialize the ViewModel with project preferences before presenting the sheet
        // ("initialize before push" pattern)
        if (sheet.BindingContext is ContextSheetViewModel vm)
        {
            await vm.InitializeAsync(projectId, sessionId, ct);
        }

        // Ensure PushAsync is called on the main thread.
        // InitializeAsync internally awaits services that use ConfigureAwait(false),
        // which can cause the continuation to resume on a thread pool thread.
        // IPopupService.Current.PushAsync requires the main thread on Android.
        // The cancellation check inside the lambda closes the narrow window between
        // InitializeAsync completing and PushAsync executing on the main thread.
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            return IPopupService.Current.PushAsync(sheet);
        });
    }

    /// <inheritdoc />
    public async Task ShowCommandPaletteAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var sheet = _serviceProvider.GetRequiredService<CommandPaletteSheet>();

        // Present via UXDivers popup stack
        await IPopupService.Current.PushAsync(sheet);
    }

    /// <inheritdoc />
    public async Task ShowSubagentPickerAsync(Action<string> onSubagentSelected, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var sheet = _serviceProvider.GetRequiredService<AgentPickerSheet>();

        if (sheet.BindingContext is AgentPickerViewModel vm)
        {
            vm.PickerMode = PickerMode.Subagent;
            // Wrap Action<string> into Action<string?> — the picker always passes a non-null name in subagent mode.
            vm.OnAgentSelected = agentName =>
            {
                if (agentName is not null)
                    onSubagentSelected(agentName);
            };
        }

        // Present via UXDivers popup stack
        await IPopupService.Current.PushAsync(sheet);
    }

    /// <inheritdoc />
    public async Task ShowProjectSwitcherAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var sheet = _serviceProvider.GetRequiredService<ProjectSwitcherSheet>();

        // "Initialize before push" pattern: load projects before presenting the sheet
        // so the list is populated when the popup appears.
        if (sheet.BindingContext is ProjectSwitcherViewModel vm)
        {
            await vm.LoadProjectsCommand.ExecuteAsync(null);
        }

        // Ensure PushAsync is called on the main thread.
        // LoadProjectsCommand.ExecuteAsync may resume on a thread pool thread.
        // The cancellation check inside the lambda closes the narrow window between
        // LoadProjectsCommand completing and PushAsync executing on the main thread.
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            return IPopupService.Current.PushAsync(sheet);
        });
    }

    /// <inheritdoc />
    public async Task ShowAddProjectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var sheet = _serviceProvider.GetRequiredService<AddProjectSheet>();

        // Present via UXDivers popup stack
        await IPopupService.Current.PushAsync(sheet);
    }
}
