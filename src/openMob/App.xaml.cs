namespace openMob;

/// <summary>Application entry point.</summary>
public partial class App : Application
{
    /// <summary>Initialises the application.</summary>
    public App()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
        => new Window(new AppShell());
}
