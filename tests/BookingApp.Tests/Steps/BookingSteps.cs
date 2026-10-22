using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookingApp.Tests.Support;
using Reqnroll;

namespace BookingApp.Tests.Steps;

/// <summary>Steps for Booking.feature, through the API in memory. A fresh app per scenario.</summary>
[Binding]
public sealed class BookingSteps : IDisposable
{
    private static readonly Dictionary<string, int> RoomIds = new() { ["Fjord"] = 1, ["Tundra"] = 2, ["Aurora"] = 3 };
    private readonly BookingFactory _factory = new();
    private readonly HttpClient _http;
    private readonly Dictionary<string, int> _ids = [];
    private HttpResponseMessage? _last;
    private JsonElement _body;

    public BookingSteps() => _http = _factory.CreateClient();

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    [Given(@"^today is (\d{4}-\d{2}-\d{2})$")]
    public void Today(string date) =>
        _factory.Clock.Set(DateTimeOffset.Parse(date + "T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    [When(@"^I book (\w+) on (\S+) from (\d\d:\d\d) to (\d\d:\d\d) for (\d+) people as ""(.*)""$")]
    public async Task Book(string room, string date, string start, string end, int people, string title)
    {
        _last = await _http.PostAsJsonAsync("/api/bookings", new { roomId = RoomIds[room], date, start, end, title, attendees = people });
        _body = JsonDocument.Parse(await _last.Content.ReadAsStringAsync()).RootElement;
        if (_last.StatusCode == HttpStatusCode.Created) _ids[title] = _body.GetProperty("id").GetInt32();
    }

    [Given(@"^(\w+) is booked on (\S+) from (\d\d:\d\d) to (\d\d:\d\d) as ""(.*)""$")]
    public async Task Booked(string room, string date, string start, string end, string title)
    {
        await Book(room, date, start, end, 2, title);
        Assert.That(_last!.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [When(@"^I cancel ""(.*)""$")]
    public async Task Cancel(string title) =>
        Assert.That((await _http.DeleteAsync($"/api/bookings/{_ids[title]}")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

    [Then("the booking is confirmed")]
    public void Confirmed() => Assert.That(_last!.StatusCode, Is.EqualTo(HttpStatusCode.Created), _body.ToString());

    [Then(@"^the answer is (\d+)$")]
    public void Status(int status) => Assert.That((int)_last!.StatusCode, Is.EqualTo(status), _body.ToString());

    [Then(@"^the booking is refused because the room is taken by ""(.*)""$")]
    public void TakenBy(string title)
    {
        Assert.That(_last!.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(_body.GetProperty("clashId").GetInt32(), Is.EqualTo(_ids[title]));
    }

    [Then(@"^the booking is refused with ""(.*)""$")]
    public void RefusedWith(string message)
    {
        Assert.That(_last!.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var all = _body.GetProperty("errors").EnumerateObject().SelectMany(e => e.Value.EnumerateArray()).Select(v => v.GetString());
        Assert.That(all, Does.Contain(message));
    }

    [Then(@"^(\w+) on (\S+) has these bookings:$")]
    public async Task HasBookings(string room, string date, DataTable table)
    {
        var list = await _http.GetFromJsonAsync<JsonElement>($"/api/bookings?roomId={RoomIds[room]}&date={date}");
        var actual = list.EnumerateArray()
            .Select(b => $"{b.GetProperty("start").GetString()![..5]}|{b.GetProperty("end").GetString()![..5]}|{b.GetProperty("title").GetString()}");
        var expected = table.Rows.Select(r => $"{r["start"]}|{r["end"]}|{r["title"]}");
        Assert.That(actual, Is.EqualTo(expected));
    }
}
