using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data
{
    /// <summary>
    /// Port of Guades_QCUCram_AnalyticsService plus the dashboard counters.
    ///
    /// Every query is date-ranged with @From / @To (inclusive) except the
    /// "right now" counters, which are deliberately not date-filtered.
    /// The status lists are constants from QCUCRAM_Status, never user input.
    /// </summary>
    public class QCUCRAM_ReportRepository
    {
        private readonly QCUCRAM_Db _db;

        public QCUCRAM_ReportRepository() : this(new QCUCRAM_Db()) { }

        public QCUCRAM_ReportRepository(QCUCRAM_Db db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        /// <summary>The two parameters every ranged query needs.</summary>
        private static SqlParameter[] Range(DateTime from, DateTime to)
        {
            return new[]
            {
                QCUCRAM_Db.P("@From", SqlDbType.Date, from.Date),
                QCUCRAM_Db.P("@To",   SqlDbType.Date, to.Date)
            };
        }

        // =================================================================
        //  Summary cards
        // =================================================================

        /// <summary>Total bookings whose reservation date falls in the range.</summary>
        public int TotalBookings(DateTime from, DateTime to)
        {
            return _db.Count(
                @"SELECT COUNT(*) AS Total FROM Reservations
                   WHERE ReservationDate BETWEEN @From AND @To",
                Range(from, to));
        }

        /// <summary>Currently restricted requesters - a "right now" number, not date-filtered.</summary>
        public int CurrentlyRestricted()
        {
            return _db.Count("SELECT COUNT(*) AS Total FROM Students WHERE IsRestricted = 1");
        }

        /// <summary>Average group size, null when the range holds no bookings.</summary>
        public decimal? AverageGroupSize(DateTime from, DateTime to)
        {
            QCUCRAM_AvgResult row = _db.QueryFirstOrDefault<QCUCRAM_AvgResult>(
                @"SELECT CAST(AVG(CAST(NumberOfMembers AS FLOAT)) AS DECIMAL(4,1)) AS Average
                    FROM Reservations
                   WHERE ReservationDate BETWEEN @From AND @To",
                Range(from, to));

            return row == null ? (decimal?)null : row.Average;
        }

        // =================================================================
        //  Charts
        // =================================================================

        /// <summary>1. How many bookings ended up in each status.</summary>
        public List<QCUCRAM_StatusTotal> StatusBreakdown(DateTime from, DateTime to)
        {
            return _db.Query<QCUCRAM_StatusTotal>(
                @"SELECT Status, COUNT(*) AS Total
                    FROM Reservations
                   WHERE ReservationDate BETWEEN @From AND @To
                   GROUP BY Status
                   ORDER BY Total DESC",
                Range(from, to));
        }

        /// <summary>2. Bookings per month, "2026-11" buckets, oldest first.</summary>
        public List<QCUCRAM_MonthTotal> BookingsPerMonth(DateTime from, DateTime to)
        {
            return _db.Query<QCUCRAM_MonthTotal>(
                @"SELECT FORMAT(ReservationDate, 'yyyy-MM') AS [Month], COUNT(*) AS Total
                    FROM Reservations
                   WHERE ReservationDate BETWEEN @From AND @To
                   GROUP BY FORMAT(ReservationDate, 'yyyy-MM')
                   ORDER BY 1",
                Range(from, to));
        }

        /// <summary>3. Bookings per weekday, Monday first (honours @@DATEFIRST).</summary>
        public List<QCUCRAM_DayTotal> BookingsPerWeekday(DateTime from, DateTime to)
        {
            return _db.Query<QCUCRAM_DayTotal>(
                @"SELECT DATENAME(WEEKDAY, ReservationDate) AS [Day], COUNT(*) AS Total
                    FROM Reservations
                   WHERE ReservationDate BETWEEN @From AND @To
                   GROUP BY DATENAME(WEEKDAY, ReservationDate),
                            (DATEPART(WEEKDAY, ReservationDate) + @@DATEFIRST - 2) % 7
                   ORDER BY (DATEPART(WEEKDAY, ReservationDate) + @@DATEFIRST - 2) % 7",
                Range(from, to));
        }

        /// <summary>
        /// 4. Room utilisation. Available = active slots x days in range, so the
        /// percentage is bookings over capacity-to-book. First row = most booked.
        /// </summary>
        public List<QCUCRAM_RoomUtilizationRow> RoomUtilization(DateTime from, DateTime to)
        {
            return _db.Query<QCUCRAM_RoomUtilizationRow>(
                @"SELECT r.RoomName,
                         COUNT(res.ReservationID) AS Booked,
                         (SELECT COUNT(*) FROM TimeSlots WHERE IsActive = 1)
                             * (DATEDIFF(DAY, @From, @To) + 1) AS Available,
                         CAST(100.0 * COUNT(res.ReservationID) /
                              NULLIF((SELECT COUNT(*) FROM TimeSlots WHERE IsActive = 1)
                                     * (DATEDIFF(DAY, @From, @To) + 1), 0)
                              AS DECIMAL(5,1)) AS UtilizationPct
                    FROM Rooms r
                    LEFT JOIN Reservations res ON res.RoomID = r.RoomID
                         AND res.ReservationDate BETWEEN @From AND @To
                         AND res.Status IN (" + QCUCRAM_Status.OccupiedListSql + @")
                   GROUP BY r.RoomID, r.RoomName
                   ORDER BY Booked DESC, r.RoomName",
                Range(from, to));
        }

        /// <summary>5. Peak time slots. Label is filled in C# so the SQL stays portable.</summary>
        public List<QCUCRAM_SlotTotal> PeakTimeSlots(DateTime from, DateTime to)
        {
            List<QCUCRAM_SlotTotal> rows = _db.Query<QCUCRAM_SlotTotal>(
                @"SELECT t.TimeSlotID, t.StartTime, t.EndTime, COUNT(res.ReservationID) AS Total
                    FROM TimeSlots t
                    LEFT JOIN Reservations res ON res.TimeSlotID = t.TimeSlotID
                         AND res.ReservationDate BETWEEN @From AND @To
                         AND res.Status IN (" + QCUCRAM_Status.OccupiedOrForfeitedListSql + @")
                   GROUP BY t.TimeSlotID, t.StartTime, t.EndTime
                   ORDER BY Total DESC, t.StartTime",
                Range(from, to));

            foreach (QCUCRAM_SlotTotal row in rows)
                row.Label = QCUCRAM_ReservationInfo.FormatSlot(row.StartTime, row.EndTime);

            return rows;
        }

        /// <summary>6. No-show rate: forfeitures over everything that got past Pending.</summary>
        public QCUCRAM_NoShowSummary NoShowRate(DateTime from, DateTime to)
        {
            return _db.QuerySingle<QCUCRAM_NoShowSummary>(
                @"SELECT ISNULL(SUM(CASE WHEN Status = 'Forfeited' THEN 1 ELSE 0 END), 0) AS Forfeited,
                         COUNT(*) AS ApprovedOrLater,
                         CAST(100.0 * SUM(CASE WHEN Status = 'Forfeited' THEN 1 ELSE 0 END)
                              / NULLIF(COUNT(*), 0) AS DECIMAL(5,1)) AS NoShowPct
                    FROM Reservations
                   WHERE ReservationDate BETWEEN @From AND @To
                     AND Status IN (" + QCUCRAM_Status.OccupiedOrForfeitedListSql + @")",
                Range(from, to));
        }

        /// <summary>7. Who forfeited, worst first.</summary>
        public List<QCUCRAM_NoShowStudentRow> NoShowsPerStudent(DateTime from, DateTime to)
        {
            return _db.Query<QCUCRAM_NoShowStudentRow>(
                @"SELECT s.StudentNumber,
                         s.LastName + ', ' + s.FirstName AS Student,
                         COUNT(*) AS Forfeitures
                    FROM Reservations res
                    JOIN Students s ON s.StudentID = res.StudentID
                   WHERE res.Status = 'Forfeited'
                     AND res.ReservationDate BETWEEN @From AND @To
                   GROUP BY s.StudentID, s.StudentNumber, s.LastName, s.FirstName
                   ORDER BY Forfeitures DESC, s.StudentNumber",
                Range(from, to));
        }

        /// <summary>8. Bookings per program.</summary>
        public List<QCUCRAM_ProgramTotal> BookingsPerProgram(DateTime from, DateTime to)
        {
            return _db.Query<QCUCRAM_ProgramTotal>(
                @"SELECT s.Program, COUNT(*) AS Total
                    FROM Reservations res
                    JOIN Students s ON s.StudentID = res.StudentID
                   WHERE res.ReservationDate BETWEEN @From AND @To
                   GROUP BY s.Program
                   ORDER BY Total DESC, s.Program",
                Range(from, to));
        }

        /// <summary>9. What admins actually decided, read from the audit trail.</summary>
        public List<QCUCRAM_DecisionTotal> AdminDecisions(DateTime from, DateTime to)
        {
            return _db.Query<QCUCRAM_DecisionTotal>(
                @"SELECT h.NewStatus, COUNT(*) AS Total
                    FROM ReservationStatusHistory h
                    JOIN Reservations r ON r.ReservationID = h.ReservationID
                   WHERE h.ChangedByRole = 'Admin'
                     AND h.NewStatus IN ('Approved','Declined','Reassigned')
                     AND r.ReservationDate BETWEEN @From AND @To
                   GROUP BY h.NewStatus
                   ORDER BY Total DESC",
                Range(from, to));
        }

        /// <summary>10. Top 10 repeat bookers.</summary>
        public List<QCUCRAM_StudentTotal> TopRepeatBookers(DateTime from, DateTime to)
        {
            return _db.Query<QCUCRAM_StudentTotal>(
                @"SELECT TOP 10 s.StudentID, s.StudentNumber,
                         s.LastName + ', ' + s.FirstName AS FullName,
                         COUNT(*) AS Total
                    FROM Reservations res
                    JOIN Students s ON s.StudentID = res.StudentID
                   WHERE res.ReservationDate BETWEEN @From AND @To
                   GROUP BY s.StudentID, s.StudentNumber, s.LastName, s.FirstName
                   ORDER BY Total DESC, s.StudentNumber",
                Range(from, to));
        }

        // =================================================================
        //  Dashboard
        // =================================================================

        /// <summary>
        /// The six cards at the top of the admin dashboard. "Today" comes from
        /// QCUCRAM_Clock so the Dev Tools offset is respected everywhere.
        /// </summary>
        public QCUCRAM_DashboardCounts Dashboard()
        {
            DateTime today = QCUCRAM_Clock.Today;

            QCUCRAM_DashboardCounts c = new QCUCRAM_DashboardCounts();

            // Each query gets its own parameter instance. A SqlParameter may only belong to one
            // SqlParameterCollection at a time, so sharing one across three commands throws
            // "The SqlParameter is already contained by another SqlParameterCollection."
            c.PendingToday = _db.Count(
                @"SELECT COUNT(*) AS Total FROM Reservations
                   WHERE ReservationDate = CAST(@d AS DATE) AND Status = 'Pending'",
                QCUCRAM_Db.P("@d", SqlDbType.Date, today));

            c.ApprovedToday = _db.Count(
                @"SELECT COUNT(*) AS Total FROM Reservations
                   WHERE ReservationDate = CAST(@d AS DATE) AND Status IN ('Approved','Reassigned')",
                QCUCRAM_Db.P("@d", SqlDbType.Date, today));

            c.CheckedInToday = _db.Count(
                @"SELECT COUNT(*) AS Total FROM Reservations
                   WHERE ReservationDate = CAST(@d AS DATE) AND Status = 'CheckedIn'",
                QCUCRAM_Db.P("@d", SqlDbType.Date, today));

            // Dashboard.aspx.cs CntPending / CntForfeited / CntTotal. These three were never
            // filled, so the Pending, Forfeited-today and Total cards always showed 0.
            c.Pending = _db.Count(
                "SELECT COUNT(*) AS Total FROM Reservations WHERE Status = 'Pending'");

            c.ForfeitedToday = _db.Count(
                @"SELECT COUNT(*) AS Total FROM Reservations
                   WHERE ReservationDate = CAST(@d AS DATE) AND Status = 'Forfeited'",
                QCUCRAM_Db.P("@d", SqlDbType.Date, today));

            c.TotalReservations = _db.Count("SELECT COUNT(*) AS Total FROM Reservations");

            c.TotalStudents = _db.Count("SELECT COUNT(*) AS Total FROM Students");
            c.RestrictedStudents = _db.Count("SELECT COUNT(*) AS Total FROM Students WHERE IsRestricted = 1");
            c.UnreadNotifications = _db.Count("SELECT COUNT(*) AS Total FROM Notifications WHERE IsRead = 0");

            return c;
        }
    }
}
