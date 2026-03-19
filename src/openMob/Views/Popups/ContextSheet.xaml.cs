using openMob.Core.ViewModels;

namespace openMob.Views.Popups;

/// <summary>
/// Context Sheet bottom sheet — displays and allows editing of session-level settings:
/// project, agent, model, thinking level, auto-accept, and subagent invocation (REQ-025 through REQ-028).
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
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel.LoadContextCommand.CanExecute(null))
        {
            await _viewModel.LoadContextCommand.ExecuteAsync(null);
        }
    }
}
