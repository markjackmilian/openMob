using openMob.Core.ViewModels;
using UXDivers.Popups.Maui;
using UXDivers.Popups.Services;

namespace openMob.Views.Popups;

/// <summary>
/// Folder picker popup for selecting a server-side project directory.
/// </summary>
public partial class FolderPickerSheet : PopupPage
{
    /// <summary>Initialises the folder picker sheet with its ViewModel.</summary>
    /// <param name="viewModel">The folder picker ViewModel.</param>
    public FolderPickerSheet(FolderPickerViewModel viewModel)
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
