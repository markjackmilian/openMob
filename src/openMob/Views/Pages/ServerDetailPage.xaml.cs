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
    /// Guard flag set to <see langword="true"/> while the ViewModel is updating
    /// <c>IsServerAutoApproveEnabled</c> programmatically (optimistic update or rollback).
    /// Prevents the resulting <c>Switch.Toggled</c> event from re-invoking the command.
    /// </summary>
    private bool _isProgrammaticToggle;

    /// <summary>Initialises the server detail page with its ViewModel.</summary>
    public ServerDetailPage(ServerDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        // Subscribe to PropertyChanged so we can set the guard flag before
        // IsServerAutoApproveEnabled changes propagate to the Switch via OneWay binding.
        // This prevents the Switch.Toggled event from re-firing when the ViewModel
        // performs an optimistic update or a rollback.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>Navigates back when the back button is tapped.</summary>
    private async void OnBackButtonTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>
    /// Intercepts ViewModel property changes to set the programmatic-toggle guard
    /// before <c>IsServerAutoApproveEnabled</c> propagates to the <c>Switch</c> via
    /// the OneWay binding. This prevents the Switch from re-firing <c>Toggled</c>
    /// when the ViewModel performs an optimistic update or a rollback.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ServerDetailViewModel.IsServerAutoApproveEnabled))
        {
            _isProgrammaticToggle = true;
        }
    }

    /// <summary>
    /// Forwards the auto-approve toggle's Toggled event to the ViewModel command.
    /// Ignores events fired programmatically by the OneWay binding (optimistic update / rollback).
    /// Using a code-behind handler instead of EventToCommandBehavior because the
    /// toolkit namespace is not declared in this project's XAML pages.
    /// </summary>
    private void OnAutoApproveSwitchToggled(object? sender, ToggledEventArgs e)
    {
        if (_isProgrammaticToggle)
        {
            _isProgrammaticToggle = false;
            return;
        }

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
