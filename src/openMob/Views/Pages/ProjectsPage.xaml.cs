using openMob.Core.ViewModels;

namespace openMob.Views.Pages;

/// <summary>Projects page — list of all projects with add, select, and delete actions.</summary>
public partial class ProjectsPage : ContentPage
{
    private readonly ProjectsViewModel _viewModel;

    /// <summary>Initialises the projects page with its ViewModel.</summary>
    /// <param name="viewModel">The projects ViewModel.</param>
    public ProjectsPage(ProjectsViewModel viewModel)
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

    /// <summary>Navigates back when the back button is tapped.</summary>
    private async void OnBackButtonTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnAddProjectClicked(object? sender, EventArgs e)
    {
        // FolderPickerSheet will be pushed via IAppPopupService when fully integrated.
        if (_viewModel.ShowAddProjectCommand.CanExecute(null))
        {
            await _viewModel.ShowAddProjectCommand.ExecuteAsync(null);
        }
    }
}
