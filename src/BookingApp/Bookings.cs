namespace BookingApp;

public sealed record Room(int Id, string Name, int Capacity);

public sealed record Booking(int Id, int RoomId, DateOnly Date, TimeOnly Start, TimeOnly End, string Title, int Attendees);

/// <summary>The body of POST /api/bookings. Strings, so the API can say exactly what is wrong with each field.</summary>
public sealed record BookingRequest(int? RoomId, string? Date, string? Start, string? End, string? Title, int? Attendees);

/// <summary>
/// Meeting-room bookings, in memory, one lock. Rules:
/// 08:00–20:00, a 15-minute grid, 15 minutes to 4 hours, not in the past, no more people than seats,
/// and no overlap with another booking of the same room (touching ends are fine: 10:00–11:00 then 11:00–12:00).
/// </summary>
public sealed class BookingStore(TimeProvider time)
{
    public static readonly TimeOnly Opens = new(8, 0);
    public static readonly TimeOnly Closes = new(20, 0);
    public const int MinMinutes = 15;
    public const int MaxMinutes = 240;

    public static readonly IReadOnlyList<Room> Rooms =
    [
        new(1, "Fjord", 4),
        new(2, "Tundra", 8),
        new(3, "Aurora", 16),
    ];

    private readonly Lock _gate = new();
    private readonly List<Booking> _bookings = [];
    private int _nextId = 1;

    public DateOnly Today => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

    public IReadOnlyList<Booking> List(int? roomId, DateOnly? date)
    {
        lock (_gate)
        {
            return _bookings
                .Where(b => (roomId is null || b.RoomId == roomId) && (date is null || b.Date == date))
                .OrderBy(b => b.Date).ThenBy(b => b.RoomId).ThenBy(b => b.Start)
                .ToList();
        }
    }

    public Booking? Find(int id)
    {
        lock (_gate)
        {
            return _bookings.FirstOrDefault(b => b.Id == id);
        }
    }

    /// <summary>Validates field by field; returns the errors or the new booking, or the booking it clashes with.</summary>
    public (Dictionary<string, string[]>? Errors, Booking? Created, Booking? Clash) Add(BookingRequest r)
    {
        var errors = new Dictionary<string, string[]>();
        void Error(string field, string message) => errors[field] = [message];

        var room = Rooms.FirstOrDefault(x => x.Id == r.RoomId);
        if (room is null) Error("roomId", "Unknown room.");

        DateOnly date = default;
        if (!DateOnly.TryParseExact(r.Date, "yyyy-MM-dd", out date)) Error("date", "Use yyyy-MM-dd.");
        else if (date < Today) Error("date", "The date is in the past.");

        TimeOnly start = default, end = default;
        bool times = true;
        if (!TimeOnly.TryParseExact(r.Start, "HH:mm", out start)) { Error("start", "Use HH:mm."); times = false; }
        if (!TimeOnly.TryParseExact(r.End, "HH:mm", out end)) { Error("end", "Use HH:mm."); times = false; }
        if (times)
        {
            if (start.Minute % 15 != 0 || end.Minute % 15 != 0) Error("start", "Times are on a 15-minute grid.");
            else if (start < Opens || end > Closes) Error("start", "Rooms are open 08:00–20:00.");
            else
            {
                var minutes = (end - start).TotalMinutes;
                if (end <= start) Error("end", "The end is not after the start.");
                else if (minutes < MinMinutes || minutes > MaxMinutes) Error("end", "A booking lasts 15 minutes to 4 hours.");
            }
        }

        var title = r.Title?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 100) Error("title", "A title of 1–100 characters is required.");

        if (r.Attendees is null || r.Attendees < 1) Error("attendees", "At least one attendee.");
        else if (room is not null && r.Attendees > room.Capacity) Error("attendees", $"{room.Name} seats {room.Capacity}.");

        if (errors.Count > 0) return (errors, null, null);

        lock (_gate)
        {
            var clash = _bookings.FirstOrDefault(b => b.RoomId == room!.Id && b.Date == date && b.Start < end && start < b.End);
            if (clash is not null) return (null, null, clash);
            var created = new Booking(_nextId++, room!.Id, date, start, end, title!, r.Attendees!.Value);
            _bookings.Add(created);
            return (null, created, null);
        }
    }

    public bool Cancel(int id)
    {
        lock (_gate)
        {
            return _bookings.RemoveAll(b => b.Id == id) > 0;
        }
    }
}
