using System;
using System.Collections.Generic;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>One row of the check-in desk.</summary>
    public class QCUCRAMCheckInRow
    {
        public int ReservationID { get; set; }
        public string Requester { get; set; }
        public string StudentNumber { get; set; }
        public string Program { get; set; }
        public string RoomName { get; set; }
        public string SlotLabel { get; set; }
        public DateTime ReservationDate { get; set; }
        public int NumberOfMembers { get; set; }
        public string Status { get; set; }
        public string StatusCss { get; set; }

        public DateTime SlotStart { get; set; }
        public DateTime GraceEndsAt { get; set; }
        public int MinutesLeft { get; set; }

        public bool CanCheckIn { get; set; }

        /// <summary>"Opens at 9:00 AM" / "Closed at 9:15 AM" - shown when CanCheckIn is false.</summary>
        public string BlockedReason { get; set; }

        public DateTime? CheckedInAt { get; set; }
    }

    public class QCUCRAMCheckInViewModel
    {
        public DateTime Now { get; set; }
        public int GraceMinutes { get; set; }
        public string Search { get; set; }

        public IList<QCUCRAMCheckInRow> Waiting { get; set; }
        public IList<QCUCRAMCheckInRow> CheckedIn { get; set; }

        public QCUCRAMCheckInViewModel()
        {
            Waiting = new List<QCUCRAMCheckInRow>();
            CheckedIn = new List<QCUCRAMCheckInRow>();
        }
    }
}
