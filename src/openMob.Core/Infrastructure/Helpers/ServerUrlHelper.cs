namespace openMob.Core.Infrastructure.Helpers;

/// <summary>
/// Static utility methods for parsing and reconstructing server connection URLs.
/// </summary>
/// <remarks>
/// URL parsing logic is extracted from <c>OnboardingViewModel.TestConnectionAsync</c>
/// to avoid duplication across <c>ServerManagementViewModel</c> and <c>ServerDetailViewModel</c>.
/// </remarks>
public static class ServerUrlHelper
{
    /// <summary>
    /// Attempts to parse a raw URL string into its constituent server connection components.
    /// </summary>
    /// <param name="rawUrl">The raw URL string to parse (e.g. "http://192.168.1.10:4096").</param>
    /// <param name="host">The extracted hostname or IP address, or empty string on failure.</param>
    /// <param name="port">The extracted port number, or 0 on failure.</param>
    /// <param name="useHttps">True if the scheme is https; false otherwise.</param>
    /// <returns>True if parsing succeeded and the scheme is http or https; false otherwise.</returns>
    public static bool TryParse(string? rawUrl, out string host, out int port, out bool useHttps)
    {
        host = string.Empty;
        port = 0;
        useHttps = false;

        if (string.IsNullOrWhiteSpace(rawUrl))
            return false;

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != "http" && uri.Scheme != "https")
            return false;

        host = uri.Host;
        useHttps = uri.Scheme == "https";
        var defaultPort = useHttps ? 443 : 80;
        port = uri.IsDefaultPort ? defaultPort : uri.Port;

        return true;
    }

    /// <summary>Reconstructs a URL string from host, port, and HTTPS flag.</summary>
    /// <param name="host">The hostname or IP address.</param>
    /// <param name="port">The port number.</param>
    /// <param name="useHttps">True to use the https scheme; false for http.</param>
    /// <returns>
    /// A URL string such as <c>http://192.168.1.10:4096</c> or <c>https://myserver.local</c>.
    /// The port is omitted when it equals the protocol default (443 for HTTPS, 80 for HTTP).
    /// </returns>
    public static string BuildUrl(string host, int port, bool useHttps)
    {
        var scheme = useHttps ? "https" : "http";
        var defaultPort = useHttps ? 443 : 80;
        return port == defaultPort ? $"{scheme}://{host}" : $"{scheme}://{host}:{port}";
    }
}
