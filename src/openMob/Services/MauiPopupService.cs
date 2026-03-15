using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using openMob.Core.Services;

namespace openMob.Services;

/// <summary>
/// MAUI implementation of <see cref="IAppPopupService"/> using native MAUI alerts
/// and CommunityToolkit.Maui toasts. UXDivers popup integration will be added
/// when the package is fully configured.
/// </summary>
internal sealed class MauiPopupService : IAppPopupService
{
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
    public Task PushPopupAsync(object popup, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        // UXDivers popup push will be integrated here.
        // For now, this is a no-op placeholder.
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PopPopupAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        // UXDivers popup pop will be integrated here.
        // For now, this is a no-op placeholder.
        return Task.CompletedTask;
    }
}
