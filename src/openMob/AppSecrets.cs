namespace openMob;

/// <summary>
/// Compile-time application secrets injected via a gitignored partial class.
/// </summary>
/// <remarks>
/// <para>
/// To supply real secret values locally, create <c>AppSecrets.Local.cs</c> in the same
/// directory (it is listed in <c>.gitignore</c> and will never be committed).
/// </para>
/// <para>
/// Template for <c>AppSecrets.Local.cs</c>:
/// <code>
/// namespace openMob;
/// internal static partial class AppSecrets
/// {
///     static partial void GetSentryDsn(ref string dsn) =>
///         dsn = "https://&lt;key&gt;@&lt;host&gt;/&lt;project-id&gt;";
/// }
/// </code>
/// </para>
/// <para>
/// In CI/CD pipelines, generate <c>AppSecrets.Local.cs</c> from a repository secret
/// before the build step. Never hardcode secrets in this file.
/// </para>
/// </remarks>
internal static partial class AppSecrets
{
    /// <summary>
    /// Partial method hook — implement in <c>AppSecrets.Local.cs</c> to supply the real DSN.
    /// When not implemented the DSN remains an empty string and Sentry runs in no-op mode.
    /// </summary>
    static partial void GetSentryDsn(ref string dsn);

    /// <summary>Gets the Sentry DSN. Returns <see cref="string.Empty"/> if not configured locally.</summary>
    public static string SentryDsn
    {
        get
        {
            var dsn = string.Empty;
            GetSentryDsn(ref dsn);
            return dsn;
        }
    }
}
