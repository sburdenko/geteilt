using System.Diagnostics;

namespace Geteilt.Web.Api;

/// <summary>
/// What an API call produced. Closed: a call either carries a value or an error, never both and
/// never neither, so a caller cannot forget to handle the failure.
/// </summary>
public abstract record Result<T>
{
    private Result()
    {
    }

    public sealed record Ok(T Value) : Result<T>;

    public sealed record Failed(ApiError Error) : Result<T>;

    public TOut Match<TOut>(Func<T, TOut> whenOk, Func<ApiError, TOut> whenFailed)
    {
        ArgumentNullException.ThrowIfNull(whenOk);
        ArgumentNullException.ThrowIfNull(whenFailed);

        return this switch
        {
            Ok ok => whenOk(ok.Value),
            Failed failed => whenFailed(failed.Error),
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>
    /// Two calls a screen needs together. Both succeeded, or the first failure is the result:
    /// half a screen is not offered.
    /// </summary>
    public Result<TOut> Zip<TOther, TOut>(Result<TOther> other, Func<T, TOther, TOut> combine)
    {
        ArgumentNullException.ThrowIfNull(other);
        ArgumentNullException.ThrowIfNull(combine);

        return (this, other) switch
        {
            (Ok mine, Result<TOther>.Ok theirs) => new Result<TOut>.Ok(combine(mine.Value, theirs.Value)),
            (Failed failed, _) => new Result<TOut>.Failed(failed.Error),
            (_, Result<TOther>.Failed failed) => new Result<TOut>.Failed(failed.Error),
            _ => throw new UnreachableException(),
        };
    }

    public void Switch(Action<T> whenOk, Action<ApiError> whenFailed)
    {
        ArgumentNullException.ThrowIfNull(whenOk);
        ArgumentNullException.ThrowIfNull(whenFailed);

        switch (this)
        {
            case Ok ok:
                whenOk(ok.Value);
                break;
            case Failed failed:
                whenFailed(failed.Error);
                break;
            default:
                throw new UnreachableException();
        }
    }
}

/// <summary>A call that answers with nothing but success or failure.</summary>
public readonly record struct Unit
{
    public static Unit Value => default;
}
