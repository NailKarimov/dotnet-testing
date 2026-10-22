# dotnet-testing

[![Tests](https://github.com/NailKarimov/dotnet-testing/actions/workflows/tests.yml/badge.svg)](https://github.com/NailKarimov/dotnet-testing/actions/workflows/tests.yml)
[![Allure report](https://img.shields.io/badge/report-Allure-orange)](https://nailkarimov.github.io/dotnet-testing/)

API, BDD and UI tests **in C#** on a meeting-room booking app of our own: an ASP.NET Core minimal API with
ProblemDetails and one embedded page. **NUnit**, **WebApplicationFactory** with a fake `TimeProvider`, **Reqnroll**,
**Playwright for .NET**.

| Suite | Tool | What it proves |
| --- | --- | --- |
| `Api/BookingApiTests` | NUnit + WebApplicationFactory (in memory) | the overlap table (back-to-back allowed, edges/inside/cover refused), opening hours, 15-minute grid, 15 min–4 h, capacity per room, past dates by a fake clock, a ValidationProblem naming every bad field, 10 parallel requests for one slot → one 201, no stack traces, security headers |
| `Features/Booking.feature` | Reqnroll | the booking rules in Gherkin, including a Scenario Outline for hours and length |
| `Ui/BookingPageTests` | Playwright for .NET on Kestrel | booking, clash and validation messages in `role=alert`, cancel, a title with markup stays text, every control has an accessible name |

54 tests in about 6 s; 100 % of the app's lines and 90 % of branches covered. **Mutation check:** 12 planted
bugs, all caught — the two in the page (XSS, a hidden error message) only by the browser tests. Details:
[`docs/dotnet.md`](docs/dotnet.md) · visual guide:
[`docs/dotnet.html`](https://nailkarimov.github.io/dotnet-testing/dotnet.html).

## Running it

```bash
dotnet build tests/BookingApp.Tests
pwsh tests/BookingApp.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test tests/BookingApp.Tests
python3 scripts/mutants.py
```

Part of a QA portfolio: [github.com/NailKarimov](https://github.com/NailKarimov).
