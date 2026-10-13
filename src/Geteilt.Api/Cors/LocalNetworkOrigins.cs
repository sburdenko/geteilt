using System.Net;

namespace Geteilt.Api.Cors;

/// <summary>
/// Which browser origins the development API answers.
/// </summary>
/// <remarks>
/// A list of literal origins does not survive testing on a phone: the app is then served from
/// whatever address the router handed the laptop this morning, and that changes. This accepts any
/// origin on the local network instead, which is the same trust boundary as "somebody is on your
/// wifi" — fine while developing, and never reachable in production, because the policy is only
/// registered when the environment is Development.
/// </remarks>
public static class LocalNetworkOrigins
{
    public static bool Allows(string origin, IReadOnlyCollection<string> configured)
    {
        ArgumentNullException.ThrowIfNull(configured);

        if (configured.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(origin, UriKind.Absolute, out Uri? parsed)
               && parsed.Scheme is "http" or "https"
               && IsLocal(parsed.Host);
    }

    private static bool IsLocal(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out IPAddress? address))
        {
            return false;
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        // The private ranges of RFC 1918, plus link-local from RFC 3927 for a laptop and a phone
        // that found each other without a router.
        return address.GetAddressBytes() switch
        {
            [10, ..] => true,
            [172, >= 16 and <= 31, ..] => true,
            [192, 168, ..] => true,
            [169, 254, ..] => true,
            _ => false,
        };
    }
}
