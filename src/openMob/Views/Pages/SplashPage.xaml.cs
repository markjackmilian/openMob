using openMob.Core.ViewModels;

namespace openMob.Views.Pages;

/// <summary>Splash page — bootstrap screen that determines initial navigation destination.</summary>
public partial class SplashPage : ContentPage
{
    private readonly SplashViewModel _viewModel;

    /// <summary>Initialises the splash page with its ViewModel.</summary>
    /// <param name="viewModel">The splash ViewModel.</param>
    public SplashPage(SplashViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel.InitializeCommand.CanExecute(null))
        {
            await _viewModel.InitializeCommand.ExecuteAsync(null);
        }
    }
}
