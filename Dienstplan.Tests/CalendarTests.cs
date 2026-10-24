using System.Text;
using System.Text.Json;
using Dienstplan.Web.Calendar;
using Microsoft.AspNetCore.WebUtilities;

namespace Dienstplan.Tests;

public class CalendarTests
{
    private static readonly DateTime Stamp = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    private static string Encode(object payload) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));

    [Fact]
    public void Decodes_plan_from_browser_payload()
    {
        var encoded = Encode(new
        {
            s = new[] { "Frühdienst", "Urlaub" },
            d = new Dictionary<string, int> { ["2026-11-04"] = 1, ["2026-11-03"] = 0 },
        });

        Assert.True(ShiftPlan.TryDecode(encoded, out var events));
        Assert.Equal(
            [
                new ShiftEvent(new DateOnly(2026, 11, 3), "Frühdienst"),
                new ShiftEvent(new DateOnly(2026, 11, 4), "Urlaub"),
            ],
            events);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64-json")]
    public void Rejects_invalid_payload(string? encoded) =>
        Assert.False(ShiftPlan.TryDecode(encoded, out _));

    [Fact]
    public void Rejects_unknown_shift_index()
    {
        var encoded = Encode(new { s = new[] { "Früh" }, d = new Dictionary<string, int> { ["2026-11-03"] = 5 } });
        Assert.False(ShiftPlan.TryDecode(encoded, out _));
    }

    [Fact]
    public void Writes_rfc5545_all_day_event_with_uid_dtstamp_and_crlf()
    {
        var ics = IcsWriter.Write([new ShiftEvent(new DateOnly(2026, 11, 3), "Frühdienst")], Stamp);

        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
        Assert.DoesNotContain("\r\n\r\n", ics);
        Assert.Contains("UID:dienst-20261103@dienstplan.codeinside.eu\r\n", ics);
        Assert.Contains("DTSTAMP:20261024T120000Z\r\n", ics);
        Assert.Contains("DTSTART;VALUE=DATE:20261103\r\nDTEND;VALUE=DATE:20261104\r\n", ics);
        Assert.Contains("SUMMARY:Frühdienst\r\n", ics);
    }

    [Fact]
    public void All_day_event_ends_on_following_day_across_year_boundary()
    {
        var ics = IcsWriter.Write([new ShiftEvent(new DateOnly(2026, 12, 31), "Nachtdienst")], Stamp);

        Assert.Contains("DTSTART;VALUE=DATE:20261231\r\nDTEND;VALUE=DATE:20270101\r\n", ics);
    }

    [Fact]
    public void Escapes_and_folds_long_titles()
    {
        var ics = IcsWriter.Write([new ShiftEvent(new DateOnly(2026, 11, 3), "Dienst; mit, Sonderzeichen " + new string('ü', 40))], Stamp);

        Assert.Contains(@"SUMMARY:Dienst\; mit\, Sonderzeichen", ics);
        Assert.All(ics.Split("\r\n"), line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, line));
    }
}
