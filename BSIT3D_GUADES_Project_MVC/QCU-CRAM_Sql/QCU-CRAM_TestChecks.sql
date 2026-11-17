USE [QCU-CRAM_DB];
GO

-- ============================================================
-- QCU-CRAM Step 36 - read-only checks to confirm test results
-- Run in SSMS after each test group. Nothing here changes data.
-- ============================================================

-- Latest 15 reservations (T9, T10, T11, T13-T16)
SELECT TOP 15 r.ReservationID, s.StudentNumber, rm.RoomName, r.ReservationDate,
       ts.StartTime, r.NumberOfMembers, r.Status, r.CheckedInAt
FROM Reservations r
JOIN Students s   ON s.StudentID  = r.StudentID
JOIN Rooms rm     ON rm.RoomID    = r.RoomID
JOIN TimeSlots ts ON ts.TimeSlotID = r.TimeSlotID
ORDER BY r.ReservationID DESC;

-- T9 / T10: must return ZERO rows (no two active bookings for the same room+date+slot)
SELECT RoomID, ReservationDate, TimeSlotID, COUNT(*) AS ActiveCount
FROM Reservations
WHERE Status NOT IN ('Declined','Cancelled','Forfeited')
GROUP BY RoomID, ReservationDate, TimeSlotID
HAVING COUNT(*) > 1;

-- T8: must return ZERO rows (max 1 active booking per student per date)
SELECT StudentID, ReservationDate, COUNT(*) AS ActiveCount
FROM Reservations
WHERE Status NOT IN ('Declined','Cancelled','Forfeited')
GROUP BY StudentID, ReservationDate
HAVING COUNT(*) > 1;

-- T6 / T7: must return ZERO rows (member count matches member rows, and is 5-10)
SELECT r.ReservationID, r.NumberOfMembers, COUNT(m.MemberID) AS MemberRows
FROM Reservations r
LEFT JOIN ReservationMembers m ON m.ReservationID = r.ReservationID
GROUP BY r.ReservationID, r.NumberOfMembers
HAVING r.NumberOfMembers <> COUNT(m.MemberID)
    OR r.NumberOfMembers NOT BETWEEN 5 AND 10;

-- T11 / T13 / T14 / T16 / T20 / T21: status history trail for the 10 newest reservations
SELECT h.ReservationID, h.OldStatus, h.NewStatus, h.ChangedByRole, h.ChangedAt, h.Notes
FROM ReservationStatusHistory h
WHERE h.ReservationID IN (SELECT TOP 10 ReservationID FROM Reservations ORDER BY ReservationID DESC)
ORDER BY h.ReservationID DESC, h.HistoryID;

-- Every status change should have a history row: must return ZERO rows
SELECT r.ReservationID, r.Status
FROM Reservations r
WHERE NOT EXISTS (SELECT 1 FROM ReservationStatusHistory h
                  WHERE h.ReservationID = r.ReservationID AND h.NewStatus = r.Status);

-- T11 / T13 / T20 / T22 / T25: newest notifications
SELECT TOP 15 n.NotificationID, s.StudentNumber, n.ReservationID, n.Message, n.IsRead, n.CreatedAt
FROM Notifications n
JOIN Students s ON s.StudentID = n.StudentID
ORDER BY n.NotificationID DESC;

-- T20 / T22 / T24 / T25: strikes and restriction state per student
SELECT StudentNumber, LastName, NoShowCount, IsRestricted, RestrictionLiftDate
FROM Students
WHERE NoShowCount > 0 OR IsRestricted = 1
ORDER BY IsRestricted DESC, NoShowCount DESC;

-- T18: every CheckedIn/Completed row must have CheckedInAt (ZERO rows expected)
SELECT ReservationID, Status FROM Reservations
WHERE Status IN ('CheckedIn','Completed') AND CheckedInAt IS NULL;

-- T26: confirm the setting values the sweep reads
SELECT SettingKey, SettingValue FROM Settings ORDER BY SettingKey;

-- T27: inactive rooms that still have future bookings (should still display in the app)
SELECT rm.RoomName, COUNT(*) AS FutureBookings
FROM Rooms rm
JOIN Reservations r ON r.RoomID = rm.RoomID
WHERE rm.IsActive = 0 AND r.ReservationDate >= CAST(GETDATE() AS DATE)
GROUP BY rm.RoomName;