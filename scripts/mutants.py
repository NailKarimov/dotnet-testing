"""Mutation check: plants one bug at a time (in the API or in the page) and runs the whole suite.

python3 scripts/mutants.py           # all
python3 scripts/mutants.py M3 M8     # some
"""

import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
APP = "src/BookingApp/"
MUTANTS = [
    ("M1", "Bookings.cs", "b.Start < end && start < b.End", "b.Start <= end && start <= b.End", "touching bookings clash"),
    ("M2", "Bookings.cs", "MaxMinutes = 240;", "MaxMinutes = 255;", "bookings up to 4 h 15 min"),
    ("M3", "Bookings.cs", "start < Opens || end > Closes", "start < Opens", "bookings after closing time"),
    ("M4", "Bookings.cs", "date < Today", "date < Today.AddDays(-1)", "yesterday can be booked"),
    ("M5", "Bookings.cs", "r.Attendees > room.Capacity", "r.Attendees > room.Capacity + 1", "one person more than the seats"),
    ("M6", "Bookings.cs", "start.Minute % 15 != 0 || end.Minute % 15 != 0", "false", "no 15-minute grid"),
    ("M7", "Bookings.cs", "RemoveAll(b => b.Id == id) > 0", "RemoveAll(b => b.Id == id) >= 0", "cancelling twice is 204"),
    ("M8", "Bookings.cs", "b.RoomId == room!.Id && ", "", "a booking blocks every room"),
    ("M9", "Bookings.cs", "title.Length > 100", "title.Length > 200", "titles up to 200 characters"),
    ("M10", "App.cs", 'ctx.Response.Headers.XContentTypeOptions = "nosniff";', "", "nosniff header dropped"),
    ("M11", "wwwroot/index.html", "text.append(document.createTextNode(`${b.title} · ${b.attendees} people`));",
     "text.innerHTML += `${b.title} · ${b.attendees} people`;", "the page renders titles as HTML (XSS)"),
    ("M12", "wwwroot/index.html", "$('error').textContent = (res.status === 409 ? 'Already booked. ' : '') + messages.join(' ');",
     "$('error').textContent = 'Something went wrong.';", "the page hides the server's message"),
]


def main(selected: set[str]) -> int:
    survived = []
    for mid, file, old, new, what in MUTANTS:
        if selected and mid not in selected:
            continue
        path = ROOT / APP / file
        original = path.read_text()
        if original.count(old) != 1:
            raise SystemExit(f"{mid}: pattern must occur exactly once in {file}")
        path.write_text(original.replace(old, new))
        try:
            r = subprocess.run(
                ["dotnet", "test", "tests/BookingApp.Tests", "--nologo", "--logger", "console;verbosity=normal"],
                cwd=ROOT, capture_output=True, text=True, check=False,
            )
        finally:
            path.write_text(original)
        caught = r.returncode != 0
        failed = sorted({m.group(1) for m in re.finditer(r"^\s*Failed (\S+)", r.stdout, re.M)})[:3]
        print(f"| {mid} | {what} | {'caught' if caught else '**SURVIVED**'} | {', '.join(failed)} |", flush=True)
        if not caught:
            survived.append(mid)
    print(f"\n{len(survived)} survived: {' '.join(survived)}" if survived else "\nAll mutants caught.")
    return 1 if survived else 0


if __name__ == "__main__":
    print("| # | Bug | Result | First failing tests |\n| --- | --- | --- | --- |")
    sys.exit(main(set(sys.argv[1:])))
