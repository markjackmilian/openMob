using openMob.Core.ViewModels;
using UXDivers.Popups.Maui;
using UXDivers.Popups.Services;

namespace openMob.Views.Popups;

/// <summary>
/// Project switcher popup — allows rapid project switching from the chat header.
/// Project loading is handled by MauiPopupService.ShowProjectSwitcherAsync before this popup is pushed.
/// </summary>
public partial class ProjectSwitcherSheet : PopupPage
{
    /// <summary>Initialises the project switcher sheet with its ViewModel.</summary>
    /// <param name="viewModel">The project switcher ViewModel.</param>
    public ProjectSwitcherSheet(ProjectSwitcherViewModel viewModel)
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
