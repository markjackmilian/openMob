using openMob.Core.ViewModels;
using UXDivers.Popups.Maui;
using UXDivers.Popups.Services;

namespace openMob.Views.Popups;

/// <summary>Add project popup — collects project name and path for creation.</summary>
public partial class AddProjectSheet : PopupPage
{
    /// <summary>Initialises the add project sheet with its ViewModel.</summary>
    /// <param name="viewModel">The add project ViewModel.</param>
    public AddProjectSheet(AddProjectViewModel viewModel)
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
