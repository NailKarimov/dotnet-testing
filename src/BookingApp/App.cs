using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookingApp;

/// <summary>Builds the app. Tests call this too, to run it on Kestrel for the browser.</summary>
public static class App
{
    public static WebApplication Create(string[] args, Action<IServiceCollection>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.TryAddSingleton(TimeProvider.System);
        configure?.Invoke(builder.Services);
        builder.Services.AddSingleton<BookingStore>();
        builder.Services.AddProblemDetails();
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers.XContentTypeOptions = "nosniff";
            ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
            ctx.Response.Headers.ContentSecurityPolicy = "default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'";
            await next();
        });

        var page = ReadPage();
        app.MapGet("/", () => Results.Content(page, "text/html; charset=utf-8"));
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        var api = app.MapGroup("/api");
        api.MapGet("/rooms", () => BookingStore.Rooms);

        api.MapGet("/bookings", (int? roomId, string? date, BookingStore store) =>
        {
            DateOnly? day = null;
            if (date is not null)
            {
                if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var d))
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["date"] = ["Use yyyy-MM-dd."] });
                day = d;
            }
            return Results.Ok(store.List(roomId, day));
        });

        api.MapGet("/bookings/{id:int}", (int id, BookingStore store) =>
            store.Find(id) is { } b ? Results.Ok(b) : Results.NotFound());

        api.MapPost("/bookings", (BookingRequest body, BookingStore store) =>
        {
            var (errors, created, clash) = store.Add(body);
            if (errors is not null) return Results.ValidationProblem(errors);
            if (clash is not null)
                return Results.Problem(
                    title: "The room is already booked.",
                    detail: $"Booked {clash.Start:HH:mm}–{clash.End:HH:mm}: {clash.Title}",
                    statusCode: StatusCodes.Status409Conflict,
                    extensions: new Dictionary<string, object?> { ["clashId"] = clash.Id });
            return Results.Created($"/api/bookings/{created!.Id}", created);
        });

        api.MapDelete("/bookings/{id:int}", (int id, BookingStore store) =>
            store.Cancel(id) ? Results.NoContent() : Results.NotFound());

        return app;
    }

    private static string ReadPage()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("index.html")
            ?? throw new InvalidOperationException("index.html is not embedded");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
