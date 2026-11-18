using System;
using System.Collections.Generic;
using System.Globalization;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>
    /// Phase 06 - the model behind Views/Student/QCU-CRAM_Dashboard.cshtml.
    ///
    /// Port of Guades_QCUCram_Student/Guades_QCUCram_Dashboard.aspx: the restriction
    /// banner, the welcome strip with Quick Reserve, the upcoming and recent booking
    /// tables, the per-room availability glance and the month calendar.
    /// </summary>
    public class QCUCRAMStudentDashboardViewModel
    {
        public QCUCRAMStudentDashboardViewModel()
        {
            Upcoming = new List<QCUCRAM_ReservationListItem>();
            Recent = new List<QCUCRAM_ReservationListItem>();
            Glance = new List<QCUCRAMAvailabilityGlanceRow>();
        }

        public QCUCRAM_Student Student { get; set; }

        /// <summary>True when the account is barred from booking (pnlRestricted in the original).</summary>
        public bool IsRestricted { get; set; }

        public int NoShowCount { get; set; }
        public int NoShowThreshold { get; set; }
        public DateTime? RestrictionLiftDate { get; set; }

        /// <summary>Active bookings whose slot has not ended yet, soonest first.</summary>
        public List<QCUCRAM_ReservationListItem> Upcoming { get; set; }

        /// <summary>The five most recently created bookings, newest first.</summary>
        public List<QCUCRAM_ReservationListItem> Recent { get; set; }

        /// <summary>One row per active room: how many of its slots are held on the booking day.</summary>
        public List<QCUCRAMAvailabilityGlanceRow> Glance { get; set; }

        public QCUCRAMCalendarViewModel Calendar { get; set; }

        public int UnreadCount { get; set; }
        public int TotalReservations { get; set; }

        /// <summary>Today + AdvanceBookingDays - what Quick Reserve points at.</summary>
        public DateTime BookingDay { get; set; }

        /// <summary>
        /// The AdvanceBookingDays setting the booking day was derived from (1 = tomorrow).
        /// Kept so the glance header can say "tomorrow" / "today" / "in 3 days" the way
        /// the original ShowGlance(bookingDay, ahead) did.
        /// </summary>
        public int AdvanceBookingDays { get; set; }

        /// <summary>
        /// lblGlanceTitle in the original: "Availability tomorrow (Sat, Mar 14)".
        /// </summary>
        public string GlanceTitle
        {
            get
            {
                string when = AdvanceBookingDays == 1 ? "tomorrow"
                            : AdvanceBookingDays == 0 ? "today"
                            : "in " + AdvanceBookingDays + " days";

                return "Availability " + when + " (" +
                       BookingDay.ToString("ddd, MMM d", CultureInfo.InvariantCulture) + ")";
            }
        }

        /// <summary>
        /// lblWelcome in the original: "Welcome, " + FirstName + " " + LastName + "."
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (Student == null) { return string.Empty; }
                return (Student.FirstName + " " + Student.LastName).Trim();
            }
        }

        public string BookingDayLabel
        {
            get { return BookingDay.ToString("dddd, MMMM d, yyyy"); }
        }

        public string FirstName
        {
            get { return Student == null ? string.Empty : Student.FirstName; }
        }

        public string FullName
        {
            get
            {
                if (Student == null) { return string.Empty; }
                string mi = string.IsNullOrWhiteSpace(Student.MI) ? string.Empty : " " + Student.MI + ".";
                return Student.FirstName + mi + " " + Student.LastName;
            }
        }

        /// <summary>Strikes left before the account is restricted.</summary>
        public int StrikesLeft
        {
            get
            {
                int left = NoShowThreshold - NoShowCount;
                return left < 0 ? 0 : left;
            }
        }
    }

    /// <summary>One room's occupancy on the booking day ("2 of 8 slots held").</summary>
    public class QCUCRAMAvailabilityGlanceRow
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; }
        public int TotalSlots { get; set; }
        public int Held { get; set; }

        public int Open
        {
            get
            {
                int open = TotalSlots - Held;
                return open < 0 ? 0 : open;
            }
        }

        /// <summary>
        /// The original ShowGlance badge: open == 0 is "Full" (red), open &lt;= 2 is
        /// amber, otherwise green. The text is "3/8 open", not "3 of 8 open".
        /// </summary>
        public bool IsFull { get { return Open == 0; } }

        public string BadgeText
        {
            get { return IsFull ? "Full" : Open + "/" + TotalSlots + " open"; }
        }

        public string BadgeClass
        {
            get { return IsFull ? "bg-danger" : (Open <= 2 ? "bg-warning text-dark" : "bg-success"); }
        }
    }
}
