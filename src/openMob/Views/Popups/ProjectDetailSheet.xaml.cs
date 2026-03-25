using openMob.Core.ViewModels;
using UXDivers.Popups.Maui;
using UXDivers.Popups.Services;

namespace openMob.Views.Popups;

/// <summary>
/// Project detail popup — shows the current project's metadata and model override state.
/// The ViewModel is initialized by <see cref="openMob.Services.MauiPopupService.ShowProjectDetailAsync"/>
/// before this popup is pushed via <see cref="IPopupService"/>.
/// </summary>
public partial class ProjectDetailSheet : PopupPage
{
    /// <summary>Initialises the project detail sheet with its ViewModel.</summary>
    /// <param name="viewModel">The project detail ViewModel.</param>
    public ProjectDetailSheet(ProjectDetailViewModel viewModel)
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
