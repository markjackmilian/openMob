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

    /// <inheritdoc/>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
