namespace Geteilt.Contracts.Settlements;

/// <summary>How the money actually moved. A third copy of a domain enum, for the usual reason.</summary>
public enum SettlementMethod
{
    BankTransfer,
    Cash,
    Other,
}
