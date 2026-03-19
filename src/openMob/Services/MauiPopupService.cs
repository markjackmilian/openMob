using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using openMob.Core.Services;
using openMob.Core.ViewModels;
using openMob.Views.Popups;

namespace openMob.Services;

/// <summary>
/// MAUI implementation of <see cref="IAppPopupService"/> using native MAUI alerts
/// and CommunityToolkit.Maui toasts. UXDivers popup integration will be added
/// when the package is fully configured.
/// </summary>
internal sealed class MauiPopupService : IAppPopupService
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>Initialises the popup service with the DI service provider.</summary>
    /// <param name="serviceProvider">The application service provider for resolving popup pages.</param>
    public MauiPopupService(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task<bool> ShowConfirmDeleteAsync(string title, string message, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var page = Shell.Current?.CurrentPage;
        if (page is null)
            return false;

        return await page.DisplayAlertAsync(title, message, "Delete", "Cancel");
    }

    /// <inheritdoc />
    public async Task<string?> ShowRenameAsync(string currentName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

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

        var navigation = Shell.Current?.Navigation;
        if (navigation is null)
            return;

        // Resolve the ModelPickerSheet from DI (registered as Transient)
        var sheet = _serviceProvider.GetRequiredService<ModelPickerSheet>();

        // Set the callback on the ViewModel before presenting
        if (sheet.BindingContext is ModelPickerViewModel vm)
        {
            vm.OnModelSelected = onModelSelected;
        }

        // Present as a modal page (Shell.PresentationMode="ModalAnimated" on the page)
        await navigation.PushModalAsync(sheet, animated: true);
    }

    /// <inheritdoc />
    public Task PushPopupAsync(object popup, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        // UXDivers popup push will be integrated here.
        // For now, this is a no-op placeholder.
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task PopPopupAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var navigation = Shell.Current?.Navigation;
        if (navigation is null)
            return;

        // Pop the topmost modal page if one exists
        if (navigation.ModalStack.Count > 0)
        {
            await navigation.PopModalAsync(animated: true);
        }
    }

    /// <inheritdoc />
    public async Task ShowContextSheetAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var sheet = _serviceProvider.GetRequiredService<ContextSheet>();
        var page = GetCurrentPage();
        if (page is not null)
        {
            await page.Navigation.PushModalAsync(sheet, animated: true);
        }
    }

    /// <inheritdoc />
    public async Task ShowCommandPaletteAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var sheet = _serviceProvider.GetRequiredService<CommandPaletteSheet>();
        var page = GetCurrentPage();
        if (page is not null)
        {
            await page.Navigation.PushModalAsync(sheet, animated: true);
        }
    }

    /// <inheritdoc />
    public async Task ShowAgentPickerSubagentModeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var sheet = _serviceProvider.GetRequiredService<AgentPickerSheet>();
        if (sheet.BindingContext is AgentPickerViewModel vm)
        {
            vm.IsSubagentMode = true;
        }

        var page = GetCurrentPage();
        if (page is not null)
        {
            await page.Navigation.PushModalAsync(sheet, animated: true);
        }
    }

    /// <summary>Gets the current visible page from Shell navigation.</summary>
    /// <returns>The current page, or <c>null</c> if unavailable.</returns>
    private static Page? GetCurrentPage()
    {
        return Shell.Current?.CurrentPage;
    }
}
