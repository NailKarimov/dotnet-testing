using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace BookingApp.Tests.Support;

/// <summary>The real app on Kestrel at a free port, for the browser. TestServer has no socket a browser could open.</summary>
public sealed class RunningApp : IAsyncDisposable
{
    private readonly WebApplication _app;

    private RunningApp(WebApplication app, string url)
    {
        _app = app;
        Url = url;
    }

    public string Url { get; }

    public static async Task<RunningApp> StartAsync(TimeProvider clock)
    {
        var app = App.Create([], s => s.AddSingleton(clock));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        return new RunningApp(app, app.Urls.First());
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
