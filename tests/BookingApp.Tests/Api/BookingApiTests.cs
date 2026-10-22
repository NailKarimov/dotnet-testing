using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookingApp.Tests.Support;
using static BookingApp.Tests.Support.BookingApi;

namespace BookingApp.Tests.Api;

/// <summary>API tests in memory through WebApplicationFactory: no port, no browser, milliseconds per test.</summary>
[TestFixture]
[Category("api")]
public class BookingApiTests
{
    private BookingFactory _factory = null!;
    private HttpClient _http = null!;

    [SetUp]
    public void Start()
    {
        _factory = new BookingFactory();
        _http = _factory.CreateClient();
    }

    [TearDown]
    public void Stop()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

    [Test]
    public async Task Booking_IsCreated_WithLocation_AndCanBeReadBack()
    {
        var res = await _http.Book(Body());
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await Json(res);
        Assert.That(res.Headers.Location!.ToString(), Is.EqualTo($"/api/bookings/{body.GetProperty("id").GetInt32()}"));
        Assert.That(body.GetProperty("start").GetString(), Is.EqualTo("10:00:00"));

        var again = await _http.GetAsync(res.Headers.Location);
        Assert.That(again.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await Json(again)).GetProperty("title").GetString(), Is.EqualTo("Planning"));
    }

    // An existing booking 10:00–11:00; does the new one clash?
    [TestCase("09:00", "10:00", false, TestName = "ends exactly when the other starts → allowed")]
    [TestCase("11:00", "12:00", false, TestName = "starts exactly when the other ends → allowed")]
    [TestCase("09:30", "10:15", true, TestName = "overlaps the start → 409")]
    [TestCase("10:45", "11:30", true, TestName = "overlaps the end → 409")]
    [TestCase("10:15", "10:45", true, TestName = "inside the other → 409")]
    [TestCase("09:00", "12:00", true, TestName = "covers the other → 409")]
    [TestCase("10:00", "11:00", true, TestName = "the same slot → 409")]
    public async Task Overlap(string start, string end, bool clash)
    {
        (await _http.Book(Body())).EnsureSuccessStatusCode();
        var res = await _http.Book(Body(start: start, end: end));
        Assert.That(res.StatusCode, Is.EqualTo(clash ? HttpStatusCode.Conflict : HttpStatusCode.Created));
        if (clash)
        {
            var p = await Json(res);
            Assert.That(p.GetProperty("title").GetString(), Is.EqualTo("The room is already booked."));
            Assert.That(p.GetProperty("clashId").GetInt32(), Is.EqualTo(1));
            Assert.That(res.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/problem+json"));
        }
    }

    [Test]
    public async Task SameSlot_InAnotherRoom_OrOnAnotherDay_IsFine()
    {
        (await _http.Book(Body())).EnsureSuccessStatusCode();
        Assert.That((await _http.Book(Body(roomId: 3))).StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That((await _http.Book(Body(date: "2026-10-03"))).StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [TestCase("10:00", "10:15", true, TestName = "15 minutes, the minimum → 201")]
    [TestCase("10:00", "14:00", true, TestName = "4 hours, the maximum → 201")]
    [TestCase("10:00", "14:15", false, TestName = "4 h 15 min → 400")]
    [TestCase("08:00", "09:00", true, TestName = "from opening time → 201")]
    [TestCase("19:00", "20:00", true, TestName = "until closing time → 201")]
    [TestCase("07:45", "08:30", false, TestName = "starts before 08:00 → 400")]
    [TestCase("19:30", "20:15", false, TestName = "ends after 20:00 → 400")]
    [TestCase("10:05", "11:00", false, TestName = "off the 15-minute grid → 400")]
    [TestCase("11:00", "10:00", false, TestName = "ends before it starts → 400")]
    [TestCase("10:00", "10:00", false, TestName = "zero length → 400")]
    [TestCase("1000", "11:00", false, TestName = "not HH:mm → 400")]
    public async Task Times(string start, string end, bool ok)
    {
        var res = await _http.Book(Body(start: start, end: end));
        Assert.That(res.StatusCode, Is.EqualTo(ok ? HttpStatusCode.Created : HttpStatusCode.BadRequest));
    }

    [TestCase(1, 4, true, TestName = "Fjord with 4 people, its capacity → 201")]
    [TestCase(1, 5, false, TestName = "Fjord with 5 people → 400")]
    [TestCase(3, 16, true, TestName = "Aurora with 16 → 201")]
    [TestCase(2, 0, false, TestName = "nobody → 400")]
    public async Task Capacity(int room, int people, bool ok)
    {
        var res = await _http.Book(Body(roomId: room, attendees: people));
        Assert.That(res.StatusCode, Is.EqualTo(ok ? HttpStatusCode.Created : HttpStatusCode.BadRequest));
        if (!ok)
        {
            Assert.That((await Json(res)).GetProperty("errors").TryGetProperty("attendees", out _), Is.True);
        }
    }

    [Test]
    public async Task ValidationProblem_NamesEveryBadField()
    {
        var res = await _http.Book(Body(roomId: 9, date: "02.10.2026", start: "x", end: "y", title: " ", attendees: null));
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var errors = (await Json(res)).GetProperty("errors");
        Assert.That(
            errors.EnumerateObject().Select(e => e.Name),
            Is.EquivalentTo(new[] { "roomId", "date", "start", "end", "title", "attendees" }));
    }

    [TestCase(1, true, TestName = "a 1-character title → 201")]
    [TestCase(100, true, TestName = "a 100-character title → 201")]
    [TestCase(101, false, TestName = "a 101-character title → 400")]
    public async Task TitleLength(int length, bool ok)
    {
        var res = await _http.Book(Body(title: new string('t', length)));
        Assert.That(res.StatusCode, Is.EqualTo(ok ? HttpStatusCode.Created : HttpStatusCode.BadRequest));
    }

    [TestCase("2026-09-30", false, TestName = "yesterday → 400")]
    [TestCase("2026-10-01", true, TestName = "today → 201")]
    public async Task PastDates(string date, bool ok)
    {
        var res = await _http.Book(Body(date: date));
        Assert.That(res.StatusCode, Is.EqualTo(ok ? HttpStatusCode.Created : HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task TheClockDecidesWhatIsPast()
    {
        _factory.Clock.Set(BookingFactory.Now.AddDays(5));
        Assert.That((await _http.Book(Body())).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), "2026-10-02 is now in the past");
    }

    [Test]
    public async Task Cancel_FreesTheSlot_AndIsNotFoundTheSecondTime()
    {
        var created = await Json(await _http.Book(Body()));
        var id = created.GetProperty("id").GetInt32();
        Assert.That((await _http.DeleteAsync($"/api/bookings/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await _http.DeleteAsync($"/api/bookings/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await _http.Book(Body())).StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task List_FiltersByRoomAndDay_InStartOrder()
    {
        await _http.Book(Body(start: "14:00", end: "15:00", title: "Late"));
        await _http.Book(Body(start: "09:00", end: "10:00", title: "Early"));
        await _http.Book(Body(roomId: 3, title: "Elsewhere"));
        var list = await _http.GetFromJsonAsync<JsonElement>($"/api/bookings?roomId=2&date={Tomorrow}");
        Assert.That(list.EnumerateArray().Select(b => b.GetProperty("title").GetString()), Is.EqualTo(new[] { "Early", "Late" }));
        Assert.That((await _http.GetAsync("/api/bookings?date=tomorrow")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task TenPeople_BookTheSameSlot_AtOnce_OneWins()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => _http.Book(Body(title: $"Team {i}"))));
        Assert.That(results.Count(r => r.StatusCode == HttpStatusCode.Created), Is.EqualTo(1));
        Assert.That(results.Count(r => r.StatusCode == HttpStatusCode.Conflict), Is.EqualTo(9));
    }

    [Test]
    public async Task MalformedJson_IsAProblem_NotAStackTrace()
    {
        var res = await _http.PostAsync("/api/bookings", new StringContent("{\"roomId\":", System.Text.Encoding.UTF8, "application/json"));
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var text = await res.Content.ReadAsStringAsync();
        Assert.That(text, Does.Not.Contain("Exception").And.Not.Contain("   at "));
    }

    [Test]
    public async Task SecurityHeaders()
    {
        var res = await _http.GetAsync("/");
        Assert.That(res.Headers.GetValues("X-Content-Type-Options").Single(), Is.EqualTo("nosniff"));
        Assert.That(res.Headers.GetValues("Content-Security-Policy").Single(), Does.StartWith("default-src 'self'"));
        Assert.That(res.Content.Headers.ContentType!.MediaType, Is.EqualTo("text/html"));
    }
}
