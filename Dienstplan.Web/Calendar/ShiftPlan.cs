using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;

namespace Dienstplan.Web.Calendar;

/// <summary>
/// A single shift on a given day; it is written as an all-day calendar event.
/// </summary>
public sealed record ShiftEvent(DateOnly Day, string Title);

/// <summary>
/// Decodes the compact plan the browser sends as base64url-encoded JSON in the query string:
/// { "s": [ "Frühdienst", "Spätdienst" ], "d": { "2026-11-03": 0, "2026-11-04": 1 } }
/// Keeping everything in the URL makes the .ics a plain GET request, which is what iOS Safari
/// needs to show its "Add all to calendar" sheet.
/// </summary>
public static class ShiftPlan
{
    private const int MaxDays = 100;
    private const int MaxTitleLength = 60;

    private sealed record Payload(
        [property: JsonPropertyName("s")] List<string?>? Shifts,
        [property: JsonPropertyName("d")] Dictionary<string, int>? Days);

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

            var title = Clean(shifts[index]);
            if (title.Length == 0)
                return false;

            result.Add(new ShiftEvent(day, title));
        }

        events = result.OrderBy(e => e.Day).ToList();
        return true;
    }

    private static string Clean(string? value)
    {
        var text = new string((value ?? string.Empty).Where(c => !char.IsControl(c)).ToArray()).Trim();
        return text.Length > MaxTitleLength ? text[..MaxTitleLength] : text;
    }
}
