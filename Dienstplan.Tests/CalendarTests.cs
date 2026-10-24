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
            s = new[] { new { n = "Frühdienst", b = "06:00", e = "14:00" }, new { n = "Urlaub", b = "", e = "" } },
            d = new Dictionary<string, int> { ["2026-11-04"] = 1, ["2026-11-03"] = 0 },
        });

        Assert.True(ShiftPlan.TryDecode(encoded, out var events));
        Assert.Equal(
            [
                new ShiftEvent(new DateOnly(2026, 11, 3), "Frühdienst", new TimeOnly(6, 0), new TimeOnly(14, 0)),
                new ShiftEvent(new DateOnly(2026, 11, 4), "Urlaub", null, null),
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
        var encoded = Encode(new { s = new[] { new { n = "Früh", b = "06:00", e = "14:00" } }, d = new Dictionary<string, int> { ["2026-11-03"] = 5 } });
        Assert.False(ShiftPlan.TryDecode(encoded, out _));
    }

    [Fact]
    public void Writes_rfc5545_event_with_uid_dtstamp_and_crlf()
    {
        var ics = IcsWriter.Write([new ShiftEvent(new DateOnly(2026, 11, 3), "Frühdienst", new TimeOnly(6, 0), new TimeOnly(14, 0))], Stamp);

        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
        Assert.DoesNotContain("\r\n\r\n", ics);
        Assert.Contains("UID:dienst-20261103@dienstplan.code-inside.de\r\n", ics);
        Assert.Contains("DTSTAMP:20261024T120000Z\r\n", ics);
        Assert.Contains("DTSTART:20261103T060000\r\nDTEND:20261103T140000\r\n", ics);
        Assert.Contains("SUMMARY:Frühdienst\r\n", ics);
    }

    [Fact]
    public void Night_shift_ends_next_day()
    {
        var ics = IcsWriter.Write([new ShiftEvent(new DateOnly(2026, 11, 30), "Nachtdienst", new TimeOnly(21, 0), new TimeOnly(6, 30))], Stamp);

        Assert.Contains("DTSTART:20261130T210000\r\nDTEND:20261201T063000\r\n", ics);
    }

    [Fact]
    public void All_day_event_uses_date_values()
    {
        var ics = IcsWriter.Write([new ShiftEvent(new DateOnly(2026, 12, 31), "Urlaub", null, null)], Stamp);

        Assert.Contains("DTSTART;VALUE=DATE:20261231\r\nDTEND;VALUE=DATE:20270101\r\n", ics);
    }

    [Fact]
    public void Escapes_and_folds_long_titles()
    {
        var ics = IcsWriter.Write([new ShiftEvent(new DateOnly(2026, 11, 3), "Dienst; mit, Sonderzeichen " + new string('ü', 40), null, null)], Stamp);

        Assert.Contains(@"SUMMARY:Dienst\; mit\, Sonderzeichen", ics);
        Assert.All(ics.Split("\r\n"), line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, line));
    }
}
