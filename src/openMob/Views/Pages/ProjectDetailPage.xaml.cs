using openMob.Core.ViewModels;

namespace openMob.Views.Pages;

/// <summary>Project detail page — shows project info, recent sessions, and configuration.</summary>
[QueryProperty(nameof(ProjectId), "projectId")]
public partial class ProjectDetailPage : ContentPage
{
    private readonly ProjectDetailViewModel _viewModel;

    /// <summary>Initialises the project detail page with its ViewModel.</summary>
    /// <param name="viewModel">The project detail ViewModel.</param>
    public ProjectDetailPage(ProjectDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    /// <summary>Gets or sets the project ID received via query parameter.</summary>
    public string ProjectId
    {
        get => _viewModel.ProjectId;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _ = _viewModel.LoadProjectCommand.ExecuteAsync(value);
            }
        }
    }
}
