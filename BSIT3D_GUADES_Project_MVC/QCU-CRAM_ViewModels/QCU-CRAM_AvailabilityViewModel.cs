using System;
using System.Collections.Generic;
using System.Globalization;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>
    /// Phase 06 - the model behind Views/Availability/QCU-CRAM_Index.cshtml.
    ///
    /// Port of Guades_QCUCram_Availability.aspx: one row per time slot, one column per
    /// room, and one cell per room + slot pair. The Web Forms page built the same shape
    /// with three SqlDataSource queries and a byCell dictionary; here the service fills
    /// the dictionary and the view only reads it.
    ///
    /// Cell states, in the order the original tested them:
    ///   off      - the room or the slot is disabled and nothing is booked in it
    ///   pending  - an active request is waiting for a decision
    ///   taken    - a confirmed booking (Approved / Reassigned / CheckedIn / Completed)
    ///   mine     - the signed-in student's own request or booking
    ///   closed   - an empty slot on a date before the booking day
    ///   open     - empty and bookable
    /// </summary>
    public class QCUCRAMAvailabilityViewModel
    {
        public QCUCRAMAvailabilityViewModel()
        {
            Rooms = new List<QCUCRAMRoomColumn>();
            Slots = new List<QCUCRAMSlotRow>();
            Cells = new Dictionary<string, QCUCRAMCell>(StringComparer.Ordinal);
        }

        /// <summary>The date being shown (already normalised to midnight).</summary>
        public DateTime Date { get; set; }

        /// <summary>Today + AdvanceBookingDays - the only date a request may be filed for.</summary>
        public DateTime BookingDay { get; set; }

        public bool IsAdmin { get; set; }
        public bool IsStudent { get; set; }

        /// <summary>True only when Date equals BookingDay, i.e. an Open cell can be clicked.</summary>
        public bool CanBook { get; set; }

        /// <summary>True when the date is before the booking day (every empty cell is Closed).</summary>
        public bool IsClosed { get; set; }

        /// <summary>True when the date is after the booking day (read-only preview).</summary>
        public bool IsFuture { get; set; }

        /// <summary>The booking rules as an &lt;ol&gt;, straight from Settings (BuildRulesHtml).</summary>
        public string RulesHtml { get; set; }

        public List<QCUCRAMRoomColumn> Rooms { get; set; }
        public List<QCUCRAMSlotRow> Slots { get; set; }

        /// <summary>Keyed by <see cref="Key"/> so the view can look a cell up in O(1).</summary>
        public Dictionary<string, QCUCRAMCell> Cells { get; set; }

        public int OpenCount { get; set; }
        public int PendingCount { get; set; }
        public int TakenCount { get; set; }
        public int MineCount { get; set; }
        public int ClosedCount { get; set; }

        /// <summary>Total bookable cells (rooms x slots) - the denominator of the counters.</summary>
        public int TotalCells { get; set; }

        public static string Key(int roomId, int timeSlotId)
        {
            return roomId.ToString(CultureInfo.InvariantCulture) + "|" +
                   timeSlotId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The cell for one room + slot; never null, so the view can render unconditionally.</summary>
        public QCUCRAMCell Cell(int roomId, int timeSlotId)
        {
            QCUCRAMCell cell;
            if (Cells.TryGetValue(Key(roomId, timeSlotId), out cell)) { return cell; }

            return new QCUCRAMCell
            {
                RoomId = roomId,
                TimeSlotId = timeSlotId,
                State = "off",
                Text = "\u2014",
                CssClass = "table-secondary",
                StateClass = "qcu-cell--unavailable"
            };
        }

        public string DateLabel
        {
            get { return Date.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture); }
        }

        public string ShortDateLabel
        {
            get { return Date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture); }
        }

        public string BookingDayLabel
        {
            get { return BookingDay.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture); }
        }

        public DateTime PreviousDay { get { return Date.AddDays(-1); } }
        public DateTime NextDay { get { return Date.AddDays(1); } }

        /// <summary>"Open 12 of 40 slots" - the one-line summary above the grid.</summary>
        public string SummaryLine
        {
            get
            {
                if (IsClosed) { return "This date is closed. Requests can only be filed for " + BookingDayLabel + "."; }
                if (IsFuture) { return "This is a preview of " + DateLabel + ". Requests can only be filed for " + BookingDayLabel + "."; }
                return OpenCount + " of " + TotalCells + " slot(s) are still open on " + DateLabel + ".";
            }
        }
    }

    /// <summary>One room column of the grid.</summary>
    public class QCUCRAMRoomColumn
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; }
        public int MinCapacity { get; set; }
        public int MaxCapacity { get; set; }
        public bool IsActive { get; set; }

        /// <summary>"4-8 people" - shown under the room name.</summary>
        public string CapacityLabel
        {
            get
            {
                return MinCapacity == MaxCapacity
                    ? MaxCapacity + " people"
                    : MinCapacity + "-" + MaxCapacity + " people";
            }
        }
    }

    /// <summary>One time-slot row of the grid.</summary>
    public class QCUCRAMSlotRow
    {
        public int TimeSlotId { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsActive { get; set; }

        /// <summary>"9:00 AM - 10:00 AM".</summary>
        public string Label
        {
            get
            {
                DateTime d = DateTime.Today;
                return d.Add(StartTime).ToString("h:mm tt", CultureInfo.InvariantCulture) + " - " +
                       d.Add(EndTime).ToString("h:mm tt", CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>One room + slot intersection.</summary>
    public class QCUCRAMCell
    {
        public int RoomId { get; set; }
        public int TimeSlotId { get; set; }
        public int? ReservationId { get; set; }
        public string Status { get; set; }

        /// <summary>open | pending | taken | mine | closed | off</summary>
        public string State { get; set; }

        /// <summary>The caption drawn inside the cell.</summary>
        public string Text { get; set; }

        /// <summary>Secondary line (requester name for an admin, "Your request" for a student).</summary>
        public string Detail { get; set; }

        /// <summary>Bootstrap table class already understood by QCU-CRAM_Site.css.</summary>
        public string CssClass { get; set; }

        /// <summary>Phase 06 cell modifier (.qcu-cell--open etc.).</summary>
        public string StateClass { get; set; }

        /// <summary>True when the cell should render as a link to the Reserve page.</summary>
        public bool IsBookable { get; set; }

        /// <summary>True when the cell belongs to the signed-in student.</summary>
        public bool IsMine { get; set; }
    }
}
