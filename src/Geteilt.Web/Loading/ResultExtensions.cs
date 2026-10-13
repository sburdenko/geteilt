using Geteilt.Web.Api;

namespace Geteilt.Web.Loading;

public static class ResultExtensions
{
    /// <summary>What a screen shows once a call has answered: its data, or why there is none.</summary>
    public static Loadable<T> ToLoadable<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Match<Loadable<T>>(value => new Loadable<T>.Loaded(value), error => new Loadable<T>.Failed(error));
    }
}
