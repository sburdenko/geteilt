namespace Geteilt.Web.Localisation;

public static class LocalDate
{
    /// <summary>
    /// Today on the reader's own calendar. In the browser the local zone is the device's, and in
    /// Germany UTC is still on yesterday for the first hour or two after midnight.
    /// </summary>
    public static DateOnly LocalToday(this TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        return DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
    }
}
