USE [QCU-CRAM_DB];
GO

-- ============================================================
-- QCU-CRAM Analytics queries (Step 33)
-- Date range filter. If you moved the Dev Tools clock, set these by hand.
-- "Occupied" = Approved, Reassigned, CheckedIn, Completed
-- ============================================================
DECLARE @From DATE = DATEADD(DAY, -30, CAST(GETDATE() AS DATE));
DECLARE @To   DATE = CAST(GETDATE() AS DATE);
-- Example: SET @From = '2026-01-01'; SET @To = '2026-12-31';

-- 1. Status breakdown
SELECT Status, COUNT(*) AS Total
FROM Reservations
WHERE ReservationDate BETWEEN @From AND @To
GROUP BY Status
ORDER BY Total DESC;

-- 2. Bookings per month
SELECT FORMAT(ReservationDate, 'yyyy-MM') AS [Month], COUNT(*) AS Total
FROM Reservations
WHERE ReservationDate BETWEEN @From AND @To
GROUP BY FORMAT(ReservationDate, 'yyyy-MM')
ORDER BY 1;

-- 3. Bookings per weekday (Monday first, independent of DATEFIRST)
SELECT DATENAME(WEEKDAY, ReservationDate) AS [Day], COUNT(*) AS Total
FROM Reservations
WHERE ReservationDate BETWEEN @From AND @To
GROUP BY DATENAME(WEEKDAY, ReservationDate),
         (DATEPART(WEEKDAY, ReservationDate) + @@DATEFIRST - 2) % 7
ORDER BY (DATEPART(WEEKDAY, ReservationDate) + @@DATEFIRST - 2) % 7;

-- 4. Room utilization (first row = most booked, last row = least booked)
SELECT r.RoomName,
       COUNT(res.ReservationID) AS Booked,
       (SELECT COUNT(*) FROM TimeSlots WHERE IsActive = 1) * (DATEDIFF(DAY, @From, @To) + 1) AS Available,
       CAST(100.0 * COUNT(res.ReservationID) /
            NULLIF((SELECT COUNT(*) FROM TimeSlots WHERE IsActive = 1) * (DATEDIFF(DAY, @From, @To) + 1), 0)
            AS DECIMAL(5,1)) AS UtilizationPct
FROM Rooms r
LEFT JOIN Reservations res ON res.RoomID = r.RoomID
     AND res.ReservationDate BETWEEN @From AND @To
     AND res.Status IN ('Approved','Reassigned','CheckedIn','Completed')
GROUP BY r.RoomID, r.RoomName
ORDER BY Booked DESC, r.RoomName;

-- 5. Peak time slot
SELECT t.StartTime, t.EndTime, COUNT(res.ReservationID) AS Total
FROM TimeSlots t
LEFT JOIN Reservations res ON res.TimeSlotID = t.TimeSlotID
     AND res.ReservationDate BETWEEN @From AND @To
     AND res.Status IN ('Approved','Reassigned','CheckedIn','Completed','Forfeited')
GROUP BY t.TimeSlotID, t.StartTime, t.EndTime
ORDER BY Total DESC, t.StartTime;

-- 6. No-show (forfeiture) rate overall
SELECT SUM(CASE WHEN Status = 'Forfeited' THEN 1 ELSE 0 END) AS Forfeited,
       COUNT(*) AS ApprovedOrLater,
       CAST(100.0 * SUM(CASE WHEN Status = 'Forfeited' THEN 1 ELSE 0 END) / NULLIF(COUNT(*), 0) AS DECIMAL(5,1)) AS NoShowPct
FROM Reservations
WHERE ReservationDate BETWEEN @From AND @To
  AND Status IN ('Approved','Reassigned','CheckedIn','Completed','Forfeited');

-- 7. No-shows per student
SELECT s.StudentNumber, s.LastName + ', ' + s.FirstName AS Student, COUNT(*) AS Forfeitures
FROM Reservations res
JOIN Students s ON s.StudentID = res.StudentID
WHERE res.Status = 'Forfeited' AND res.ReservationDate BETWEEN @From AND @To
GROUP BY s.StudentID, s.StudentNumber, s.LastName, s.FirstName
ORDER BY Forfeitures DESC, s.StudentNumber;

-- 8. Currently restricted students (not date-filtered: it is a "right now" number)
SELECT COUNT(*) AS CurrentlyRestricted FROM Students WHERE IsRestricted = 1;

-- 9. Program with most bookings
SELECT s.Program, COUNT(*) AS Total
FROM Reservations res
JOIN Students s ON s.StudentID = res.StudentID
WHERE res.ReservationDate BETWEEN @From AND @To
GROUP BY s.Program
ORDER BY Total DESC, s.Program;

-- 10. Average group size
SELECT CAST(AVG(CAST(NumberOfMembers AS FLOAT)) AS DECIMAL(4,1)) AS AvgGroupSize
FROM Reservations
WHERE ReservationDate BETWEEN @From AND @To;

-- 11. Admin decision breakdown (from the audit trail)
SELECT h.NewStatus, COUNT(*) AS Total
FROM ReservationStatusHistory h
JOIN Reservations r ON r.ReservationID = h.ReservationID
WHERE h.ChangedByRole = 'Admin'
  AND h.NewStatus IN ('Approved','Declined','Reassigned')
  AND r.ReservationDate BETWEEN @From AND @To
GROUP BY h.NewStatus
ORDER BY Total DESC;

