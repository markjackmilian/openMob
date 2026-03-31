using openMob.Core.ViewModels;

namespace openMob.Views.Pages;

/// <summary>Server detail page — form for adding, editing, and managing a server connection.</summary>
[QueryProperty(nameof(ServerId), "serverId")]
[QueryProperty(nameof(DiscoveredHost), "discoveredHost")]
[QueryProperty(nameof(DiscoveredPort), "discoveredPort")]
[QueryProperty(nameof(DiscoveredName), "discoveredName")]
public partial class ServerDetailPage : ContentPage
{
    private readonly ServerDetailViewModel _viewModel;
    private string? _discoveredHost;
    private int _discoveredPort;
    private string? _discoveredName;
    private bool _initialised;

    /// <summary>
    /// Guard flag that suppresses the <c>Switch.Toggled</c> handler while the
    /// code-behind itself is updating <c>Switch.IsToggled</c> programmatically.
    /// </summary>
    private bool _updatingSwitch;

    /// <summary>Initialises the server detail page with its ViewModel.</summary>
    public ServerDetailPage(ServerDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        // Sync the Switch visual state with the ViewModel property manually.
        // This avoids the MAUI Switch double-fire problem entirely:
        // no IsToggled binding → no programmatic Toggled events.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>Navigates back when the back button is tapped.</summary>
    private async void OnBackButtonTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>
    /// Keeps the <c>AutoApproveSwitch</c> visual state in sync with
    /// <see cref="ServerDetailViewModel.IsServerAutoApproveEnabled"/> without
    /// using a XAML binding. This eliminates the MAUI <c>Switch.Toggled</c>
    /// double-fire problem: the guard flag <see cref="_updatingSwitch"/> ensures
    /// that programmatic updates to <c>IsToggled</c> do not re-invoke the command.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ServerDetailViewModel.IsServerAutoApproveEnabled))
        {
            _updatingSwitch = true;
            AutoApproveSwitch.IsToggled = _viewModel.IsServerAutoApproveEnabled;
            _updatingSwitch = false;
        }
    }

    /// <summary>
    /// Handles user-initiated toggle changes only. Programmatic changes (from
    /// <see cref="OnViewModelPropertyChanged"/>) are suppressed by the
    /// <see cref="_updatingSwitch"/> guard flag.
    /// </summary>
    private void OnAutoApproveSwitchToggled(object? sender, ToggledEventArgs e)
    {
        if (_updatingSwitch)
            return;

        if (_viewModel.ToggleServerAutoApproveCommand.CanExecute(null))
            _viewModel.ToggleServerAutoApproveCommand.Execute(null);
    }

    /// <summary>Gets or sets the server ID received via query parameter (Edit mode).</summary>
    public string ServerId
    {
        get => string.Empty;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _initialised = true;
                _ = _viewModel.InitialiseAsync(value);
            }
        }
    }

    /// <summary>Gets or sets the discovered host received via query parameter (Add mode from discovery).</summary>
    public string DiscoveredHost { get => string.Empty; set => _discoveredHost = value; }

    /// <summary>Gets or sets the discovered port received via query parameter (Add mode from discovery).</summary>
    public string DiscoveredPort { get => string.Empty; set => int.TryParse(value, out _discoveredPort); }

    /// <summary>Gets or sets the discovered name received via query parameter (Add mode from discovery).</summary>
    public string DiscoveredName { get => string.Empty; set => _discoveredName = value; }

    /// <inheritdoc/>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_initialised)
        {
            _initialised = true;
            _ = _viewModel.InitialiseAsync(null, _discoveredHost, _discoveredPort, _discoveredName);
        }
    }
}
