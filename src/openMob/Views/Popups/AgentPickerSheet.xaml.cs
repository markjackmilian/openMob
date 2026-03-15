using openMob.Core.ViewModels;

namespace openMob.Views.Popups;

/// <summary>Agent picker bottom sheet — displays available agents for selection.</summary>
public partial class AgentPickerSheet : ContentPage
{
    private readonly AgentPickerViewModel _viewModel;

    /// <summary>Initialises the agent picker sheet with its ViewModel.</summary>
    /// <param name="viewModel">The agent picker ViewModel.</param>
    public AgentPickerSheet(AgentPickerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel.LoadAgentsCommand.CanExecute(null))
        {
            await _viewModel.LoadAgentsCommand.ExecuteAsync(null);
        }
    }
}
