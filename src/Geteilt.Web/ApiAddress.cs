namespace Geteilt.Web;

public static class ApiAddress
{
    /// <summary>
    /// Placeholder for the host the app was served from. Configuration writes
    /// <c>http://{host}:8080/</c> and this fills in the rest.
    /// </summary>
    /// <remarks>
    /// The development address used to be spelled localhost, which is right until the app is
    /// opened on a phone — where localhost is the phone. Substituting the host the page came from
    /// makes one setting work from the laptop and from anything else on the same wifi, without
    /// anybody editing a file with today's IP address in it.
    /// </remarks>
    public const string HostPlaceholder = "{host}";

    public static Uri Resolve(string? configured, string appBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(appBaseAddress);

        if (string.IsNullOrWhiteSpace(configured))
        {
            return new Uri(appBaseAddress);
        }

        return configured.Contains(HostPlaceholder, StringComparison.Ordinal)
            ? new Uri(configured.Replace(HostPlaceholder, new Uri(appBaseAddress).Host, StringComparison.Ordinal))
            : new Uri(configured);
    }
}
