using System;
using System.Collections.Generic;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>One row of the approval queue.</summary>
    public class QCUCRAMQueueRow
    {
        public int ReservationID { get; set; }
        public string Requester { get; set; }
        public string StudentNumber { get; set; }
        public string Program { get; set; }
        public string RoomName { get; set; }
        public DateTime ReservationDate { get; set; }
        public string SlotLabel { get; set; }
        public int NumberOfMembers { get; set; }
        public string Status { get; set; }
        public string StatusCss { get; set; }

        /// <summary>Bootstrap badge class - Queue.aspx.cs StatusBadge used text-bg-*.</summary>
        public string StatusBadge { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsToday { get; set; }

        public bool CanApprove { get; set; }
        public bool CanReassign { get; set; }
        public bool CanDecline { get; set; }
    }

    /// <summary>
    /// One option in the "move to another room and time" select.
    /// Value is "roomId|slotId" so the post-back needs no extra hidden fields.
    /// </summary>
    public class QCUCRAMReassignTarget
    {
        public string Value { get; set; }
        public string Label { get; set; }
    }

    public class QCUCRAMReassignViewModel
    {
        public int ReservationID { get; set; }
        public string CurrentRoomName { get; set; }
        public string CurrentSlotLabel { get; set; }
        public DateTime ReservationDate { get; set; }
        public string Status { get; set; }
        public IList<QCUCRAMReassignTarget> Targets { get; set; }

        /// <summary>Set when the request can no longer be reassigned; the controller flashes it.</summary>
        public string Error { get; set; }

        public QCUCRAMReassignViewModel()
        {
            Targets = new List<QCUCRAMReassignTarget>();
        }
    }

    public class QCUCRAMQueueViewModel
    {
        /// <summary>The clock the rows were judged against (Dev Tools aware).</summary>
        public DateTime Now { get; set; }

        public IList<QCUCRAMQueueRow> Pending { get; set; }
        public IList<QCUCRAMQueueRow> Upcoming { get; set; }

        /// <summary>Set only when ?review=N was supplied and the reservation exists.</summary>
        public QCUCRAMReservationDetailViewModel Detail { get; set; }

        /// <summary>Set only when ?reassign=N was supplied and the reservation exists.</summary>
        public QCUCRAMReassignViewModel Reassign { get; set; }

        public int ReviewID { get; set; }
        public int ReassignID { get; set; }

        public QCUCRAMQueueViewModel()
        {
            Pending = new List<QCUCRAMQueueRow>();
            Upcoming = new List<QCUCRAMQueueRow>();
        }
    }
}
