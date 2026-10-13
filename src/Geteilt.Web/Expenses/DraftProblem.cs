namespace Geteilt.Web.Expenses;

/// <summary>
/// Why a draft cannot be sent yet. The names match the error codes the API answers with, so the
/// same phrase in the translation files serves both a form the user has not finished and a
/// rejection that arrives from the server.
/// </summary>
public enum DraftProblem
{
    DescriptionMissing,
    DescriptionTooLong,
    AmountMustBePositive,
    PayerMissing,
    NobodyTakingPart,
    SharesSumMismatch,
    DateInFuture,
}
