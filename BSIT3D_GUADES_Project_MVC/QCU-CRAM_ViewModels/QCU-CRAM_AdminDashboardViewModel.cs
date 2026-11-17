using System;
using System.Collections.Generic;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>
    /// One card in the strip at the top of the admin dashboard.
    ///
    /// The Web Forms page built these as raw HTML in a StringBuilder inside
    /// Dashboard.aspx.cs. Here the label, the number, the caption and the colour
    /// are decided once in the service, so the Razor file only loops.
    /// </summary>
    public class QCUCRAMStatCard
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public string Hint { get; set; }

        /// <summary>"" | "is-warn" | "is-bad" - maps to a CSS class on .qcu-stat.</summary>
        public string Tone { get; set; }

        /// <summary>Where the card links to, so a number is always one click from its list.</summary>
        public string Url { get; set; }
    }

    /// <summary>One row of the "today" strip on the dashboard.</summary>
    public class QCUCRAMUpcomingRow
    {
        public int ReservationID { get; set; }
        public string Requester { get; set; }
        public string RoomName { get; set; }
        public string SlotLabel { get; set; }
        public string Status { get; set; }
        public string StatusCss { get; set; }
        public int NumberOfMembers { get; set; }
        public TimeSpan StartTime { get; set; }

        /// <summary>True while the clock is inside this booking's slot.</summary>
        public bool IsNow { get; set; }
    }

    public class QCUCRAMAdminDashboardViewModel
    {
        public DateTime Today { get; set; }
        public DateTime Now { get; set; }

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
