using GeoTimeZone;
using NetTopologySuite.Geometries;
using Travether.Api.Domain;
using Travether.Api.Places;

namespace Travether.Api.Plans;

/// <summary>Activity Plan validation and time handling (PLAN.md §4.3).</summary>
public static class PlanRules
{
    public const int MinSeats = 2;
    public const int MaxSeats = 100;

    private static readonly GeometryFactory Geography = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public static Point ToPoint(LatLng p) => Geography.CreatePoint(new Coordinate(p.Lng, p.Lat));

    public static LatLng ToLatLng(Point p) => new(p.Y, p.X);

    /// <summary>The IANA time zone at a point, looked up offline.</summary>
    public static string TimeZoneAt(LatLng p) => TimeZoneLookup.GetTimeZone(p.Lat, p.Lng).Result;

    /// <summary>A local wall-clock time at the meeting point as an instant. A time skipped by a DST change moves an hour on.</summary>
    public static DateTimeOffset ToInstant(DateOnly date, TimeOnly time, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    public static (DateOnly Date, TimeOnly Time) ToLocal(DateTimeOffset instant, string timeZoneId)
    {
        var local = TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
        return (DateOnly.FromDateTime(local.DateTime), TimeOnly.FromDateTime(local.DateTime));
    }

    /// <summary>Returns an error code, or null when the plan is valid.</summary>
    public static string? Validate(
        string title, string areaLabel, string destination, LatLng origin, int seatLimit, int seatsTaken,
        DateOnly localDate, DateTimeOffset startsAt, DateTimeOffset now, VacationCard card)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "TitleRequired";
        }

        if (string.IsNullOrWhiteSpace(areaLabel) || !PlaceRules.IsValid(origin.Lat, origin.Lng))
        {
            return "MeetingPointRequired";
        }

        if (string.IsNullOrWhiteSpace(destination))
        {
            return "DestinationRequired";
        }

        if (seatLimit is < MinSeats or > MaxSeats)
        {
            return "InvalidSeatLimit";
        }

        if (seatsTaken > seatLimit)
        {
            return "SeatLimitBelowParticipants";
        }

        if (startsAt < now)
        {
            return "PlanInPast";
        }

        return localDate < card.StartsOn || localDate > card.EndsOn ? "PlanOutsideTrip" : null;
    }

    /// <summary>Open and Full follow the seat count; Cancelled and Done stay.</summary>
    public static PlanStatus StatusFor(PlanStatus current, int seatsTaken, int seatLimit) =>
        current is PlanStatus.Open or PlanStatus.Full ? (seatsTaken >= seatLimit ? PlanStatus.Full : PlanStatus.Open) : current;

    public static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
