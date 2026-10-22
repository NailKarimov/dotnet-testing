using System.Text.RegularExpressions;
using BookingApp.Tests.Support;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace BookingApp.Tests.Ui;

/// <summary>
/// The booking page in Chromium, against the real app on Kestrel. Locators by role and label only, the way a
/// person (or a screen reader) finds things. Every test uses its own day, so tests never see each other's bookings.
/// </summary>
[TestFixture]
[Category("ui")]
[Parallelizable(ParallelScope.None)]
public class BookingPageTests : PageTest
{
    private static RunningApp _app = null!;
    private static int _day = 2;

    [OneTimeSetUp]
    public async Task StartApp() => _app = await RunningApp.StartAsync(new FakeClock(BookingFactory.Now));

    [OneTimeTearDown]
    public async Task StopApp() => await _app.DisposeAsync();

    private string _date = "";

    [SetUp]
    public async Task Open()
    {
        _date = $"2026-10-{Interlocked.Increment(ref _day):00}";
        await Context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = false });
        await Page.GotoAsync(_app.Url);
        await Expect(Page.Locator("body[data-ready=true]")).ToBeAttachedAsync();
        await Field("Date").FillAsync(_date);
        await Field("Date").DispatchEventAsync("change");
    }

    [TearDown]
    public async Task SaveTraceOnFailure()
    {
        var failed = TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed;
        var dir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "traces");
        Directory.CreateDirectory(dir);
        await Context.Tracing.StopAsync(new() { Path = failed ? Path.Combine(dir, $"{TestContext.CurrentContext.Test.Name}.zip") : null });
    }

    private ILocator Field(string label) => Page.GetByLabel(label, new() { Exact = true });

    private ILocator List => Page.GetByRole(AriaRole.List, new() { Name = "Bookings" });

    private async Task Book(string room, string start, string end, string title, int people)
    {
        await Field("Room").SelectOptionAsync(new SelectOptionValue { Label = room });
        await Field("Start").FillAsync(start);
        await Field("End").FillAsync(end);
        await Field("Attendees").FillAsync(people.ToString());
        await Field("Title").FillAsync(title);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Book", Exact = true }).ClickAsync();
    }

    [Test]
    public async Task ABooking_AppearsInTheList()
    {
        await Book("Tundra (8 seats)", "10:00", "11:00", "Retro", 4);
        await Expect(Page.GetByText("Booked: Retro")).ToBeVisibleAsync();
        await Expect(List.GetByRole(AriaRole.Listitem)).ToHaveTextAsync(new[] { new Regex("10:00–11:00 Retro · 4 people") });
        await Expect(Page.GetByText("No bookings yet.")).ToBeHiddenAsync();
    }

    [Test]
    public async Task AClash_IsExplainedInAnAlert()
    {
        await Book("Tundra (8 seats)", "10:00", "11:00", "Retro", 4);
        await Book("Tundra (8 seats)", "10:30", "11:30", "Standup", 3);
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Already booked. Booked 10:00–11:00: Retro");
        await Expect(List.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(1);
    }

    [Test]
    public async Task TheServersValidationMessage_IsShown()
    {
        await Book("Fjord (4 seats)", "10:00", "11:00", "All hands", 9);
        await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveTextAsync("Fjord seats 4.");
    }

    [Test]
    public async Task Cancel_RemovesTheBooking()
    {
        await Book("Aurora (16 seats)", "15:00", "16:00", "Demo", 10);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel Demo" }).ClickAsync();
        await Expect(Page.GetByText("Cancelled: Demo")).ToBeVisibleAsync();
        await Expect(Page.GetByText("No bookings yet.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task AnotherRoom_HasItsOwnList()
    {
        await Book("Tundra (8 seats)", "10:00", "11:00", "Retro", 4);
        await Field("Room").SelectOptionAsync(new SelectOptionValue { Label = "Fjord (4 seats)" });
        await Expect(Page.GetByText("No bookings yet.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ATitleWithMarkup_IsShownAsText()
    {
        const string title = "<img src=x onerror=window.pwned=1>";
        await Book("Tundra (8 seats)", "12:00", "13:00", title, 2);
        await Expect(List).ToContainTextAsync(title);
        await Expect(List.Locator("img")).ToHaveCountAsync(0);
        Assert.That(await Page.EvaluateAsync<bool>("() => window.pwned === 1"), Is.False);
    }

    [Test]
    public async Task EveryControl_HasAnAccessibleName()
    {
        foreach (var label in new[] { "Room", "Date", "Start", "End", "Attendees", "Title" })
        {
            await Expect(Page.GetByLabel(label, new() { Exact = true })).ToBeVisibleAsync();
        }
        await Expect(Page.GetByRole(AriaRole.Form, new() { Name = "Book a room" })).ToBeVisibleAsync();
    }
}
