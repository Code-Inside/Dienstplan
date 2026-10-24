using System.Text;

namespace Dienstplan.Web.Calendar;

/// <summary>
/// Writes RFC 5545 compliant iCalendar files (CRLF line endings, UID/DTSTAMP, escaping, line folding).
/// Every shift becomes an all-day event, so it shows up as a banner on that day in the calendar.
/// </summary>
public static class IcsWriter
{
    public static string Write(IEnumerable<ShiftEvent> events, DateTime stampUtc)
    {
        var sb = new StringBuilder();
        void Line(string text) => sb.Append(Fold(text)).Append("\r\n");

        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//Code Inside//Dienstplan//DE");
        Line("CALSCALE:GREGORIAN");
        Line("METHOD:PUBLISH");

        var stamp = stampUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");
        foreach (var shift in events)
        {
            Line("BEGIN:VEVENT");
            // One shift per day: re-importing the same day yields the same UID, so calendars can update instead of duplicating.
            Line($"UID:dienst-{shift.Day:yyyyMMdd}@dienstplan.codeinside.eu");
            Line($"DTSTAMP:{stamp}");
            Line($"DTSTART;VALUE=DATE:{shift.Day:yyyyMMdd}");
            Line($"DTEND;VALUE=DATE:{shift.Day.AddDays(1):yyyyMMdd}");
            Line("TRANSP:TRANSPARENT");
            Line($"SUMMARY:{Escape(shift.Title)}");
            Line("END:VEVENT");
        }

        Line("END:VCALENDAR");
        return sb.ToString();
    }

    private static string Escape(string text) => text
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\n", "\\n");

    // Lines longer than 75 octets must be folded; never split inside a UTF-8 sequence.
    private static string Fold(string line)
    {
        if (Encoding.UTF8.GetByteCount(line) <= 75)
            return line;

        var sb = new StringBuilder();
        var octets = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            if (octets + rune.Utf8SequenceLength > 75)
            {
                sb.Append("\r\n ");
                octets = 1;
            }
            sb.Append(rune.ToString());
            octets += rune.Utf8SequenceLength;
        }
        return sb.ToString();
    }
}
