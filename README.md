# dotnet-testing

[![Tests](https://github.com/NailKarimov/dotnet-testing/actions/workflows/tests.yml/badge.svg)](https://github.com/NailKarimov/dotnet-testing/actions/workflows/tests.yml)
[![Allure report](https://img.shields.io/badge/report-Allure-orange)](https://nailkarimov.github.io/dotnet-testing/)
[![Release](https://img.shields.io/github/v/release/NailKarimov/dotnet-testing)](https://github.com/NailKarimov/dotnet-testing/releases)

### ▶ Open online

| Page | What it is |
| --- | --- |
| **[Allure test report](https://nailkarimov.github.io/dotnet-testing/)** | latest CI run |
| [Planted bugs](https://nailkarimov.github.io/dotnet-testing/mutants/result.md) | mutation check result |
| [C# tests](https://nailkarimov.github.io/dotnet-testing/dotnet.html) | interactive guide |

Click these links: opening an `.html` file in the file list shows its source code, GitHub does not run it.

API, BDD and UI tests **in C#**. They run against a meeting-room booking app of our own: an ASP.NET Core minimal
API with RFC 7807 ProblemDetails and one embedded page.

| | |
| --- | --- |
| **System under test** | own booking app (`src/BookingApp`): rooms, bookings with opening hours, a 15-minute grid, capacity and overlap rules |
| **Language and tools** | C# / .NET 10 · NUnit · WebApplicationFactory · fake `TimeProvider` · Reqnroll · Playwright for .NET · coverlet |
| **Types of testing** | functional API, BDD, UI end-to-end, accessibility of the form, security (XSS, headers), concurrency |
| **Tests** | 54 (~6 s); 100 % of the app's lines and 90 % of branches covered |
| **Test quality** | 12/12 planted bugs caught (`scripts/mutants.py`, a CI job) |
| **Reports** | [Allure](https://nailkarimov.github.io/dotnet-testing/) · [mutants](https://nailkarimov.github.io/dotnet-testing/mutants/result.md) · [visual guide](https://nailkarimov.github.io/dotnet-testing/dotnet.html) |

## What is tested

| Suite | Tool | What it proves |
| --- | --- | --- |
| `Api/BookingApiTests` (36) | NUnit + WebApplicationFactory (in memory) | the overlap table: back-to-back is allowed; edges, inside and covering are refused. Also opening hours, the 15-minute grid, 15 minutes to 4 hours, capacity per room, title length, past dates decided by a fake clock, a ValidationProblem that names every bad field, 10 parallel requests for one slot giving one 201, no stack traces, security headers |
| `Features/Booking.feature` (11) | Reqnroll | the booking rules in Gherkin, including a Scenario Outline for hours and length |
| `Ui/BookingPageTests` (7) | Playwright for .NET on Kestrel | booking; clash and validation messages in `role=alert`; cancel; a title with markup stays text; every control has an accessible name |

## Findings

The form-label bug is a closed issue in [Issues](https://github.com/NailKarimov/dotnet-testing/issues?q=is%3Aissue+is%3Aclosed+label%3Abug).

- **Form labels.** A `<label>` wrapped around a field gave it the accessible name "Room Fjord (4 seats)", which
  includes the field's value. Screen readers read it that way, and an exact `GetByLabel` could not find the
  field. Fixed with `<label for>`.
- **Reqnroll binding.** The first CI run failed all 11 scenarios with a binding error. Unanchored regular
  expressions are read as Cucumber expressions, where `\d` is an invalid escape. Fixed with `^…$`.
- **Production, not Development.** In Development, ASP.NET Core adds the developer exception page and a minimal
  API throws on a bad body. The tests therefore run the app as deployed.

## Running it

Requirements: .NET 10 SDK, PowerShell (for the Playwright installer).

```bash
dotnet build tests/BookingApp.Tests
pwsh tests/BookingApp.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test tests/BookingApp.Tests                          # everything
dotnet test tests/BookingApp.Tests --filter Category=api    # in-memory API tests only
dotnet run --project src/BookingApp                         # the app by hand
python3 scripts/mutants.py                                  # mutation check
```

## Project structure

```text
src/BookingApp/              the system under test: App.Create(), BookingStore, the embedded page
tests/BookingApp.Tests/
  Support/                   BookingFactory (in memory), RunningApp (Kestrel), FakeClock
  Api/                       NUnit API tests
  Features/, Steps/          Reqnroll feature and steps
  Ui/                        Playwright page tests
scripts/mutants.py           mutation check
docs/                        test plan, test report, guides
```

## CI

`.github/workflows/tests.yml` runs on push, pull request, nightly at 06:00 UTC and on demand:
- the `test` job builds with warnings as errors, installs Chromium and runs all tests with JUnit output and
  coverage, then writes a per-class table to the summary;
- it publishes the Allure report with history, coverage and the Playwright traces of failed tests to GitHub Pages;
- the `mutants` job runs the mutation check and publishes `mutants/result.md`.

Releases: committing `.github/releases/<tag>.md` runs the Release workflow, which creates the tag and the GitHub release with that file as its notes.

## Documentation

| Document | What it is |
| --- | --- |
| [`docs/test-plan.md`](docs/test-plan.md) | test plan (ISO/IEC/IEEE 29119-3) |
| [`docs/test-report.md`](docs/test-report.md) | test summary report: latest results, findings, recommendations |
| [`docs/dotnet.md`](docs/dotnet.md) | guide: WebApplicationFactory, Reqnroll, Playwright .NET, the mutation table |
| [`docs/dotnet.html`](https://nailkarimov.github.io/dotnet-testing/dotnet.html) | the same guide, interactive: an overlap checker, in memory vs Kestrel, two traps |

## Part of a portfolio

One of 13 test automation projects by [Nail Karimov](https://github.com/NailKarimov):
[all projects](https://github.com/NailKarimov) ·
[testing map](https://nailkarimov.github.io/api-test-automation/testing-map.html) ·
[toolbox](https://nailkarimov.github.io/api-test-automation/toolbox.html).
