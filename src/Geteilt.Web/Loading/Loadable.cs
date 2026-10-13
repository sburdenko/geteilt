using System.Diagnostics;
using Geteilt.Web.Api;

namespace Geteilt.Web.Loading;

/// <summary>
/// What a screen has while its data is on the way: nothing yet, the data, or the reason there is
/// none. Closed, like <see cref="Result{T}"/>, so a screen cannot show "nothing here yet" when the
/// truth is "it did not load".
/// </summary>
public abstract record Loadable<T>
{
    private Loadable()
    {
    }

    public sealed record Loading : Loadable<T>;

    public sealed record Loaded(T Value) : Loadable<T>;

    public sealed record Failed(ApiError Error) : Loadable<T>;

    /// <summary>Changes the data if there is any; loading and failed stay as they are.</summary>
    public Loadable<T> Map(Func<T, T> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        return this is Loaded loaded ? new Loaded(change(loaded.Value)) : this;
    }

    public TOut Match<TOut>(Func<TOut> whenLoading, Func<T, TOut> whenLoaded, Func<ApiError, TOut> whenFailed)
    {
        ArgumentNullException.ThrowIfNull(whenLoading);
        ArgumentNullException.ThrowIfNull(whenLoaded);
        ArgumentNullException.ThrowIfNull(whenFailed);

        return this switch
        {
            Loading => whenLoading(),
            Loaded loaded => whenLoaded(loaded.Value),
            Failed failed => whenFailed(failed.Error),
            _ => throw new UnreachableException(),
        };
    }
}
