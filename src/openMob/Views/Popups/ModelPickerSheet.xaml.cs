using openMob.Core.ViewModels;

namespace openMob.Views.Popups;

/// <summary>Model picker bottom sheet — displays a flat, virtualised list of AI models for selection.</summary>
public partial class ModelPickerSheet : ContentPage
{
    private readonly ModelPickerViewModel _viewModel;

    /// <summary>Initialises the model picker sheet with its ViewModel.</summary>
    /// <param name="viewModel">The model picker ViewModel.</param>
    public ModelPickerSheet(ModelPickerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    /// <summary>Closes the sheet when the close button is tapped.</summary>
    private async void OnCloseButtonTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel.LoadModelsCommand.CanExecute(null))
        {
            await _viewModel.LoadModelsCommand.ExecuteAsync(null);
        }
    }
}
