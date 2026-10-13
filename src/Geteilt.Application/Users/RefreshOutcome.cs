namespace Geteilt.Application.Users;

public enum RefreshOutcome
{
    Success,

    /// <summary>No such token. Either forged, or from a database that has since been cleaned.</summary>
    NotFound,

    Expired,

    /// <summary>
    /// A token that was already redeemed came back. The legitimate holder has a successor, so
    /// whoever sent this one had a copy: the whole chain is revoked.
    /// </summary>
    Reused,
}
