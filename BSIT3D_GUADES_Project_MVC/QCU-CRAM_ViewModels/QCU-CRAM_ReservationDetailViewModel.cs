using System;
using System.Collections.Generic;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>One member of the booking group, as shown in the detail panel.</summary>
    public class QCUCRAMMemberRow
    {
        public string FullName { get; set; }
        public string StudentNumber { get; set; }
        public string Program { get; set; }
        public bool IsRequester { get; set; }
    }

    /// <summary>One line of the status history timeline.</summary>
    public class QCUCRAMHistoryRow
    {
        public DateTime ChangedAt { get; set; }
        public string FromStatus { get; set; }
        public string ToStatus { get; set; }
        public string ActorRole { get; set; }
        public string ActorName { get; set; }
        public string Note { get; set; }
    }

    /// <summary>
    /// The shared "who booked what" panel. Used by the queue modal and by the
    /// records detail pop-up, so the two screens can never drift apart.
    ///
    /// NOTE: the Students table has no YearLevel or ContactNumber column, so this
    /// model deliberately does not pretend to have them. Program and Email are the
    /// only profile fields the schema actually stores.
    /// </summary>
    public class QCUCRAMReservationDetailViewModel
    {
        public int ReservationID { get; set; }
        public string Status { get; set; }
        public string StatusCss { get; set; }

        public string RequesterName { get; set; }
        public string StudentNumber { get; set; }
        public string Program { get; set; }
        public string RequesterType { get; set; }
        public string Email { get; set; }

        public string RoomName { get; set; }
        public DateTime ReservationDate { get; set; }
        public string SlotLabel { get; set; }
        public DateTime SlotStart { get; set; }
        public DateTime SlotEnd { get; set; }

        public int NumberOfMembers { get; set; }
        public string Purpose { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Filled from the newest admin entry in the status history, if there is one.</summary>
        public string DecidedByName { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string DecisionNote { get; set; }

        public IList<QCUCRAMMemberRow> Members { get; set; }
        public IList<QCUCRAMHistoryRow> History { get; set; }

        public QCUCRAMReservationDetailViewModel()
        {
            Members = new List<QCUCRAMMemberRow>();
            History = new List<QCUCRAMHistoryRow>();
        }
    }
}
