using openMob.Core.ViewModels;

namespace openMob.Views.Popups;

/// <summary>
/// Context Sheet bottom sheet — displays and allows editing of session-level settings:
/// project, agent, model, thinking level, auto-accept, and subagent invocation.
/// Preferences are loaded by <see cref="openMob.Services.MauiPopupService.ShowContextSheetAsync"/>
/// before this page is pushed modally.
/// </summary>
public partial class ContextSheet : ContentPage
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

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Initialization is performed by MauiPopupService.ShowContextSheetAsync
        // before this page is pushed modally. No action needed here.
    }
}
