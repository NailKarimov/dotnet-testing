| # | Bug | Result | First failing tests |
| --- | --- | --- | --- |
| M1 | touching bookings clash | caught | Back_To_BackMeetingsAreFineOverlappingOnesAreNot, ends, starts |
| M2 | bookings up to 4 h 15 min | caught | 4, OpeningHoursAndLength("09:00","13:15","400",null) |
| M3 | bookings after closing time | caught | ends |
| M4 | yesterday can be booked | caught | YesterdayCannotBeBookedAndTomorrowBecomesYesterday, yesterday |
| M5 | one person more than the seats | caught | Fjord, TooManyPeopleForTheRoom |
| M6 | no 15-minute grid | caught |  |
| M7 | cancelling twice is 204 | caught | Cancel_FreesTheSlot_AndIsNotFoundTheSecondTime |
| M8 | a booking blocks every room | caught | SameSlot_InAnotherRoom_OrOnAnotherDay_IsFine |
| M9 | titles up to 200 characters | caught | a |
| M10 | nosniff header dropped | caught | SecurityHeaders |
| M11 | the page renders titles as HTML (XSS) | caught | ATitleWithMarkup_IsShownAsText |
| M12 | the page hides the server's message | caught | AClash_IsExplainedInAnAlert, TheServersValidationMessage_IsShown |

All mutants caught.
