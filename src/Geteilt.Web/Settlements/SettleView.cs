namespace Geteilt.Web.Settlements;

/// <summary>What the QR screen found at its position in the plan.</summary>
public abstract record SettleView
{
    private SettleView()
    {
    }

    public sealed record Open(OpenTransfer Transfer) : SettleView;

    /// <summary>The plan moved on: this transfer was paid, or the balances changed under it.</summary>
    public sealed record NothingOpen : SettleView;
}
