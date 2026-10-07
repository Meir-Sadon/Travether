using System.Globalization;
using System.Text;
using Travether.Api.Domain;

namespace Travether.Api.Plans;

/// <summary>
/// An iCalendar (RFC 5545) event for a plan (PLAN.md §4.3, "add to calendar"). Built from the viewer's
/// <see cref="PlanDto"/>, so the exact meeting point appears only for those allowed to see it.
/// </summary>
public static class PlanCalendar
{
    /// <summary>Plans have a start but no end; calendars get a typical length for the category.</summary>
    public static TimeSpan DurationOf(PlanCategory category) => category switch
    {
        PlanCategory.Hike or PlanCategory.Tour or PlanCategory.Beach => TimeSpan.FromHours(4),
        PlanCategory.DayTrip => TimeSpan.FromHours(8),
        PlanCategory.Nightlife => TimeSpan.FromHours(3),
        _ => TimeSpan.FromHours(2),
    };

    public static string Build(PlanDto plan, string planUrl, DateTimeOffset now)
    {
        var location = plan.MeetingPoint is { } m ? $"{m.Name}, {plan.AreaLabel}" : plan.AreaLabel;
        var description = new StringBuilder();
        if (plan.Destination is { } destination)
        {
            description.Append(CultureInfo.InvariantCulture, $"To: {destination}\n");
        }

        if (plan.Purpose is { } purpose)
        {
            description.Append(purpose).Append('\n');
        }

        description.Append(CultureInfo.InvariantCulture, $"Hosted by {plan.Host.DisplayName} on Travether: {planUrl}");

        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//Travether//Plans//EN",
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
            "BEGIN:VEVENT",
            $"UID:plan-{plan.Id:N}@travether",
            $"DTSTAMP:{Utc(now)}",
            $"DTSTART:{Utc(plan.StartsAt)}",
            $"DTEND:{Utc(plan.StartsAt + DurationOf(plan.Category))}",
            $"SUMMARY:{Escape(plan.Title)}",
            $"LOCATION:{Escape(location)}",
            $"DESCRIPTION:{Escape(description.ToString())}",
            $"URL:{planUrl}",
            $"STATUS:{(plan.Status == PlanStatus.Cancelled ? "CANCELLED" : "CONFIRMED")}",
        };
        if (plan.MeetingPoint is { } point)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"GEO:{point.Lat:0.######};{point.Lng:0.######}"));
        }

        if (plan.Status != PlanStatus.Cancelled)
        {
            lines.AddRange(["BEGIN:VALARM", "ACTION:DISPLAY", $"DESCRIPTION:{Escape(plan.Title)}", "TRIGGER:-PT1H", "END:VALARM"]);
        }

        lines.AddRange(["END:VEVENT", "END:VCALENDAR"]);

        var ics = new StringBuilder();
        foreach (var line in lines)
        {
            Fold(ics, line);
        }

        return ics.ToString();
    }

    /// <summary>"Sunrise hike to Doi Suthep" → "sunrise-hike-to-doi-suthep", for the download name.</summary>
    public static string FileName(string title)
    {
        var slug = new StringBuilder();
        foreach (var c in title.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var name = slug.ToString().Trim('-');
        return name.Length == 0 ? "travether-plan" : name[..Math.Min(name.Length, 60)].TrimEnd('-');
    }

    private static string Utc(DateTimeOffset t) => t.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    /// <summary>TEXT values escape backslash, semicolon, comma and newlines.</summary>
    public static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal);

    /// <summary>Lines end in CRLF and fold at 75 octets without splitting a UTF-8 character.</summary>
    private static void Fold(StringBuilder ics, string line)
    {
        var limit = 75;
        var bytes = 0;
        var i = 0;
        while (i < line.Length)
        {
            var step = char.IsHighSurrogate(line[i]) && i + 1 < line.Length ? 2 : 1;
            var size = Encoding.UTF8.GetByteCount(line.AsSpan(i, step));
            if (bytes + size > limit)
            {
                ics.Append("\r\n ");
                bytes = 1;
                limit = 75;
            }

            ics.Append(line, i, step);
            bytes += size;
            i += step;
        }

        ics.Append("\r\n");
    }
}
