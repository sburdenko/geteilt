namespace Geteilt.Api.Logging;

/// <summary>
/// Shortens an IBAN for logging. A full one never goes into the logs — it would sit in
/// whatever collects them, readable by anyone with access to that.
/// </summary>
/// <remarks>
/// A type of its own so the rule has one implementation and one test, rather than a substring
/// expression repeated at each call site where it could quietly be got wrong.
/// </remarks>
internal static class IbanLog
{
    internal const int VisibleCharacters = 4;

    internal static string Tail(string? iban)
    {
        if (string.IsNullOrEmpty(iban))
        {
            return string.Empty;
        }

        // Anything short enough that four characters would be most of it is hidden entirely.
        return iban.Length <= VisibleCharacters
            ? new string('*', iban.Length)
            : iban[^VisibleCharacters..];
    }
}
