using Geteilt.Web.Localisation;
using Microsoft.Extensions.Time.Testing;

namespace Geteilt.Web.Tests;

/// <summary>A date the person sees on their calendar, not the one in Greenwich.</summary>
public sealed class LocalDateTests
{
    [Fact]
    public void JustAfterMidnightInBerlin_IsAlreadyTheNextDay()
    {
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 10, 9, 22, 30, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));

        clock.LocalToday().Should().Be(new DateOnly(2026, 10, 10));
    }

    [Fact]
    public void InTheMiddleOfTheDay_UtcAndLocalAgree()
    {
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));

        clock.LocalToday().Should().Be(new DateOnly(2026, 10, 9));
    }
}
