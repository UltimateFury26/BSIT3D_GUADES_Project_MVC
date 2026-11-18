using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Services
{
    /// <summary>
    /// Phase 09 - the Analytics screen's data and layout maths.
    ///
    /// Port of Guades_QCUCram_Analytics.aspx.cs. The Web Forms page ran thirteen
    /// separate SqlCommands and then, for each of seven GridViews, built a
    /// Dictionary&lt;string, BarInfo&gt; and wrote the bar width into the RowDataBound
    /// handler. The queries already live in QCUCRAM_ReportRepository (Phase 03),
    /// so this class delegates every read to it and keeps only the presentation
    /// maths: the summary cards, the "most/least booked room" pick and the
    /// percentage that becomes each bar's width.
    ///
    /// It is an instance class with constructor injection, the same shape as
    /// QCUCRAMAdminService and QCUCRAMStudentService, because this project has no
    /// EF DbContext factory to lean on - QCUCRAM_Db is the unit of work.
    /// </summary>
    public class QCUCRAMAnalyticsService
    {
        private readonly QCUCRAM_ReportRepository _reports;
        private readonly QCUCRAM_Db _db;

        public QCUCRAMAnalyticsService()
            : this(new QCUCRAM_ReportRepository(), new QCUCRAM_Db()) { }

        public QCUCRAMAnalyticsService(QCUCRAM_ReportRepository reports, QCUCRAM_Db db)
        {
            if (reports == null) throw new ArgumentNullException("reports");
            if (db == null) throw new ArgumentNullException("db");

            _reports = reports;
            _db = db;
        }

        // =================================================================
        //  The thirteen reads (thin wrappers, so the controller has one
        //  dependency and the guide's method list still exists by name)
        // =================================================================

        public int TotalBookings(DateTime from, DateTime to) { return _reports.TotalBookings(from, to); }
        public int CurrentlyRestricted() { return _reports.CurrentlyRestricted(); }
        public decimal? AverageGroupSize(DateTime from, DateTime to) { return _reports.AverageGroupSize(from, to); }
        public List<QCUCRAM_StatusTotal> StatusBreakdown(DateTime from, DateTime to) { return _reports.StatusBreakdown(from, to); }
        public List<QCUCRAM_MonthTotal> BookingsPerMonth(DateTime from, DateTime to) { return _reports.BookingsPerMonth(from, to); }
        public List<QCUCRAM_DayTotal> BookingsPerWeekday(DateTime from, DateTime to) { return _reports.BookingsPerWeekday(from, to); }
        public List<QCUCRAM_RoomUtilizationRow> RoomUtilization(DateTime from, DateTime to) { return _reports.RoomUtilization(from, to); }
        public List<QCUCRAM_SlotTotal> PeakTimeSlots(DateTime from, DateTime to) { return _reports.PeakTimeSlots(from, to); }
        public QCUCRAM_NoShowSummary NoShowRate(DateTime from, DateTime to) { return _reports.NoShowRate(from, to); }
        public List<QCUCRAM_NoShowStudentRow> NoShowsPerStudent(DateTime from, DateTime to) { return _reports.NoShowsPerStudent(from, to); }
        public List<QCUCRAM_ProgramTotal> BookingsPerProgram(DateTime from, DateTime to) { return _reports.BookingsPerProgram(from, to); }
        public List<QCUCRAM_DecisionTotal> AdminDecisions(DateTime from, DateTime to) { return _reports.AdminDecisions(from, to); }
        public List<QCUCRAM_StudentTotal> TopRepeatBookers(DateTime from, DateTime to) { return _reports.TopRepeatBookers(from, to); }

        /// <summary>
        /// The earliest and latest reservation dates in the whole table, for ?range=all.
        ///
        /// Done with EF rather than SQL so it reads the model, and wrapped so an
        /// empty table returns (null, null) instead of throwing on Min() over nothing.
        /// </summary>
        public QCUCRAMDateBounds DateBounds()
        {
            // Two cheap indexed lookups rather than one GROUP BY: EF6 cannot be
            // relied on to translate GroupBy(constant), and Min() over an empty
            // table throws, whereas FirstOrDefault() simply returns null.
            DateTime? min = _db.Context.Reservations
                .OrderBy(r => r.ReservationDate)
                .Select(r => (DateTime?)r.ReservationDate)
                .FirstOrDefault();

            DateTime? max = _db.Context.Reservations
                .OrderByDescending(r => r.ReservationDate)
                .Select(r => (DateTime?)r.ReservationDate)
                .FirstOrDefault();

            return new QCUCRAMDateBounds(min, max);
        }

        // =================================================================
        //  Build - one call that fills the whole screen
        // =================================================================

        /// <summary>
        /// Runs every query once and assembles the view model.
        ///
        /// Called inside a try/catch by the controller: a failure here becomes the
        /// "could not be loaded" alert rather than a yellow screen of death.
        /// </summary>
        public QCUCRAMAnalyticsViewModel Build(DateTime from, DateTime to, string preset)
        {
            QCUCRAMAnalyticsViewModel vm = new QCUCRAMAnalyticsViewModel();
            vm.From = from;
            vm.To = to;
            vm.Preset = string.IsNullOrEmpty(preset) ? "custom" : preset;
            vm.HasResults = true;

            // ---- summary cards ----
            vm.TotalBookings = _reports.TotalBookings(from, to);

            QCUCRAM_NoShowSummary noShow = _reports.NoShowRate(from, to);
            if (noShow != null)
            {
                vm.Forfeited = noShow.Forfeited;
                vm.ApprovedOrLater = noShow.ApprovedOrLater;
                vm.NoShowPct = noShow.NoShowPct;
            }

            vm.CurrentlyRestricted = _reports.CurrentlyRestricted();
            vm.AverageGroupSize = _reports.AverageGroupSize(from, to);

            // ---- room utilisation: first row is most booked, last is least ----
            List<QCUCRAM_RoomUtilizationRow> rooms = _reports.RoomUtilization(from, to);
            List<QCUCRAM_RoomUtilizationRow> bookedRooms = rooms.Where(r => r.Booked > 0).ToList();

            if (bookedRooms.Count > 0)
            {
                // The Web Forms page printed "Room (N)" - name plus the booking
                // count - and only when the room had at least one booking.
                vm.MostRoom = bookedRooms[0].RoomName
                            + " (" + bookedRooms[0].Booked.ToString("N0", CultureInfo.InvariantCulture) + ")";
                vm.LeastRoom = bookedRooms[bookedRooms.Count - 1].RoomName
                             + " (" + bookedRooms[bookedRooms.Count - 1].Booked.ToString("N0", CultureInfo.InvariantCulture) + ")";
            }

            vm.RoomUtilization = new QCUCRAMBarTable();
            vm.RoomUtilization.Title = "Room utilization (confirmed bookings vs. available slots)";
            vm.RoomUtilization.EmptyText = "No rooms found.";
            vm.RoomUtilization.LabelHeader = "Room";
            vm.RoomUtilization.ValueHeader = "Booked";
            vm.RoomUtilization.BarHeader = "Utilization";
            vm.RoomUtilization.ShowNote = true;
            foreach (QCUCRAM_RoomUtilizationRow row in rooms)
            {
                double pct = row.UtilizationPct.HasValue ? (double)row.UtilizationPct.Value : 0.0;
                if (pct < 0.0) { pct = 0.0; }
                if (pct > 100.0) { pct = 100.0; }

                vm.RoomUtilization.Rows.Add(new QCUCRAMBarRow
                {
                    Label = row.RoomName,
                    Total = row.Booked,
                    Pct = pct,
                    Note = row.Booked.ToString("N0", CultureInfo.InvariantCulture)
                         + " / " + row.Available.ToString("N0", CultureInfo.InvariantCulture)
                         + " slots (" + pct.ToString("0.0", CultureInfo.InvariantCulture) + "%)"
                });
            }

            // ---- peak slot card: the first row that actually has bookings ----
            List<QCUCRAM_SlotTotal> slots = _reports.PeakTimeSlots(from, to);
            QCUCRAM_SlotTotal peak = slots.Where(s => s.Total > 0).FirstOrDefault();
            if (peak != null)
            {
                vm.PeakSlot = QCUCRAM_ReservationInfo.FormatSlot(peak.StartTime, peak.EndTime)
                            + " (" + peak.Total.ToString("N0", CultureInfo.InvariantCulture) + ")";
            }

            // ---- the seven bar charts ----
            vm.StatusBreakdown = Bars("Status breakdown", "Status",
                _reports.StatusBreakdown(from, to).Select(r => Row(r.Status, r.Total)));

            vm.BookingsPerMonth = Bars("Bookings per month", "Month",
                _reports.BookingsPerMonth(from, to).Select(r => Row(r.Month, r.Total)));

            vm.BookingsPerWeekday = Bars("Bookings per weekday", "Day",
                _reports.BookingsPerWeekday(from, to).Select(r => Row(r.Day, r.Total)));

            vm.PeakTimeSlots = Bars("Peak time slots (booked or forfeited)", "Time slot",
                slots.Select(r => Row(r.Label, r.Total)), "No time slots found.");

            vm.BookingsPerProgram = Bars("Bookings per program", "Program",
                _reports.BookingsPerProgram(from, to).Select(r => Row(r.Program, r.Total)));

            vm.AdminDecisions = Bars("Library assistant decisions", "Decision",
                _reports.AdminDecisions(from, to).Select(r => Row(r.NewStatus, r.Total)),
                "No assistant decisions in this range.");

            // ---- the two plain tables ----
            vm.NoShowsPerStudent = _reports.NoShowsPerStudent(from, to);
            vm.TopRepeatBookers = _reports.TopRepeatBookers(from, to);

            return vm;
        }

        // =================================================================
        //  Helpers
        // =================================================================

        /// <summary>A label + a count, before the width is known.</summary>
        private static QCUCRAMBarRow Row(string label, int total)
        {
            return new QCUCRAMBarRow
            {
                Label = string.IsNullOrEmpty(label) ? QCUCRAM_ReportDefaults.Dash : label,
                Total = total,
                Pct = 0.0
            };
        }

        /// <summary>
        /// Turns a list of rows into a bar table, scaling every bar against the
        /// largest value so the biggest row is always full width.
        ///
        /// The Web Forms page did this in RowDataBound with a Dictionary lookup;
        /// doing it once here means the view has no arithmetic in it at all.
        /// </summary>
        private static QCUCRAMBarTable Bars(string title, string labelHeader, IEnumerable<QCUCRAMBarRow> rows,
                                            string emptyText = "No reservations in this range.")
        {
            QCUCRAMBarTable table = new QCUCRAMBarTable();
            table.Title = title;
            table.EmptyText = emptyText;
            table.LabelHeader = labelHeader;
            table.ValueHeader = "Total";
            table.BarHeader = "Share";

            List<QCUCRAMBarRow> list = rows.ToList();
            int max = 0;
            foreach (QCUCRAMBarRow row in list)
            {
                if (row.Total > max) { max = row.Total; }
            }

            foreach (QCUCRAMBarRow row in list)
            {
                row.Pct = max > 0 ? Math.Min(100.0, row.Total * 100.0 / max) : 0.0;
                table.Rows.Add(row);
            }

            return table;
        }
    }
}
