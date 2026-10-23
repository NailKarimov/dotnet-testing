# Test summary report: room booking (dotnet-testing)

The structure follows ISO/IEC/IEEE 29119-3 (test completion report), shortened.

- **Period:** 22.10.2026 – 23.10.2026
- **Build:** `main`, latest nightly run in GitHub Actions
- **Allure report:** https://nailkarimov.github.io/dotnet-testing/

## 1. Summary

All 54 tests pass. The booking rules hold through the API, the scenarios and the page, and every planted bug is
caught. The two bugs planted in the page are caught only by the browser tests.

## 2. Results

| Metric | Value |
| --- | --- |
| Tests run | 54: 36 API, 11 Reqnroll scenarios, 7 Playwright |
| Passed / failed / skipped | 54 / 0 / 0 |
| Duration | ~6 s |
| Coverage of the app (coverlet) | 100 % of lines, 90 % of branches |
| Mutation check | 12 of 12 planted bugs caught |

## 3. Findings

| ID | Severity | Summary | Status |
| --- | --- | --- | --- |
| CS-1 | minor (accessibility) | labels wrapped around fields gave them names that included their values ("Room Fjord (4 seats)") | fixed: `<label for>` |
| CS-2 | blocker (test setup) | Reqnroll read unanchored regex steps as Cucumber expressions; all scenarios failed to bind | fixed: `^…$` |

## 4. Deviations from the plan

- The project was built and verified only in CI, because the .NET SDK could not be installed in the authoring
  environment.

## 5. Recommendations

1. Add Firefox and WebKit to the UI tests if the page grows.
2. Keep the API tests in memory and only the user journeys in the browser. That keeps the full run under ten
   seconds.
