using openMob.Core.ViewModels;

namespace openMob.Views.Popups;

/// <summary>
/// Command Palette bottom sheet — displays a searchable list of commands
/// loaded from the opencode server (REQ-029, REQ-030).
/// </summary>
public partial class CommandPaletteSheet : ContentPage
{
    private readonly CommandPaletteViewModel _viewModel;

    /// <summary>Initialises the command palette sheet with its ViewModel.</summary>
    /// <param name="viewModel">The command palette ViewModel.</param>
    public CommandPaletteSheet(CommandPaletteViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel.LoadCommandsCommand.CanExecute(null))
        {
            await _viewModel.LoadCommandsCommand.ExecuteAsync(null);
        }
    }
}
