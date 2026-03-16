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

    /// <summary>Initialises the server detail page with its ViewModel.</summary>
    public ServerDetailPage(ServerDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
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
