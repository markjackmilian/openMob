using Android.App;
using Android.Runtime;

namespace openMob;

/// <summary>Android application class — bootstraps the MAUI application on Android.</summary>
[Application]
public class MainApplication : MauiApplication
{
    /// <summary>Initialises the Android application.</summary>
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
