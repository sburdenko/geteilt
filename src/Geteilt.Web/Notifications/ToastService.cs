using Geteilt.Web.Api;
using Geteilt.Web.Localisation;

namespace Geteilt.Web.Notifications;

/// <summary>What the app says to the person when something went wrong or worked.</summary>
public sealed class ToastService(Localizer localizer)
{
    private static readonly TimeSpan GoodNewsLingers = TimeSpan.FromSeconds(4);

    private readonly List<Toast> _toasts = [];

    public IReadOnlyList<Toast> Toasts => _toasts;

    public event Action? Changed;

    /// <summary>Errors stay until dismissed: they are the ones worth reading twice.</summary>
    public void ShowError(ApiError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        Toast toast = new(Guid.CreateVersion7(), ToastLevel.Error, ErrorPhrase.For(localizer, error), DetailOf(error));

        Add(toast, linger: null);
    }

    public void ShowSuccess(string title) =>
        Add(new Toast(Guid.CreateVersion7(), ToastLevel.Success, title, null), GoodNewsLingers);

    public void ShowInfo(string title) =>
        Add(new Toast(Guid.CreateVersion7(), ToastLevel.Info, title, null), GoodNewsLingers);

    public void Dismiss(Guid id)
    {
        if (_toasts.RemoveAll(toast => toast.Id == id) > 0)
        {
            Changed?.Invoke();
        }
    }

    private void Add(Toast toast, TimeSpan? linger)
    {
        _toasts.Add(toast);
        Changed?.Invoke();

        if (linger is not null)
        {
            _ = DismissLaterAsync(toast.Id, linger.Value);
        }
    }

    private async Task DismissLaterAsync(Guid id, TimeSpan after)
    {
        await Task.Delay(after);

        Dismiss(id);
    }

    private static string? DetailOf(ApiError error)
    {
        if (error.FieldErrors.Count == 0)
        {
            // A detail that only repeats the server's title is noise; the API sends that for auth
            // failures. It is compared against the server's title, not the translated one.
            return string.Equals(error.Detail, error.Title, StringComparison.Ordinal) ? null : error.Detail;
        }

        return string.Join(
            '\n',
            error.FieldErrors.SelectMany(field => field.Value.Select(message => $"{field.Key}: {message}")));
    }
}
