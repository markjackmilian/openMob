using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the server-side folder picker popup.
/// Browses folders only, starting from the server current directory, and returns
/// the selected folder path to the caller.
/// </summary>
public sealed partial class FolderPickerViewModel : ObservableObject
{
    private readonly IFileService _fileService;
    private readonly IAppPopupService _popupService;
    private readonly Stack<string?> _backStack = new();

    /// <summary>Initialises the folder picker ViewModel with required dependencies.</summary>
    /// <param name="fileService">Service for loading folder tree data from the server.</param>
    /// <param name="popupService">Service for closing the popup.</param>
    public FolderPickerViewModel(IFileService fileService, IAppPopupService popupService)
    {
        ArgumentNullException.ThrowIfNull(fileService);
        ArgumentNullException.ThrowIfNull(popupService);

        _fileService = fileService;
        _popupService = popupService;
    }

    /// <summary>Gets the current folder entries.</summary>
    public ObservableCollection<FileDto> Items { get; } = [];

    /// <summary>Gets or sets the current folder path being browsed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string? _currentPath;

    /// <summary>Gets or sets whether folders are loading.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private bool _isLoading;

    /// <summary>Gets or sets whether the folder list is empty.</summary>
    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>Gets or sets the inline error message.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string? _errorMessage;

    /// <summary>Gets or sets whether an error is currently shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private bool _hasError;

    /// <summary>Gets whether back navigation is available.</summary>
    public bool CanGoBack => _backStack.Count > 0 && !IsLoading;

    /// <summary>Gets whether the current folder can be confirmed.</summary>
    public bool CanConfirm => !IsLoading && !HasError && !string.IsNullOrWhiteSpace(CurrentPath);

    /// <summary>Callback invoked when the user confirms a folder selection.</summary>
    public Func<string, CancellationToken, Task>? OnFolderSelected { get; set; }

    /// <summary>
    /// Initialises the picker at the given start path and loads the first folder list.
    /// </summary>
    /// <param name="startPath">The server current directory.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task InitializeAsync(string? startPath, CancellationToken ct = default)
    {
        _backStack.Clear();
        CurrentPath = startPath;
        await LoadFoldersAsync(ct);
    }

    /// <summary>Loads the current folder entries.</summary>
    [RelayCommand]
    private async Task LoadFoldersAsync(CancellationToken ct)
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var result = await _fileService.GetFileTreeAsync(CurrentPath, ct);

            if (!result.IsSuccess)
            {
                Items.Clear();
                ErrorMessage = result.Error?.Message ?? "Failed to load folders.";
                HasError = true;
                return;
            }

            var folders = result.Value!
                .Where(item => item.Type == "directory" && !item.IsIgnored)
                .ToList();

            Items.Clear();
            foreach (var folder in folders)
                Items.Add(folder);

            IsEmpty = Items.Count == 0;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Items.Clear();
            ErrorMessage = $"Failed to load folders: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanConfirm));
            ConfirmCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Navigates into a folder.</summary>
    /// <param name="folder">The folder to open.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task SelectFolderAsync(FileDto folder, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(folder);

        if (folder.Type != "directory" || folder.IsIgnored || IsLoading)
            return;

        _backStack.Push(CurrentPath);
        CurrentPath = folder.RelativePath;
        await LoadFoldersAsync(ct);
    }

    /// <summary>Navigates back to the previous folder.</summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task BackAsync(CancellationToken ct)
    {
        if (_backStack.Count == 0 || IsLoading)
            return;

        CurrentPath = _backStack.Pop();
        await LoadFoldersAsync(ct);
    }

    /// <summary>Confirms the currently displayed folder.</summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync(CancellationToken ct)
    {
        var folderPath = CurrentPath;
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        await _popupService.PopPopupAsync(ct);

        var callback = OnFolderSelected;
        if (callback is not null)
            await callback(folderPath, ct);
    }

    /// <summary>Cancels the folder selection.</summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task CancelAsync(CancellationToken ct)
    {
        await _popupService.PopPopupAsync(ct);
    }
}
