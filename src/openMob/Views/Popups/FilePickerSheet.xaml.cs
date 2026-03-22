using openMob.Core.ViewModels;
using UXDivers.Popups.Maui;
using UXDivers.Popups.Services;

namespace openMob.Views.Popups;

/// <summary>
/// File picker popup — displays a searchable list of project files
/// loaded from the opencode server via <see cref="FilePickerViewModel"/>.
/// File loading is handled by MauiPopupService before this popup is pushed.
/// </summary>
public partial class FilePickerSheet : PopupPage
{
    /// <summary>Initialises the file picker sheet with its ViewModel.</summary>
    /// <param name="viewModel">The file picker ViewModel.</param>
    public FilePickerSheet(FilePickerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <summary>Closes the popup when the close button is tapped.</summary>
    private async void OnCloseButtonTapped(object? sender, EventArgs e)
    {
        await IPopupService.Current.PopAsync(this);
    }
}
