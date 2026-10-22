# Testing in C#: a meeting-room booking app

The app is ours: an **ASP.NET Core minimal API** with one embedded page (`src/BookingApp`). Rooms are open
08:00–20:00. A booking lasts 15 minutes to 4 hours on a 15-minute grid, is not in the past, has no more people
than seats, and never overlaps another booking of the same room. Touching ends are fine: 10:00–11:00, then
11:00–12:00. Errors are RFC 7807 **ProblemDetails**: `ValidationProblem` with an `errors` dictionary for 400, and
`Problem` with the clashing booking's id for 409.

| Layer | Tool | Where | Tests |
| --- | --- | --- | ---: |
| API, in memory | NUnit + `WebApplicationFactory<Program>`, fake `TimeProvider` | `Api/` | 36 |
| Behaviour (BDD) | Reqnroll, `Booking.feature` | `Features/`, `Steps/` | 11 |
| UI | Playwright for .NET (Chromium), app on Kestrel | `Ui/` | 7 |

54 tests in about 6 seconds. Line coverage of the app is 100 % and branch coverage 90 % (coverlet). The
mutation check plants 12 bugs, and the tests catch all of them.

## 1. WebApplicationFactory: the API without a network

```csharp
public sealed class BookingFactory : WebApplicationFactory<Program>
{
    public FakeClock Clock { get; } = new(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureServices(s => s.AddSingleton<TimeProvider>(Clock));
    }
}
```

Each test gets a fresh app in memory: the real routing, binding and ProblemDetails, with no port and no shared
data. "Today" comes from `TimeProvider`, so "2026-10-02 is now in the past" is `Clock.Set(…)`, not a wait.

**Production, not Development.** In Development, `WebApplication` adds the developer exception page. A minimal
API also throws on a bad request instead of answering 400. A test that runs in a different environment from the
one that is deployed tests a different program.

The overlap rule is a table against an existing 10:00–11:00 booking:

| New booking | Expected |
| --- | --- |
| 09:00–10:00 | 201: ends when the other starts |
| 11:00–12:00 | 201: starts when the other ends |
| 09:30–10:15, 10:45–11:30 | 409: overlaps one edge |
| 10:15–10:45 | 409: inside |
| 09:00–12:00 | 409: covers |
| 10:00–11:00 | 409: the same slot |

Other tables cover times (15 minutes, 4 hours, opening and closing, the grid, end before start) and capacity per
room. Separate tests check:
- one `ValidationProblem` that names all six bad fields;
- the clock;
- cancel then rebook;
- ten parallel requests for one slot: exactly one 201;
- malformed JSON gets an answer without a stack trace;
- security headers.

## 2. Reqnroll

```gherkin
Scenario: Back-to-back meetings are fine, overlapping ones are not
  Given Tundra is booked on 2026-10-02 from 10:00 to 11:00 as "Planning"
  When I book Tundra on 2026-10-02 from 11:00 to 12:00 for 4 people as "Review"
  Then the booking is confirmed
  When I book Tundra on 2026-10-02 from 11:30 to 12:30 for 4 people as "Standup"
  Then the booking is refused because the room is taken by "Review"
```

The step class gets a fresh `BookingFactory` per scenario. Reqnroll disposes it at the end.

**A trap from the first CI run.** All 11 scenarios failed with a binding error: `today is (\d{4}-\d{2}-\d{2})`
was read as a **Cucumber expression**, and there `\d` is an invalid escape. Reqnroll treats a step text as a regular
expression only when it is anchored with `^…$`.

## 3. Playwright for .NET

The browser needs a real socket, and the in-memory TestServer has none. So the UI tests start the same app
(`App.Create`) on Kestrel at `http://127.0.0.1:0` and read the bound port back from `app.Urls`. Locators use only
roles and labels (`GetByLabel("Room", Exact = true)`, `GetByRole(AriaRole.Alert)`). Every test gets its own day,
so tests never see each other's bookings. The CI publishes a Playwright trace (`.zip`) for each failed test.

| Test | Checks |
| --- | --- |
| a booking appears in the list | status text, list item `10:00–11:00 Retro · 4 people` |
| a clash is explained | `role=alert`: "Already booked. Booked 10:00–11:00: Retro" |
| the server's validation message is shown | "Fjord seats 4." comes from the API's ProblemDetails |
| cancel | the list is empty again |
| a title with markup is text | `<img onerror=…>` shows as text, no `<img>`, no script ran |
| every control has an accessible name | six labels and a named form |

**The labels had to change.** A `<label>` wrapped around a field gives the field an accessible name that includes
the field's own value: "Room Fjord (4 seats)", "Start 10:00". A screen reader reads it that way, and an exact
`GetByLabel("Room")` cannot find the field. The fix is a label with `for=`, which gives the field its name and
nothing more.

## 4. Mutation check

`python3 scripts/mutants.py` plants one bug at a time, in the API or in the page, and runs `dotnet test`. It runs
as a CI job and publishes the result on Pages (`mutants/result.md`).

| # | Bug | Caught by |
| --- | --- | --- |
| M1 | touching bookings clash | overlap table, Reqnroll |
| M2 | bookings up to 4 h 15 min | times table, Reqnroll |
| M3 | bookings after closing time | times table, Reqnroll |
| M4 | yesterday can be booked | past dates, Reqnroll |
| M5 | one person more than the seats | capacity table |
| M6 | no 15-minute grid | times table, Reqnroll |
| M7 | cancelling twice is 204 | cancel test |
| M8 | a booking blocks every room | another room / another day |
| M9 | titles up to 200 characters | title length table |
| M10 | nosniff header dropped | security headers |
| M11 | the page renders titles as HTML (XSS) | UI: a title with markup |
| M12 | the page hides the server's message | UI: clash and validation alerts |

M11 and M12 live in the page, not the API, so only the browser tests can catch them.

## Running it

```bash
dotnet build tests/BookingApp.Tests
pwsh tests/BookingApp.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test tests/BookingApp.Tests
dotnet test tests/BookingApp.Tests --filter Category=api   # only the in-memory API tests
python3 scripts/mutants.py
```
