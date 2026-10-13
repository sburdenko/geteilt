
namespace Geteilt.Domain.Groups;

/// <summary>A set of people sharing expenses: a trip, a dinner, a rented house.</summary>
public sealed class Group
{
    public Group(Guid id, string name, string baseCurrency, Guid createdByUserId, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, MaxNameLength);

        Id = id;
        Name = name;
        // A domain exception rather than the ArgumentException Currency.Normalise raises: this
        // value arrives from a request body, so the API owes the caller 422 invalid_currency
        // instead of a 500.
        if (!Currency.IsValid(baseCurrency))
        {
            throw DomainValidationException.ForCurrency(baseCurrency);
        }

        BaseCurrency = Currency.Normalise(baseCurrency);
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    /// <summary>Materialisation constructor for EF Core.</summary>
    private Group()
    {
    }

    public const int MaxNameLength = 100;

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// ISO 4217, upper case. Never changes: switching it would mean re-converting every historical
    /// expense at historical rates, and that has no single correct answer.
    /// </summary>
    public string BaseCurrency { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>An archived group can still be read, but nothing new can be written to it.</summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, MaxNameLength);

        RejectIfArchived();

        Name = name;
    }

    /// <summary>Archiving is reversible; deleting a group is not offered at all.</summary>
    public void Archive(DateTimeOffset now) => ArchivedAt ??= now;

    public void Unarchive() => ArchivedAt = null;

    /// <summary>
    /// Guards every write. An archived group stays readable — the history has to remain
    /// available — but nothing new may be added to it.
    /// </summary>
    public void RejectIfArchived()
    {
        if (ArchivedAt is not null)
        {
            throw GroupException.Archived(Id);
        }
    }
}
