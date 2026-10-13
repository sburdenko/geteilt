namespace Geteilt.Domain.Users;

/// <summary>
/// A person with an account. Passwords, lockout and login attempts live in the ASP.NET Core
/// Identity tables, deliberately not here.
/// </summary>
public sealed class User
{
    public const int MaxEmailLength = 256;
    public const int MaxDisplayNameLength = 100;

    public User(Guid id, string email, string displayName, DateTimeOffset createdAt, string? iban = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(email.Length, MaxEmailLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(displayName.Length, MaxDisplayNameLength);

        Id = id;
        // Stored lower case so uniqueness means what a user expects: Anna@ and anna@ are one person.
        Email = email.ToLowerInvariant();
        DisplayName = displayName;
        CreatedAt = createdAt;
        SetIban(iban);
    }

    /// <summary>Materialisation constructor for EF Core.</summary>
    private User()
    {
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Stored without spaces and upper case, so it can go straight into a Girocode.</summary>
    public string? Iban { get; private set; }

    public string PreferredCurrency { get; private set; } = Currency.Euro;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public void SetIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            Iban = null;
            return;
        }

        // A domain exception, not an ArgumentException: this value comes from a person filling in
        // a form, so the API has to answer 422 with a reason rather than 500.
        if (!Girocode.Iban.TryNormalise(iban, out string normalised))
        {
            throw DomainValidationException.ForIban(iban);
        }

        Iban = normalised;
    }

    public void Rename(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(displayName.Length, MaxDisplayNameLength);

        DisplayName = displayName;
    }

    public void SetPreferredCurrency(string currency)
    {
        if (!Currency.IsValid(currency))
        {
            throw DomainValidationException.ForCurrency(currency);
        }

        PreferredCurrency = Currency.Normalise(currency);
    }
}
