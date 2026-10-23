# Test plan: room booking (dotnet-testing)

The structure follows ISO/IEC/IEEE 29119-3 (test plan), shortened for one project.

## 1. Context

- **System under test:** a meeting-room booking app of our own. It is an ASP.NET Core minimal API with
  ProblemDetails errors and one embedded HTML page.
- **Goal:** show that the booking rules hold at every boundary, through the API and through the page. Show that
  the page is usable by role and label. Prove that the tests catch real bugs.
- **References:** RFC 7807 (ProblemDetails), WCAG 2.2 (names and roles of controls), ISO/IEC 25010.

## 2. Scope

| In scope | Out of scope |
| --- | --- |
| rooms, creating, listing, reading and cancelling bookings | users and authentication |
| validation: hours, grid, length, capacity, past dates, title | time zones (the app works in UTC days) |
| the page: booking, errors, cancelling, accessible names | browsers other than Chromium |
| security headers, escaping of user text | load testing |

## 3. Quality characteristics (ISO/IEC 25010)

| Characteristic | Covered by |
| --- | --- |
| Functional correctness | API tables, Reqnroll scenarios |
| Usability (accessibility, error messages) | Playwright tests by role and label; alerts with the server's text |
| Security | XSS test on the page, security headers, no stack traces |
| Reliability (integrity under concurrency) | 10 parallel requests for one slot |

## 4. Approach

- **Levels:**
  - API in memory with WebApplicationFactory: a fresh app per test, environment Production;
  - BDD through the same in-memory API;
  - UI against the same app on Kestrel.
- **Techniques:**
  - boundary values: 15 minutes and 4 hours, 08:00 and 20:00, capacity, 100 and 101 characters;
  - an overlap decision table;
  - a fake `TimeProvider` for "today".
- **Test data:** a fresh app per API test and per scenario; a separate day for each UI test.
- **Test quality:** `scripts/mutants.py` plants 12 bugs (two of them in the page) and runs `dotnet test` for each.

## 5. Risks

| Risk | Mitigation |
| --- | --- |
| The browser cannot reach an in-memory TestServer | UI tests start the app on Kestrel at a free port |
| Locators break when the markup changes | role and label locators only, which also test accessibility |
| A test in Development tests a different program | `UseEnvironment("Production")` in the factory |

## 6. Entry and exit criteria

- **Entry:** the solution builds with warnings as errors.
- **Exit:** all tests pass, and every planted bug is caught.

## 7. Deliverables

The test suite, the Allure report on GitHub Pages, [`test-report.md`](test-report.md),
[`dotnet.md`](dotnet.md) and its visual guide.
