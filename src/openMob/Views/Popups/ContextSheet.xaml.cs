using openMob.Core.ViewModels;
using UXDivers.Popups.Maui;
using UXDivers.Popups.Services;

namespace openMob.Views.Popups;

/// <summary>
/// Context Sheet popup — displays and allows editing of session-level settings:
/// project, agent, model, thinking level, auto-accept, and subagent invocation.
/// Preferences are loaded by <see cref="openMob.Services.MauiPopupService.ShowContextSheetAsync"/>
/// before this popup is pushed via <see cref="IPopupService"/>.
/// </summary>
public partial class ContextSheet : PopupPage
{
    private readonly ContextSheetViewModel _viewModel;

    /// <summary>Initialises the context sheet with its ViewModel.</summary>
    /// <param name="viewModel">The context sheet ViewModel.</param>
    public ContextSheet(ContextSheetViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    /// <summary>Closes the popup when the close button is tapped.</summary>
    private async void OnCloseButtonTapped(object? sender, EventArgs e)
    {
        await IPopupService.Current.PopAsync(this);
    }
}
