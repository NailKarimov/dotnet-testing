using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BookingApp.Tests.Support;

/// <summary>
/// The app in memory (TestServer, no sockets) with a fake clock fixed at 2026-10-01 09:00 UTC.
/// Each factory is a fresh app with an empty booking list.
/// </summary>
public sealed class BookingFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    public FakeClock Clock { get; } = new(Now);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Production, as deployed: no developer exception page, bad requests answered instead of thrown.
        builder.UseEnvironment("Production");
        builder.ConfigureServices(s => s.AddSingleton<TimeProvider>(Clock));
    }
}

public static class BookingApi
{
    public const string Tomorrow = "2026-10-02";

    public static object Body(
        int? roomId = 2, string? date = Tomorrow, string? start = "10:00", string? end = "11:00",
        string? title = "Planning", int? attendees = 4) =>
        new { roomId, date, start, end, title, attendees };

    public static Task<HttpResponseMessage> Book(this HttpClient http, object body) =>
        http.PostAsJsonAsync("/api/bookings", body);
}