-- 12. Top repeat bookers
SELECT TOP 10 s.StudentNumber, s.LastName + ', ' + s.FirstName AS Student, COUNT(*) AS Bookings
FROM Reservations res
JOIN Students s ON s.StudentID = res.StudentID
WHERE res.ReservationDate BETWEEN @From AND @To
GROUP BY s.StudentID, s.StudentNumber, s.LastName, s.FirstName
ORDER BY Bookings DESC, s.StudentNumber;

-- ============================================================
-- SELF-CHECK: each query's result vs. an independent count.
-- Every row should say PASS. Run together with the DECLAREs above.
-- ============================================================
DECLARE @total DECIMAL(10,1) =
    (SELECT COUNT(*) FROM Reservations WHERE ReservationDate BETWEEN @From AND @To);

SELECT [Check], FromQuery, IndependentCount,
       CASE WHEN FromQuery = IndependentCount THEN 'PASS' ELSE 'FAIL' END AS Result
FROM (
    SELECT 1 AS N, 'Q1 status breakdown adds up to all bookings' AS [Check],
        CAST(ISNULL((SELECT SUM(Total) FROM (
            SELECT COUNT(*) AS Total FROM Reservations
            WHERE ReservationDate BETWEEN @From AND @To GROUP BY Status) q), 0) AS DECIMAL(10,1)) AS FromQuery,
        @total AS IndependentCount
    UNION ALL
    SELECT 2, 'Q2 monthly counts add up to all bookings',
        CAST(ISNULL((SELECT SUM(Total) FROM (
            SELECT COUNT(*) AS Total FROM Reservations
            WHERE ReservationDate BETWEEN @From AND @To
            GROUP BY FORMAT(ReservationDate, 'yyyy-MM')) q), 0) AS DECIMAL(10,1)),
        @total
    UNION ALL
    SELECT 3, 'Q3 weekday counts add up to all bookings',
        CAST(ISNULL((SELECT SUM(Total) FROM (
            SELECT COUNT(*) AS Total FROM Reservations
            WHERE ReservationDate BETWEEN @From AND @To
            GROUP BY DATENAME(WEEKDAY, ReservationDate)) q), 0) AS DECIMAL(10,1)),
        @total
    UNION ALL
    SELECT 4, 'Q4 room "Booked" adds up to occupied bookings',
        CAST((SELECT COUNT(res.ReservationID) FROM Rooms r
              LEFT JOIN Reservations res ON res.RoomID = r.RoomID
                   AND res.ReservationDate BETWEEN @From AND @To
                   AND res.Status IN ('Approved','Reassigned','CheckedIn','Completed')) AS DECIMAL(10,1)),
        CAST((SELECT COUNT(*) FROM Reservations
              WHERE ReservationDate BETWEEN @From AND @To
                AND Status IN ('Approved','Reassigned','CheckedIn','Completed')) AS DECIMAL(10,1))
    UNION ALL
    SELECT 5, 'Q5 slot totals add up to occupied + forfeited',
        CAST((SELECT COUNT(res.ReservationID) FROM TimeSlots t
              LEFT JOIN Reservations res ON res.TimeSlotID = t.TimeSlotID
                   AND res.ReservationDate BETWEEN @From AND @To
                   AND res.Status IN ('Approved','Reassigned','CheckedIn','Completed','Forfeited')) AS DECIMAL(10,1)),
        CAST((SELECT COUNT(*) FROM Reservations
              WHERE ReservationDate BETWEEN @From AND @To
                AND Status IN ('Approved','Reassigned','CheckedIn','Completed','Forfeited')) AS DECIMAL(10,1))
    UNION ALL
    SELECT 6, 'Q6 forfeited count = Q7 per-student total',
        CAST(ISNULL((SELECT SUM(CASE WHEN Status = 'Forfeited' THEN 1 ELSE 0 END) FROM Reservations
              WHERE ReservationDate BETWEEN @From AND @To
                AND Status IN ('Approved','Reassigned','CheckedIn','Completed','Forfeited')), 0) AS DECIMAL(10,1)),
        CAST(ISNULL((SELECT SUM(Forfeitures) FROM (
              SELECT COUNT(*) AS Forfeitures FROM Reservations res
              JOIN Students s ON s.StudentID = res.StudentID
              WHERE res.Status = 'Forfeited' AND res.ReservationDate BETWEEN @From AND @To
              GROUP BY s.StudentID) q), 0) AS DECIMAL(10,1))
    UNION ALL
    SELECT 7, 'Q9 program totals add up to all bookings',
        CAST(ISNULL((SELECT SUM(Total) FROM (
            SELECT COUNT(*) AS Total FROM Reservations res
            JOIN Students s ON s.StudentID = res.StudentID
            WHERE res.ReservationDate BETWEEN @From AND @To GROUP BY s.Program) q), 0) AS DECIMAL(10,1)),
        @total
    UNION ALL
    SELECT 8, 'Q10 average group size = sum / count',
        CAST(ISNULL((SELECT CAST(AVG(CAST(NumberOfMembers AS FLOAT)) AS DECIMAL(4,1)) FROM Reservations
              WHERE ReservationDate BETWEEN @From AND @To), 0) AS DECIMAL(10,1)),
        CAST(ISNULL((SELECT CAST(SUM(NumberOfMembers) * 1.0 / COUNT(*) AS DECIMAL(4,1)) FROM Reservations
              WHERE ReservationDate BETWEEN @From AND @To), 0) AS DECIMAL(10,1))
) checks
ORDER BY N;
GO