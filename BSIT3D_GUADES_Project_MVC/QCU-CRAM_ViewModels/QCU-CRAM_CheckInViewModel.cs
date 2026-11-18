using System;
using System.Collections.Generic;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>
    /// One row of the check-in desk. Mirrors the DataTable columns
    /// CheckIn.aspx.cs built: WhenHtml, RequesterHtml, MembersHtml, StateHtml,
    /// CanCheckIn, plus CheckedText on the "checked in today" grid.
    /// </summary>
    public class QCUCRAMCheckInRow
    {
        public int ReservationID { get; set; }
        public string Requester { get; set; }
        public string StudentNumber { get; set; }
        public string RequesterType { get; set; }
        public string Program { get; set; }
        public string RoomName { get; set; }
        public string SlotLabel { get; set; }
        public DateTime ReservationDate { get; set; }
        public int NumberOfMembers { get; set; }
        public string Status { get; set; }
        public string StatusCss { get; set; }

        /// <summary>Group member names, in MemberID order (the expandable list).</summary>
        public IList<string> Members { get; set; }

        public DateTime SlotStart { get; set; }
        public DateTime GraceEndsAt { get; set; }
        public int MinutesLeft { get; set; }

        public bool CanCheckIn { get; set; }

        /// <summary>"Opens at 9:00 AM" / "Closed at 9:15 AM - will be forfeited".</summary>
        public string BlockedReason { get; set; }

        /// <summary>Bootstrap badge class: text-bg-secondary / success / warning / danger.</summary>
        public string StateBadgeCss { get; set; }

        /// <summary>"Not yet open" / "Open for check-in" / "Grace expiring" / "Past grace".</summary>
        public string StateText { get; set; }

        /// <summary>"Opens at 9:00 AM" / "Closes in 12 min (9:15 AM)" / "Closed at 9:15 AM - will be forfeited".</summary>
        public string StateDetail { get; set; }

        public DateTime? CheckedInAt { get; set; }

        /// <summary>"9:04 AM" - the "Checked in at" column.</summary>
        public string CheckedText { get; set; }

        public QCUCRAMCheckInRow()
        {
            Members = new List<string>();
        }
    }

    public class QCUCRAMCheckInViewModel
    {
        public DateTime Now { get; set; }
        public int GraceMinutes { get; set; }
        public string Search { get; set; }

        /// <summary>"System time: ... - Grace period: N minutes" line under the notice.</summary>
        public string NowText { get; set; }

        public IList<QCUCRAMCheckInRow> Waiting { get; set; }
        public IList<QCUCRAMCheckInRow> CheckedIn { get; set; }

        public QCUCRAMCheckInViewModel()
        {
            Waiting = new List<QCUCRAMCheckInRow>();
            CheckedIn = new List<QCUCRAMCheckInRow>();
        }
    }
}
