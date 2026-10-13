namespace Geteilt.Contracts.Members;

/// <summary>
/// What someone may do inside a group.
/// </summary>
/// <remarks>
/// Deliberately a second copy of the domain enum of the same name. Both Geteilt.Domain and
/// Geteilt.Contracts reference nothing, so a shared type has nowhere to live — and Contracts is
/// compiled into the mobile client, where the domain has no business being. The mapping between
/// them is one switch and the compiler catches a new value in either.
/// </remarks>
public enum MemberRole
{
    Owner,
    Member,
}
