using UIKit;

namespace openMob;

/// <summary>iOS application entry point.</summary>
public class Program
{
    /// <summary>Main entry point for the iOS application.</summary>
    static void Main(string[] args)
    {
        // Install pre-Sentry crash diagnostics. Sentry initialises inside CreateMauiApp()
        // (via UseSentry in MauiProgram.cs), so any exception thrown before or during that
        // call is invisible to Sentry. This handler writes a plain-text crash log to the
        // app's Documents directory so it survives the crash and can be retrieved via
        // Xcode → Devices & Simulators → Download Container, or read on next launch.
        InstallPreSentryCrashLogger();

        UIApplication.Main(args, null, typeof(AppDelegate));
    }

    /// <summary>
    /// Installs <see cref="AppDomain.UnhandledException"/> and
    /// <see cref="TaskScheduler.UnobservedTaskException"/> handlers that write a
    /// timestamped crash log to <c>Documents/startup-crash.log</c> before the process
    /// terminates. The file is overwritten on each crash so it always contains the
    /// most recent failure.
    /// </summary>
    private static void InstallPreSentryCrashLogger()
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "startup-crash.log");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                var ex = e.ExceptionObject as Exception;
                var entry = $"[{DateTime.UtcNow:O}] UNHANDLED EXCEPTION (IsTerminating={e.IsTerminating})\n" +
                            $"Type: {ex?.GetType().FullName ?? "unknown"}\n" +
                            $"Message: {ex?.Message ?? e.ExceptionObject?.ToString()}\n" +
                            $"StackTrace:\n{ex?.StackTrace}\n" +
                            $"InnerException: {ex?.InnerException?.Message}\n" +
                            new string('-', 80) + "\n";
                File.AppendAllText(logPath, entry);
            }
            catch
            {
                // Never throw inside an unhandled-exception handler.
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            try
            {
                var entry = $"[{DateTime.UtcNow:O}] UNOBSERVED TASK EXCEPTION\n" +
                            $"Message: {e.Exception?.Message}\n" +
                            $"StackTrace:\n{e.Exception?.StackTrace}\n" +
                            new string('-', 80) + "\n";
                File.AppendAllText(logPath, entry);
            }
            catch
            {
                // Never throw inside an exception handler.
            }
        };
    }
}
