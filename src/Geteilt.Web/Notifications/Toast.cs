namespace Geteilt.Web.Notifications;

public enum ToastLevel
{
    Error,
    Success,
    Info,
}

/// <param name="Detail">
/// The problem document's <c>detail</c>. Shown behind a disclosure, not in the headline
///.
/// </param>
public sealed record Toast(Guid Id, ToastLevel Level, string Title, string? Detail);
