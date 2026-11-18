using System;
using System.Collections.Generic;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>
    /// Phase 09 - the shapes the three Reports screens bind to.
    ///
    /// The Web Forms Analytics page built its seven GridViews from a
    /// Dictionary&lt;string, BarInfo&gt; and wrote the bar widths into the markup by
    /// hand. Here the same numbers arrive as plain rows, and the width is
    /// calculated once in QCUCRAMAnalyticsService.Bars() so the view only loops.
    ///
    /// The raw SQL row types are NOT redeclared here: QCUCRAM_Results.cs already
    /// owns QCUCRAM_StatusTotal, QCUCRAM_MonthTotal, QCUCRAM_DayTotal,
    /// QCUCRAM_RoomUtilizationRow, QCUCRAM_SlotTotal, QCUCRAM_NoShowSummary,
    /// QCUCRAM_NoShowStudentRow, QCUCRAM_ProgramTotal, QCUCRAM_DecisionTotal and
    /// QCUCRAM_StudentTotal. Reusing them keeps a single definition of each row
    /// and avoids the duplicate-class build error that bit the setup screens.
    /// </summary>
    public static class QCUCRAM_ReportDefaults
    {
        /// <summary>The em dash every "no value" cell shows.</summary>
        public const string Dash = "\u2014";

        /// <summary>The caption an empty bar table shows.</summary>
        public const string EmptyText = "No data for this range.";
    }

    /// <summary>One row of a horizontal bar table: a label, a number and a bar width.</summary>
    public class QCUCRAMBarRow
    {
        public string Label { get; set; }
        public int Total { get; set; }

        /// <summary>0-100. Already clamped and normalised by the service.</summary>
        public double Pct { get; set; }

        /// <summary>Optional right-hand note (room utilisation prints "12 / 54 slots (22.2%)").</summary>
        public string Note { get; set; }
    }

    /// <summary>
    /// A complete bar chart: the heading, the three column captions and the rows.
    ///
    /// The seven charts on the Analytics page differ only in their captions, so
    /// one partial (_QCU-CRAM_BarTable.cshtml) renders all of them.
    /// </summary>
    public class QCUCRAMBarTable
    {
        public string Title { get; set; }
        public string EmptyText { get; set; }
        public string LabelHeader { get; set; }
        public string ValueHeader { get; set; }
        public string BarHeader { get; set; }

        /// <summary>True for the room-utilisation table, which also shows the "12 / 54 slots" note.</summary>
        public bool ShowNote { get; set; }

        public List<QCUCRAMBarRow> Rows { get; set; }

        public QCUCRAMBarTable()
        {
            EmptyText = QCUCRAM_ReportDefaults.EmptyText;
            LabelHeader = string.Empty;
            ValueHeader = "Total";
            BarHeader = "Share";
            Rows = new List<QCUCRAMBarRow>();
        }
    }

    /// <summary>
    /// The whole Analytics screen.
    ///
    /// HasResults is false when the date range was rejected (from &gt; to, or more
    /// than ten years), in which case ErrorMessage explains why and the cards are
    /// hidden - but From/To keep whatever the assistant typed so the form does
    /// not reset.
    /// </summary>
    public class QCUCRAMAnalyticsViewModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }

        /// <summary>"custom", "last30", "month" or "all" - drives the highlighted preset button.</summary>
        public string Preset { get; set; }

        public string ErrorMessage { get; set; }
        public bool HasResults { get; set; }

        // ---- summary cards ----
        public int TotalBookings { get; set; }
        public decimal? NoShowPct { get; set; }
        public int Forfeited { get; set; }
        public int ApprovedOrLater { get; set; }
        public int CurrentlyRestricted { get; set; }
        public decimal? AverageGroupSize { get; set; }
        public string MostRoom { get; set; }
        public string LeastRoom { get; set; }
        public string PeakSlot { get; set; }

        // ---- the seven bar charts ----
        public QCUCRAMBarTable StatusBreakdown { get; set; }
        public QCUCRAMBarTable BookingsPerMonth { get; set; }
        public QCUCRAMBarTable BookingsPerWeekday { get; set; }
        public QCUCRAMBarTable PeakTimeSlots { get; set; }
        public QCUCRAMBarTable RoomUtilization { get; set; }

        /// <summary>R09: the room table has its own columns in the original (Booked / Available / Utilization % + bar).</summary>
        public List<QCUCRAM_RoomUtilizationRow> RoomRows { get; set; }
        public QCUCRAMBarTable BookingsPerProgram { get; set; }
        public QCUCRAMBarTable AdminDecisions { get; set; }

        // ---- the two plain tables ----
        public List<QCUCRAM_NoShowStudentRow> NoShowsPerStudent { get; set; }
        public List<QCUCRAM_StudentTotal> TopRepeatBookers { get; set; }

        /// <summary>"Oct 1, 2026 to Oct 31, 2026"</summary>
        public string RangeText
        {
            get
            {
                return From.ToString("MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture)
                     + " to " +
                       To.ToString("MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        public QCUCRAMAnalyticsViewModel()
        {
            Preset = "last30";
            MostRoom = QCUCRAM_ReportDefaults.Dash;
            LeastRoom = QCUCRAM_ReportDefaults.Dash;
            PeakSlot = QCUCRAM_ReportDefaults.Dash;

            StatusBreakdown = new QCUCRAMBarTable();
            BookingsPerMonth = new QCUCRAMBarTable();
            BookingsPerWeekday = new QCUCRAMBarTable();
            PeakTimeSlots = new QCUCRAMBarTable();
            RoomUtilization = new QCUCRAMBarTable();
            RoomRows = new List<QCUCRAM_RoomUtilizationRow>();
            BookingsPerProgram = new QCUCRAMBarTable();
            AdminDecisions = new QCUCRAMBarTable();

            NoShowsPerStudent = new List<QCUCRAM_NoShowStudentRow>();
            TopRepeatBookers = new List<QCUCRAM_StudentTotal>();
        }
    }

    /// <summary>One booking on the Daily Schedule sheet.</summary>
    public class QCUCRAMScheduleRow
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string RoomName { get; set; }
        public string StudentNumber { get; set; }
        public string RequesterName { get; set; }
        public string RequesterType { get; set; }
        public string Program { get; set; }
        public int NumberOfMembers { get; set; }
        public string Purpose { get; set; }
        public string Status { get; set; }

        /// <summary>"9:00 AM - 10:00 AM" - built in C# because EF cannot translate FormatSlot.</summary>
        public string TimeText
        {
            get { return QCUCRAM_ReservationInfo.FormatSlot(StartTime, EndTime); }
        }
    }

    /// <summary>
    /// One day's confirmed bookings, in start-time then room order.
    ///
    /// PendingCount is a separate query: the sheet lists confirmed bookings only,
    /// but the assistant still needs to know how many requests are waiting.
    /// </summary>
    public class QCUCRAMDailyScheduleViewModel
    {
        public DateTime Date { get; set; }
        public string DateTitle { get; set; }
        public string ErrorMessage { get; set; }
        public List<QCUCRAMScheduleRow> Rows { get; set; }
        public int PendingCount { get; set; }
        public DateTime PrintedAt { get; set; }
        public string PrintedBy { get; set; }

        public int ConfirmedCount { get { return Rows == null ? 0 : Rows.Count; } }
        public DateTime PreviousDay { get { return Date.AddDays(-1); } }
        public DateTime NextDay { get { return Date.AddDays(1); } }

        public QCUCRAMDailyScheduleViewModel()
        {
            Rows = new List<QCUCRAMScheduleRow>();
            DateTitle = string.Empty;
            PrintedBy = string.Empty;
        }
    }

    /// <summary>One group member on the printable booking sheet.</summary>
    public class QCUCRAMPrintMemberRow
    {
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string MI { get; set; }

        /// <summary>"Dela Cruz, Juan P." with the middle initial only when present.</summary>
        public string FullName
        {
            get
            {
                string name = (LastName ?? string.Empty) + ", " + (FirstName ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(MI)) { name += " " + MI.Trim().TrimEnd('.') + "."; }
                return name.Trim().TrimEnd(',');
            }
        }
    }

    /// <summary>One row of the printable status-history table.</summary>
    public class QCUCRAMPrintHistoryRow
    {
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public DateTime ChangedAt { get; set; }
        public string Notes { get; set; }

        /// <summary>Admin full name, "Requester" for a student action, or "System".</summary>
        public string ChangedByText { get; set; }
    }

    /// <summary>
    /// The printable booking sheet for one reservation.
    ///
    /// Found is false for a missing id AND for a student asking about somebody
    /// else's booking - the same message either way, so an id cannot be probed.
    /// </summary>
    public class QCUCRAMPrintBookingViewModel
    {
        public bool Found { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsAdmin { get; set; }
        public string BackUrl { get; set; }

        public int ReservationId { get; set; }
        public string Status { get; set; }
        public string RequesterName { get; set; }
        public string StudentNumber { get; set; }
        public string RequesterType { get; set; }
        public string Program { get; set; }
        public string Email { get; set; }
        public string RoomName { get; set; }
        public DateTime ReservationDate { get; set; }
        public string TimeText { get; set; }
        public int NumberOfMembers { get; set; }
        public string Purpose { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CheckedInAt { get; set; }

        public List<QCUCRAMPrintMemberRow> Members { get; set; }
        public List<QCUCRAMPrintHistoryRow> History { get; set; }

        public DateTime PrintedAt { get; set; }

        public QCUCRAMPrintBookingViewModel()
        {
            Members = new List<QCUCRAMPrintMemberRow>();
            History = new List<QCUCRAMPrintHistoryRow>();
            ErrorMessage = string.Empty;
            BackUrl = "/admin/records";
            TimeText = string.Empty;
        }
    }

    /// <summary>
    /// The earliest and latest reservation dates in the database, for ?range=all.
    /// HasValue is false on an empty Reservations table.
    /// </summary>
    public class QCUCRAMDateBounds
    {
        public DateTime? Min { get; set; }
        public DateTime? Max { get; set; }

        public bool HasValue { get { return Min.HasValue && Max.HasValue; } }

        public QCUCRAMDateBounds() { }

        public QCUCRAMDateBounds(DateTime? min, DateTime? max)
        {
            Min = min;
            Max = max;
        }
    }
}
