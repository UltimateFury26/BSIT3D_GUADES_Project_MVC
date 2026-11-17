using System;
using System.Collections.Generic;
using System.Globalization;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data
{
    // =====================================================================
    //  Result-code DTOs.
    //  Property names must match the column aliases in the SQL, because
    //  Database.SqlQuery<T> binds by name, not by position.
    // =====================================================================

    /// <summary>Generic "0 = OK, 1..n = a named failure" answer.</summary>
    public class QCUCRAM_CodeResult
    {
        public int ResultCode { get; set; }
    }

    /// <summary>Rows affected by an INSERT / UPDATE / DELETE.</summary>
    public class QCUCRAM_AffectedResult
    {
        public int Affected { get; set; }
    }

    /// <summary>The identity of a freshly inserted row (OUTPUT INSERTED.X AS NewId).</summary>
    public class QCUCRAM_NewIdResult
    {
        public int NewId { get; set; }
    }

    /// <summary>SELECT COUNT(*) AS Total.</summary>
    public class QCUCRAM_CountResult
    {
        public int Total { get; set; }
    }

    /// <summary>
    /// Outcome of a sign-in attempt.
    /// 0 = OK, 1 = invalid credentials, 2 = account inactive / not allowed.
    /// </summary>
    public class QCUCRAM_LoginResult
    {
        public int ResultCode { get; set; }
        public int? UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }

        public bool Success { get { return ResultCode == 0 && UserId.HasValue; } }
    }

    /// <summary>
    /// Outcome of a registration attempt.
    /// 0 = OK, 1 = ID / student number taken, 2 = email taken, 3 = invalid input.
    /// </summary>
    public class QCUCRAM_RegisterResult
    {
        public int ResultCode { get; set; }
        public int? StudentId { get; set; }

        public bool Success { get { return ResultCode == 0 && StudentId.HasValue; } }
    }

    // =====================================================================
    //  Reservation details
    // =====================================================================

    /// <summary>
    /// Everything the status engine needs to know about one reservation:
    /// who owns it, what it holds, and exactly when the slot starts and ends.
    /// </summary>
    public class QCUCRAM_ReservationInfo
    {
        public int ReservationId { get; set; }
        public int StudentId { get; set; }
        public string Status { get; set; }
        public int RoomId { get; set; }
        public int TimeSlotId { get; set; }
        public string RoomName { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        /// <summary>Exact start moment of the booked slot (used by check-in and the sweep).</summary>
        public DateTime SlotStart { get { return Date.Date.Add(StartTime); } }

        /// <summary>Exact end moment of the booked slot.</summary>
        public DateTime SlotEnd { get { return Date.Date.Add(EndTime); } }

        /// <summary>"9:00 AM - 10:00 AM"</summary>
        public string SlotLabel { get { return FormatSlot(StartTime, EndTime); } }

        /// <summary>"Collab Room 3 on Oct 7, 2026 (9:00 AM - 10:00 AM)"</summary>
        public string Summary
        {
            get
            {
                return RoomName + " on " +
                       Date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) +
                       " (" + SlotLabel + ")";
            }
        }

        /// <summary>"9:00 AM - 10:00 AM" for any start/end pair.</summary>
        public static string FormatSlot(TimeSpan start, TimeSpan end)
        {
            DateTime d = DateTime.Today;
            return d.Add(start).ToString("h:mm tt", CultureInfo.InvariantCulture) + " - " +
                   d.Add(end).ToString("h:mm tt", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>One member row supplied by the Reserve form.</summary>
    public class QCUCRAM_MemberInput
    {
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string MI { get; set; }
    }

    /// <summary>Everything the Reserve form posts, in one object.</summary>
    public class QCUCRAM_CreateRequest
    {
        public int StudentId { get; set; }
        public DateTime Date { get; set; }          // DateTime.MinValue = missing / invalid
        public int RoomId { get; set; }
        public int TimeSlotId { get; set; }
        public string Purpose { get; set; }
        public int NumberOfMembers { get; set; }
        public List<QCUCRAM_MemberInput> Members { get; set; }
    }

    /// <summary>
    /// Result of ValidateAndCreate. Every rule violation is collected so the page can
    /// show them all at once instead of one at a time.
    /// </summary>
    public class QCUCRAM_CreateResult
    {
        public List<string> Errors { get; private set; }
        public int ReservationId { get; set; }

        public bool Success { get { return Errors.Count == 0 && ReservationId > 0; } }

        public QCUCRAM_CreateResult()
        {
            Errors = new List<string>();
        }
    }

    // =====================================================================
    //  Shaped rows for the availability grid and the reports
    // =====================================================================

    /// <summary>One cell of the availability grid: is this room + slot taken on that date?</summary>
    public class QCUCRAM_AvailabilityCell
    {
        public int RoomId { get; set; }
        public int TimeSlotId { get; set; }
        public int? ReservationId { get; set; }
        public string Status { get; set; }
    }

    /// <summary>Status + how many bookings had it.</summary>
    public class QCUCRAM_StatusTotal
    {
        public string Status { get; set; }
        public int Total { get; set; }
    }

    /// <summary>Month bucket ("2026-11") + how many bookings.</summary>
    public class QCUCRAM_MonthTotal
    {
        public string Month { get; set; }
        public int Total { get; set; }
    }

    /// <summary>Weekday bucket ("Monday") + how many bookings.</summary>
    public class QCUCRAM_DayTotal
    {
        public string Day { get; set; }
        public int Total { get; set; }
    }

    /// <summary>Room utilisation for the analytics page.</summary>
    public class QCUCRAM_RoomUtilizationRow
    {
        public string RoomName { get; set; }
        public int Booked { get; set; }
        public int Available { get; set; }
        public decimal? UtilizationPct { get; set; }
    }

    /// <summary>Time slot + how many bookings landed in it (peak-hours chart).</summary>
    public class QCUCRAM_SlotTotal
    {
        public int TimeSlotId { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Label { get; set; }
        public int Total { get; set; }
    }

    /// <summary>Single-value aggregate (AVG returns NULL on an empty range).</summary>
    public class QCUCRAM_AvgResult
    {
        public decimal? Average { get; set; }
    }

    /// <summary>Requester + booking count (top requesters table).</summary>
    public class QCUCRAM_StudentTotal
    {
        public int StudentId { get; set; }
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public int Total { get; set; }
    }

    /// <summary>The cards at the top of the admin dashboard.</summary>
    public class QCUCRAM_DashboardCounts
    {
        public int PendingToday { get; set; }
        public int ApprovedToday { get; set; }
        public int CheckedInToday { get; set; }
        public int TotalStudents { get; set; }
        public int RestrictedStudents { get; set; }
        public int UnreadNotifications { get; set; }
    }

    // =====================================================================
    //  Projection DTOs used by the list / audit queries.
    // =====================================================================

    /// <summary>One row of a reservation list (queue, records, daily schedule, my bookings).</summary>
    public class QCUCRAM_ReservationListItem
    {
        public int ReservationId { get; set; }
        public int StudentId { get; set; }
        public string StudentNumber { get; set; }
        public string StudentName { get; set; }
        public string RoomName { get; set; }
        public int TimeSlotId { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public DateTime ReservationDate { get; set; }
        public string Purpose { get; set; }
        public int NumberOfMembers { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CheckedInAt { get; set; }

        /// <summary>"8:00 AM - 9:00 AM", for display only.</summary>
        public string SlotLabel
        {
            get { return QCUCRAM_ReservationInfo.FormatSlot(StartTime, EndTime); }
        }
    }

    /// <summary>One row of the reservation audit trail.</summary>
    public class QCUCRAM_HistoryItem
    {
        public int HistoryId { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public string ChangedByRole { get; set; }
        public int? ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>Program + how many bookings came from it.</summary>
    public class QCUCRAM_ProgramTotal
    {
        public string Program { get; set; }
        public int Total { get; set; }
    }

    /// <summary>One-row no-show summary for the selected date range.</summary>
    public class QCUCRAM_NoShowSummary
    {
        public int Forfeited { get; set; }
        public int ApprovedOrLater { get; set; }
        public decimal? NoShowPct { get; set; }
    }

    /// <summary>One requester and how many bookings they forfeited.</summary>
    public class QCUCRAM_NoShowStudentRow
    {
        public string StudentNumber { get; set; }
        public string Student { get; set; }
        public int Forfeitures { get; set; }
    }

    /// <summary>Admin decision (Approved / Declined / Reassigned) + count.</summary>
    public class QCUCRAM_DecisionTotal
    {
        public string NewStatus { get; set; }
        public int Total { get; set; }
    }

    /// <summary>One notification row for the student inbox.</summary>
    public class QCUCRAM_NotificationItem
    {
        public int NotificationId { get; set; }
        public int? ReservationId { get; set; }
        public string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>"Nov 18, 2026 9:04 AM" for display only.</summary>
        public string WhenText
        {
            get { return CreatedAt.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture); }
        }
    }
}
