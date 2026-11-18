using System;
using System.Collections.Generic;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>
    /// One card in the strip at the top of the admin dashboard.
    ///
    /// Dashboard.aspx.cs built these as raw HTML in a StringBuilder. Here the
    /// label, the number, the caption, the border colour and the destination are
    /// decided once in the service, so the Razor file only loops.
    /// </summary>
    public class QCUCRAMStatCard
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public string Hint { get; set; }

        /// <summary>Bootstrap border accent: warning | primary | success | danger | secondary.</summary>
        public string Accent { get; set; }

        /// <summary>Where the card links to, so a number is always one click from its list.</summary>
        public string Url { get; set; }
    }

    /// <summary>One row of the "Next 5 upcoming bookings" table.</summary>
    public class QCUCRAMUpcomingRow
    {
        public int ReservationID { get; set; }
        public DateTime ReservationDate { get; set; }

        /// <summary>"Today" / "Tomorrow" / null - the badge beside the date.</summary>
        public string DateBadge { get; set; }

        /// <summary>Bootstrap badge class for DateBadge.</summary>
        public string DateBadgeCss { get; set; }

        public string Requester { get; set; }
        public string StudentNumber { get; set; }
        public string RoomName { get; set; }
        public string SlotLabel { get; set; }
        public string Status { get; set; }

        /// <summary>Bootstrap badge class, e.g. "bg-warning text-dark".</summary>
        public string StatusCss { get; set; }

        public int NumberOfMembers { get; set; }
        public TimeSpan StartTime { get; set; }
    }

    public class QCUCRAMAdminDashboardViewModel
    {
        public DateTime Today { get; set; }
        public DateTime Now { get; set; }

        /// <summary>"Welcome, {user}." - set by the controller from the session.</summary>
        public string WelcomeName { get; set; }

        /// <summary>"System time: ..." line, including the Dev Tools note when shifted.</summary>
        public string ClockText { get; set; }

        /// <summary>Dev Tools note, or null when the clock is not shifted.</summary>
        public string ClockNote { get; set; }

        /// <summary>True when the page could not be fully loaded; shows the alert.</summary>
        public bool HasError { get; set; }

        public string Message { get; set; }

        public IList<QCUCRAMStatCard> Cards { get; set; }
        public IList<QCUCRAMUpcomingRow> Upcoming { get; set; }

        public int PendingCount { get; set; }
        public int RestrictedCount { get; set; }

        public QCUCRAMAdminDashboardViewModel()
        {
            Cards = new List<QCUCRAMStatCard>();
            Upcoming = new List<QCUCRAMUpcomingRow>();
        }
    }
}
