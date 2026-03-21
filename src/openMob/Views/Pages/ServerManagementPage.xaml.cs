using openMob.Core.ViewModels;

namespace openMob.Views.Pages;

/// <summary>Server management page — lists saved servers and mDNS-discovered servers.</summary>
public partial class ServerManagementPage : ContentPage
{
    private readonly ServerManagementViewModel _viewModel;

    /// <summary>Initialises the server management page with its ViewModel.</summary>
    public ServerManagementPage(ServerManagementViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    /// <summary>Navigates back when the back button is tapped.</summary>
    private async void OnBackButtonTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    /// <inheritdoc/>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
