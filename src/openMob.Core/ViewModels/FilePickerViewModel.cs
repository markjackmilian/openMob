using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Services;
using System.Collections.ObjectModel;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the file picker popup.
/// Loads project files via <see cref="IFileService"/> and allows selection.
/// </summary>
public partial class FilePickerViewModel : ObservableObject
{
    private readonly IFileService _fileService;

    public FilePickerViewModel(IFileService fileService)
    {
        ArgumentNullException.ThrowIfNull(fileService);
        _fileService = fileService;
    }

    /// <summary>The full list of files loaded from the project.</summary>
    public ObservableCollection<FileDto> Files { get; } = [];

    /// <summary>The filtered list of files based on <see cref="SearchText"/>.</summary>
    public ObservableCollection<FileDto> FilteredFiles { get; } = [];

    /// <summary>Search text for filtering files by name or path.</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>Indicates whether files are currently loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Indicates whether the filtered list is empty and not loading.</summary>
    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>Error message if file loading fails.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Indicates whether an error occurred.</summary>
    [ObservableProperty]
    private bool _hasError;

    /// <summary>
    /// When set, file selection invokes this callback instead of the default behavior.
    /// Used by the message composer for token insertion mode.
    /// </summary>
    public Action<string>? OnFileSelected { get; set; }

    /// <summary>Loads files from the project via <see cref="IFileService"/>.</summary>
    [RelayCommand]
    private async Task LoadFilesAsync(CancellationToken ct)
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var result = await _fileService.GetFilesAsync(ct).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error?.Message ?? "Failed to load files.";
                HasError = true;
                return;
            }

            var files = result.Value!;

            Files.Clear();
            foreach (var file in files)
            {
                Files.Add(file);
            }

            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
            // Cancelled — no action needed
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Selects a file and invokes the callback or pops the popup.</summary>
    [RelayCommand]
    private async Task SelectFileAsync(FileDto file)
    {
        OnFileSelected?.Invoke(file.RelativePath);
    }

    /// <summary>Filters <see cref="Files"/> into <see cref="FilteredFiles"/> based on <see cref="SearchText"/>.</summary>
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredFiles.Clear();

        var query = SearchText?.Trim() ?? string.Empty;

        foreach (var file in Files)
        {
            if (string.IsNullOrEmpty(query)
                || file.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase)
                || file.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredFiles.Add(file);
            }
        }

        IsEmpty = FilteredFiles.Count == 0 && !IsLoading;
    }
}
