using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;

namespace Dienstplan.Web.Calendar;

/// <summary>
/// A shift written as an all-day calendar event from <see cref="Start"/> through <see cref="End"/> (inclusive).
/// A single day has Start == End.
/// </summary>
public sealed record ShiftEvent(DateOnly Start, DateOnly End, string Title)
{
    public ShiftEvent(DateOnly day, string title) : this(day, day, title) { }
}

/// <summary>
/// Decodes the compact plan the browser sends as base64url-encoded JSON in the query string:
/// { "s": [ "Frühdienst", "Spätdienst" ], "d": { "2026-11-03": 0, "2026-11-04": 1 }, "m": true }
/// "m" merges consecutive days with the same shift into one multi-day event.
/// Keeping everything in the URL makes the .ics a plain GET request, which is what iOS Safari
/// needs to show its "Add all to calendar" sheet.
/// </summary>
public static class ShiftPlan
{
    private const int MaxDays = 100;
    private const int MaxTitleLength = 60;

    private sealed record Payload(
        [property: JsonPropertyName("s")] List<string?>? Shifts,
        [property: JsonPropertyName("d")] Dictionary<string, int>? Days,
        [property: JsonPropertyName("m")] bool Merge = false);

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

        result.Sort((a, b) => a.Start.CompareTo(b.Start));
        events = payload.Merge ? MergeConsecutive(result) : result;
        return true;
    }

    /// <summary>
    /// Joins back-to-back days with the same shift, e.g. Früh Mon + Tue + Wed becomes one event Mon–Wed.
    /// Expects the events sorted by start date.
    /// </summary>
    public static IReadOnlyList<ShiftEvent> MergeConsecutive(IEnumerable<ShiftEvent> sorted)
    {
        var merged = new List<ShiftEvent>();
        foreach (var current in sorted)
        {
            if (merged.Count > 0 && merged[^1] is var last && last.Title == current.Title && last.End.AddDays(1) == current.Start)
                merged[^1] = last with { End = current.End };
            else
                merged.Add(current);
        }
        return merged;
    }

    private static string Clean(string? value)
    {
        var text = new string((value ?? string.Empty).Where(c => !char.IsControl(c)).ToArray()).Trim();
        return text.Length > MaxTitleLength ? text[..MaxTitleLength] : text;
    }
}
