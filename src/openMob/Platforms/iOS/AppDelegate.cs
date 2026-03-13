using Foundation;

namespace openMob;

/// <summary>iOS application delegate — entry point for the iOS platform.</summary>
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
