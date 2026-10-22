Feature: Booking meeting rooms
  Rooms are open 08:00–20:00. A booking lasts 15 minutes to 4 hours on a 15-minute grid,
  never has more people than seats, and never overlaps another booking of the same room.

  Background:
    Given today is 2026-10-01

  Scenario: A free slot is booked
    When I book Tundra on 2026-10-02 from 10:00 to 11:00 for 4 people as "Planning"
    Then the booking is confirmed
    And Tundra on 2026-10-02 has these bookings:
      | start | end   | title    |
      | 10:00 | 11:00 | Planning |

  Scenario: Back-to-back meetings are fine, overlapping ones are not
    Given Tundra is booked on 2026-10-02 from 10:00 to 11:00 as "Planning"
    When I book Tundra on 2026-10-02 from 11:00 to 12:00 for 4 people as "Review"
    Then the booking is confirmed
    When I book Tundra on 2026-10-02 from 11:30 to 12:30 for 4 people as "Standup"
    Then the booking is refused because the room is taken by "Review"

  Scenario Outline: Opening hours and length
    When I book Aurora on 2026-10-02 from <start> to <end> for 4 people as "Workshop"
    Then the answer is <status>

    Examples:
      | start | end   | status |
      | 08:00 | 08:15 | 201    |
      | 07:45 | 08:15 | 400    |
      | 16:00 | 20:00 | 201    |
      | 16:00 | 20:15 | 400    |
      | 09:00 | 13:15 | 400    |
      | 09:10 | 10:00 | 400    |

  Scenario: Too many people for the room
    When I book Fjord on 2026-10-02 from 10:00 to 11:00 for 5 people as "All hands"
    Then the booking is refused with "Fjord seats 4."

  Scenario: Yesterday cannot be booked, and tomorrow becomes yesterday
    When I book Tundra on 2026-09-30 from 10:00 to 11:00 for 2 people as "Too late"
    Then the answer is 400
    Given today is 2026-10-03
    When I book Tundra on 2026-10-02 from 10:00 to 11:00 for 2 people as "Also too late"
    Then the answer is 400

  Scenario: A cancelled slot can be booked again
    Given Tundra is booked on 2026-10-02 from 10:00 to 11:00 as "Planning"
    When I cancel "Planning"
    And I book Tundra on 2026-10-02 from 10:00 to 11:00 for 4 people as "Replanning"
    Then the booking is confirmed
