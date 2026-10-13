using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Geteilt.Application.Expenses;

/// <summary>
/// Where the previous page stopped: the date and id of its last row.
/// </summary>
/// <remarks>
/// Keyset rather than offset. With OFFSET, an expense added while somebody is paging shifts every
/// later row by one, so the reader sees one entry twice and misses another — and somebody will add
/// one, because everyone is sitting at the same table. A cursor names a position in the ordering
/// instead of counting from the start, so inserts elsewhere cannot move it.
///
/// Base64 on the wire so it reads as a token rather than as two fields worth guessing at. That is
/// not a security measure — it is opaque, not secret — it just keeps clients from building their
/// own and freezing the shape of the ordering.
/// </remarks>
internal readonly record struct ExpenseCursor(DateOnly Date, Guid Id)
{
    internal string Encode() =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"{Date:yyyy-MM-dd}|{Id}")));

    internal static bool TryDecode(string? cursor, out ExpenseCursor value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            string[] parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor)).Split('|');

            if (parts.Length != 2
                || !DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateOnly date)
                || !Guid.TryParse(parts[1], out Guid id))
            {
                return false;
            }

            value = new ExpenseCursor(date, id);

            return true;
        }
        catch (FormatException)
        {
            // A cursor is client-supplied, so garbage in it is an ordinary event, not an incident.
            return false;
        }
    }
}
