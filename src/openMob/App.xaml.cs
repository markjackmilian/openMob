namespace openMob;

/// <summary>Application entry point.</summary>
public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>Initialises the application.</summary>
    /// <param name="serviceProvider">The DI service provider.</param>
    public App(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
    {
        // AppShell must be resolved AFTER InitializeComponent() so that
        // Colors.xaml and Styles.xaml are loaded before Shell XAML parses
        // StaticResource references.
        var shell = _serviceProvider.GetRequiredService<AppShell>();
        return new Window(shell);
    }
}
