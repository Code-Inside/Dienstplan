using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;

namespace Dienstplan.Web.Calendar;

/// <summary>
/// A single shift on a given day. Without start/end it becomes an all-day event (e.g. "Urlaub").
/// </summary>
public sealed record ShiftEvent(DateOnly Day, string Title, TimeOnly? Start, TimeOnly? End);

/// <summary>
/// Decodes the compact plan the browser sends as base64url-encoded JSON in the query string:
/// { "s": [ { "n": "Frühdienst", "b": "06:00", "e": "14:00" } ], "d": { "2026-11-03": 0 } }
/// Keeping everything in the URL makes the .ics a plain GET request, which is what iOS Safari
/// needs to show its "Add all to calendar" sheet.
/// </summary>
public static class ShiftPlan
{
    private const int MaxDays = 100;
    private const int MaxTitleLength = 60;

    private sealed record Payload(
        [property: JsonPropertyName("s")] List<ShiftDto>? Shifts,
        [property: JsonPropertyName("d")] Dictionary<string, int>? Days);

    private sealed record ShiftDto(
        [property: JsonPropertyName("n")] string? Name,
        [property: JsonPropertyName("b")] string? Start,
        [property: JsonPropertyName("e")] string? End);

    public static bool TryDecode(string? encoded, out IReadOnlyList<ShiftEvent> events)
    {
        events = [];
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > 16_000)
            return false;

        Payload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(WebEncoders.Base64UrlDecode(encoded));
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return false;
        }

        if (payload?.Shifts is not { Count: > 0 } shifts || payload.Days is not { Count: > 0 and <= MaxDays } days)
            return false;

        var result = new List<ShiftEvent>();
        foreach (var (dateText, index) in days)
        {
            if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                return false;
            if (index < 0 || index >= shifts.Count)
                return false;

            var shift = shifts[index];
            var title = Clean(shift.Name);
            if (title.Length == 0)
                return false;

            var start = ParseTime(shift.Start);
            var end = ParseTime(shift.End);
            // Only timed if both are given, otherwise fall back to all-day.
            if (start is null || end is null)
                start = end = null;

            result.Add(new ShiftEvent(day, title, start, end));
        }

        events = result.OrderBy(e => e.Day).ToList();
        return true;
    }

    private static TimeOnly? ParseTime(string? value) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;

    private static string Clean(string? value)
    {
        var text = new string((value ?? string.Empty).Where(c => !char.IsControl(c)).ToArray()).Trim();
        return text.Length > MaxTitleLength ? text[..MaxTitleLength] : text;
    }
}
