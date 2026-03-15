using openMob.Core.ViewModels;

namespace openMob.Views.Popups;

/// <summary>Project switcher bottom sheet — allows rapid project switching from the chat header.</summary>
public partial class ProjectSwitcherSheet : ContentPage
{
    private readonly ProjectSwitcherViewModel _viewModel;

    /// <summary>Initialises the project switcher sheet with its ViewModel.</summary>
    /// <param name="viewModel">The project switcher ViewModel.</param>
    public ProjectSwitcherSheet(ProjectSwitcherViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel.LoadProjectsCommand.CanExecute(null))
        {
            await _viewModel.LoadProjectsCommand.ExecuteAsync(null);
        }
    }
}
