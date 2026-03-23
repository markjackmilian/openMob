using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Services;
using System.Collections.ObjectModel;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the file picker popup.
/// Supports first-level tree browsing via <see cref="IFileService.GetFileTreeAsync"/>,
/// directory navigation with a back stack, and server-side search with 300 ms debounce
/// via <see cref="IFileService.FindFilesAsync"/>.
/// </summary>
public partial class FilePickerViewModel : ObservableObject, IDisposable
{
    private readonly IFileService _fileService;
    private readonly IAppPopupService _popupService;

    /// <summary>Cancellation source for in-flight tree load operations.</summary>
    private CancellationTokenSource? _loadCts;

    /// <summary>Cancellation source for in-flight search debounce and API calls.</summary>
    private CancellationTokenSource? _searchCts;

    /// <summary>Navigation history of directory paths (null represents root).</summary>
    private readonly Stack<string?> _backStack = new();

    /// <summary>Initialises the file picker ViewModel with required dependencies.</summary>
    /// <param name="fileService">Service for loading project files from the opencode server.</param>
    /// <param name="popupService">Service for popup operations (used to pop the sheet after selection).</param>
    public FilePickerViewModel(IFileService fileService, IAppPopupService popupService)
    {
        ArgumentNullException.ThrowIfNull(fileService);
        ArgumentNullException.ThrowIfNull(popupService);
        _fileService = fileService;
        _popupService = popupService;
    }

    /// <summary>The display collection of file and directory items for the current view.</summary>
    public ObservableCollection<FileDto> Items { get; } = [];

    /// <summary>Search text for server-side file search.</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>Indicates whether files are currently loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Indicates whether the item list is empty and not loading.</summary>
    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>Error message if file loading or search fails.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Indicates whether an error occurred.</summary>
    [ObservableProperty]
    private bool _hasError;

    /// <summary>The current directory path being browsed, or <c>null</c> for the project root.</summary>
    [ObservableProperty]
    private string? _currentPath;

    /// <summary>Indicates whether a search query is active (i.e. <see cref="SearchText"/> is non-empty).</summary>
    [ObservableProperty]
    private bool _isSearchActive;

    /// <summary>Indicates whether back navigation is available (back stack is non-empty and not searching).</summary>
    [ObservableProperty]
    private bool _canGoBack;

    /// <summary>
    /// When set, file selection invokes this callback instead of the default behavior.
    /// Used by the message composer for token insertion mode.
    /// </summary>
    public Action<string>? OnFileSelected { get; set; }

    /// <summary>
    /// Loads the first-level file tree at <see cref="CurrentPath"/>.
    /// Cancels any in-flight tree load before starting a new one.
    /// </summary>
    [RelayCommand]
    private async Task LoadFilesAsync(CancellationToken ct)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _loadCts.Token);

        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var result = await _fileService.GetFileTreeAsync(CurrentPath, linkedCts.Token);

            if (!result.IsSuccess)
            {
                Items.Clear();
                ErrorMessage = result.Error?.Message ?? "Failed to load files.";
                HasError = true;
                return;
            }

            Items.Clear();
            foreach (var file in result.Value!)
            {
                Items.Add(file);
            }

            IsEmpty = Items.Count == 0;
        }
        catch (OperationCanceledException)
        {
            // Cancelled — no action needed
        }
        catch (Exception ex)
        {
            // Catch-all for unexpected failures (e.g. JsonException when the server returns
            // HTML instead of JSON, HttpRequestException for network errors, etc.).
            // Without this block the exception propagates silently, leaving HasError = false
            // and the list empty with no feedback to the user.
            Items.Clear();
            ErrorMessage = $"Failed to load files: {ex.Message}";
            HasError = true;
        }
        finally
        {
            linkedCts.Dispose();
            IsLoading = false;
        }
    }

    /// <summary>
    /// Selects a file, invokes the callback, and pops the popup.
    /// In tree mode, tapping a directory navigates into it instead of selecting (REQ-005).
    /// In search mode, tapping a directory selects it normally (REQ-008).
    /// </summary>
    [RelayCommand]
    private async Task SelectFileAsync(FileDto file, CancellationToken ct)
    {
        // In tree mode, tapping a directory navigates into it instead of selecting (REQ-005)
        if (file.Type == "directory" && !IsSearchActive)
        {
            await NavigateToDirectoryAsync(file, ct);
            return;
        }

        OnFileSelected?.Invoke(file.RelativePath);
        await _popupService.PopPopupAsync(ct);
    }

    /// <summary>
    /// Navigates into a directory node. Pushes the current path onto the back stack,
    /// updates <see cref="CurrentPath"/>, and reloads the tree.
    /// Only operates when search is not active.
    /// </summary>
    [RelayCommand]
    private async Task NavigateToDirectoryAsync(FileDto directory, CancellationToken ct)
    {
        if (IsSearchActive)
            return;

        _backStack.Push(CurrentPath);
        CurrentPath = directory.RelativePath;
        CanGoBack = _backStack.Count > 0 && !IsSearchActive;

        await LoadFilesAsync(ct);
    }

    /// <summary>
    /// Navigates back to the previous directory by popping the back stack.
    /// Only enabled when <see cref="CanGoBack"/> is <c>true</c>.
    /// </summary>
    [RelayCommand]
    private async Task BackAsync(CancellationToken ct)
    {
        if (_backStack.Count == 0 || IsSearchActive)
            return;

        CurrentPath = _backStack.Pop();
        CanGoBack = _backStack.Count > 0 && !IsSearchActive;

        await LoadFilesAsync(ct);
    }

    /// <summary>
    /// Reacts to <see cref="SearchText"/> changes. Triggers debounced server-side search
    /// when text is non-empty, or reloads the tree at <see cref="CurrentPath"/> when cleared.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        IsSearchActive = !string.IsNullOrEmpty(value);
        CanGoBack = _backStack.Count > 0 && !IsSearchActive;

        // Cancel any pending debounce or in-flight search
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;

        if (string.IsNullOrEmpty(value))
        {
            // Search cleared — reload tree at current directory
            _ = LoadFilesCommand.ExecuteAsync(null);
        }
        else
        {
            // Start debounced server search
            _ = SearchAfterDelayAsync(value);
        }
    }

    /// <summary>
    /// Performs a server-side file search after a 300 ms debounce delay.
    /// Cancels any previous debounce or search before starting.
    /// </summary>
    /// <param name="query">The search query text.</param>
    private async Task SearchAfterDelayAsync(string query)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        try
        {
            await Task.Delay(300, ct);
            ct.ThrowIfCancellationRequested();

            // Cancel any in-flight tree load
            _loadCts?.Cancel();

            IsLoading = true;
            HasError = false;
            ErrorMessage = null;

            var result = await _fileService.FindFilesAsync($"*{query}*", ct);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error?.Message ?? "Search failed.";
                HasError = true;
                Items.Clear();
                return;
            }

            Items.Clear();
            foreach (var file in result.Value!)
            {
                Items.Add(file);
            }

            IsEmpty = Items.Count == 0;
        }
        catch (OperationCanceledException)
        {
            // Debounce cancelled or search cancelled — expected, no action
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    /// <summary>Releases cancellation token sources held by this ViewModel.</summary>
    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
    }
}
