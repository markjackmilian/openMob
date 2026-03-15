using openMob.Core.ViewModels;

namespace openMob.Views.Popups;

/// <summary>Add project bottom sheet — collects project name and path for creation.</summary>
public partial class AddProjectSheet : ContentPage
{
    /// <summary>Initialises the add project sheet with its ViewModel.</summary>
    /// <param name="viewModel">The add project ViewModel.</param>
    public AddProjectSheet(AddProjectViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
