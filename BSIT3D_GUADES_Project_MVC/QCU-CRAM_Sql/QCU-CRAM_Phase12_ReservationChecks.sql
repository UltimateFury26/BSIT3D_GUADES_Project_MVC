USE [QCU-CRAM_DB];
GO

-- ============================================================
-- QCU-CRAM Phase 12 - read-only evidence for reservation creation
-- Run in SSMS after each reservation test. Nothing here changes data.
-- Set @ReservationID to the id shown on the My Reservations
-- confirmation (?created=ID) for checks 1-3.
-- ============================================================

DECLARE @ReservationID INT = 0;   -- <-- put the new reservation id here

-- 1. The reservation row itself: must be Pending, 5-10 members, purpose 1-300 chars
SELECT r.ReservationID, r.StudentID, r.RoomID, r.TimeSlotID, r.ReservationDate,
       r.NumberOfMembers, LEN(r.Purpose) AS PurposeLength, r.Status, r.CreatedAt
FROM Reservations r
WHERE r.ReservationID = @ReservationID;

-- 2. Member rows: MemberRows must equal NumberOfMembers (all-or-nothing write)
SELECT r.ReservationID, r.NumberOfMembers, COUNT(m.MemberID) AS MemberRows
FROM Reservations r
LEFT JOIN ReservationMembers m ON m.ReservationID = r.ReservationID
WHERE r.ReservationID = @ReservationID
GROUP BY r.ReservationID, r.NumberOfMembers;

-- 3. First history row and the "received" notification: exactly ONE row each
--    History expected: OldStatus NULL, NewStatus Pending, ChangedByRole Student,
--                      ChangedBy = the student's id, Notes 'Request submitted'
SELECT h.HistoryID, h.OldStatus, h.NewStatus, h.ChangedByRole, h.ChangedBy, h.Notes
FROM ReservationStatusHistory h
WHERE h.ReservationID = @ReservationID;

--    Notification expected: IsRead = 0, message starts "Your request #<id> (" and ends
--    "was received and is awaiting review by the library assistant."
SELECT n.NotificationID, n.StudentID, n.IsRead, n.Message
FROM Notifications n
WHERE n.ReservationID = @ReservationID;

-- ------------------------------------------------------------
-- Whole-database checks: each must return ZERO rows (no partial writes)
-- ------------------------------------------------------------

-- 4. Reservation without exactly one NULL -> Pending history row
SELECT r.ReservationID
FROM Reservations r
WHERE (SELECT COUNT(*) FROM ReservationStatusHistory h
        WHERE h.ReservationID = r.ReservationID AND h.OldStatus IS NULL AND h.NewStatus = 'Pending') <> 1;

-- 5. Reservation with no "received" notification
SELECT r.ReservationID
FROM Reservations r
WHERE NOT EXISTS (SELECT 1 FROM Notifications n
                   WHERE n.ReservationID = r.ReservationID
                     AND n.Message LIKE 'Your request #%was received and is awaiting review by the library assistant.');

-- 6. Member row count differs from NumberOfMembers, or size outside 5-10
SELECT r.ReservationID, r.NumberOfMembers, COUNT(m.MemberID) AS MemberRows
FROM Reservations r
LEFT JOIN ReservationMembers m ON m.ReservationID = r.ReservationID
GROUP BY r.ReservationID, r.NumberOfMembers
HAVING r.NumberOfMembers <> COUNT(m.MemberID) OR r.NumberOfMembers NOT BETWEEN 5 AND 10;

-- 7. Two active bookings for the same room + date + slot
SELECT RoomID, ReservationDate, TimeSlotID, COUNT(*) AS ActiveCount
FROM Reservations
WHERE Status NOT IN ('Declined','Cancelled','Forfeited')
GROUP BY RoomID, ReservationDate, TimeSlotID
HAVING COUNT(*) > 1;

-- 8. More than one active booking per student per date
SELECT StudentID, ReservationDate, COUNT(*) AS ActiveCount
FROM Reservations
WHERE Status NOT IN ('Declined','Cancelled','Forfeited')
GROUP BY StudentID, ReservationDate
HAVING COUNT(*) > 1;

-- 9. Booking on an inactive room / slot, or over the room's capacity
--    (rows are only expected here if the room/slot was deactivated AFTER the booking)
SELECT r.ReservationID, rm.RoomName, rm.IsActive AS RoomActive, ts.IsActive AS SlotActive,
       r.NumberOfMembers, rm.MinCapacity, rm.MaxCapacity
FROM Reservations r
JOIN Rooms rm     ON rm.RoomID = r.RoomID
JOIN TimeSlots ts ON ts.TimeSlotID = r.TimeSlotID
WHERE r.NumberOfMembers NOT BETWEEN rm.MinCapacity AND rm.MaxCapacity;

-- ------------------------------------------------------------
-- Safety net: the unique active-slot index must exist and be filtered
-- Expected: one row, is_unique = 1, filter excludes Declined/Cancelled/Forfeited
-- ------------------------------------------------------------
SELECT i.name, i.is_unique, i.filter_definition
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('dbo.Reservations') AND i.name = 'UX_Res_ActiveSlot';
