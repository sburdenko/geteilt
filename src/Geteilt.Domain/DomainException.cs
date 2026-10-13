namespace Geteilt.Domain;

/// <summary>
/// Base for every failure the domain raises deliberately, as opposed to a bug.
/// The message is the per-occurrence detail; everything stable about the failure lives in
/// <see cref="Error"/> and is part of the public API contract.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(DomainError error, string message)
        : base(message) => Error = error;

    protected DomainException(DomainError error, string message, Exception innerException)
        : base(message, innerException) => Error = error;

    public DomainError Error { get; } = new(string.Empty, DomainErrorKind.Validation, string.Empty);

    public string Code => Error.Code;

    public DomainErrorKind Kind => Error.Kind;
}
