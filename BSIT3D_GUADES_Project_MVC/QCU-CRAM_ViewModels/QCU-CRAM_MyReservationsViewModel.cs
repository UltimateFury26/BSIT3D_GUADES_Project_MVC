using System;
using System.Collections.Generic;
using System.Globalization;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>
    /// Phase 06 - the model behind Views/Student/QCU-CRAM_MyReservations.cshtml.
    ///
    /// Port of Guades_QCUCram_Student/Guades_QCUCram_MyReservations.aspx: the status
    /// filter, the "request #N was submitted" confirmation, the ten-row GridView and the
    /// Cancel button that only appears while the booking can still be called off.
    /// </summary>
    public class QCUCRAMMyReservationsViewModel
    {
        public QCUCRAMMyReservationsViewModel()
        {
            Items = new List<QCUCRAMReservationRow>();
        }

        public List<QCUCRAMReservationRow> Items { get; set; }

        /// <summary>"" = All, otherwise one of the eight statuses.</summary>
        public string StatusFilter { get; set; }

        /// <summary>?created=N - the id the Reserve page redirected with.</summary>
        public int? CreatedId { get; set; }

        /// <summary>Rows before paging (used by the summary line).</summary>
        public int TotalItems { get; set; }

        public QCUCRAMPagerViewModel Pager { get; set; }

        /// <summary>All eight statuses, for the filter drop-down.</summary>
        public string[] StatusChoices
        {
            get { return QCUCRAM_Status.All; }
        }

        public bool HasFilter
        {
            get { return !string.IsNullOrWhiteSpace(StatusFilter); }
        }

        public bool IsEmpty
        {
            get { return Items.Count == 0; }
        }
    }

    /// <summary>One row of the My Reservations table.</summary>
    public class QCUCRAMReservationRow
    {
        public QCUCRAM_ReservationListItem Item { get; set; }

        /// <summary>True while the student may still cancel (Pending/Approved/Reassigned and not started).</summary>
        public bool CanCancel { get; set; }

        public int ReservationId { get { return Item == null ? 0 : Item.ReservationId; } }

        public string Status
        {
            get { return Item == null ? string.Empty : Item.Status; }
        }

        /// <summary>
        /// Phase 13: the Web Forms grid printed the raw status text ("CheckedIn"), so the label
        /// is the raw status too.
        /// </summary>
        public string StatusLabel
        {
            get { return Item == null ? string.Empty : Item.Status; }
        }

        /// <summary>
        /// Phase 13: Bootstrap badge class from Guades_QCUCram_MyReservations.aspx.cs StatusBadge().
        /// Cancelled (and anything unknown) falls through to text-bg-secondary.
        /// </summary>
        public string BadgeCss
        {
            get
            {
                switch (Status)
                {
                    case "Pending": return "text-bg-warning";
                    case "Approved": return "text-bg-success";
                    case "Reassigned": return "text-bg-info";
                    case "CheckedIn": return "text-bg-primary";
                    case "Completed": return "text-bg-dark";
                    case "Declined": return "text-bg-danger";
                    case "Forfeited": return "text-bg-danger";
                    default: return "text-bg-secondary";
                }
            }
        }

        /// <summary>Phase 13: "Wed, Oct 7, 2026" - the source format ddd, MMM d, yyyy.</summary>
        public string DateLabel
        {
            get
            {
                return Item == null
                    ? string.Empty
                    : Item.ReservationDate.ToString("ddd, MMM d, yyyy", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Phase 13: the source "Members" column is the bare number (no "pax").</summary>
        public string MembersLabel
        {
            get { return Item == null ? string.Empty : Item.NumberOfMembers.ToString(CultureInfo.InvariantCulture); }
        }

        /// <summary>Exact start moment, used by the "already started" tooltip.</summary>
        public DateTime SlotStart
        {
            get { return Item == null ? DateTime.MinValue : Item.ReservationDate.Date.Add(Item.StartTime); }
        }
    }
}
