using System;
using System.Collections.Generic;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>
    /// Phase 06 - the model behind Views/Student/QCU-CRAM_Reserve.cshtml.
    ///
    /// Port of Guades_QCUCram_Student/Guades_QCUCram_Reserve.aspx. The same five inputs
    /// are posted - Date, Room, Slot, Purpose, NumberOfMembers plus the member repeater -
    /// and the same server-side rules are re-checked by
    /// QCUCRAM_ReservationRepository.ValidateAndCreate, so a tampered post cannot slip
    /// past the form.
    ///
    /// The member rows bind as Members[0].LastName, Members[1].LastName, ... which is why
    /// this is a List&lt;QCUCRAM_MemberInput&gt; and not a fixed set of properties.
    /// </summary>
    public class QCUCRAMReserveViewModel
    {
        public QCUCRAMReserveViewModel()
        {
            Rooms = new List<QCUCRAM_Room>();
            TimeSlots = new List<QCUCRAM_TimeSlot>();
            Members = new List<QCUCRAM_MemberInput>();
            MinMembers = 5;
            MaxMembers = 10;
            MaxPurposeLength = 300;
        }

        // ---- who is asking (the <dl> at the top of the original page) ----

        public QCUCRAM_Student Requester { get; set; }

        public bool IsRestricted { get; set; }
        public int NoShowCount { get; set; }
        public DateTime? RestrictionLiftDate { get; set; }

        // ---- the form fields ----

        /// <summary>Must equal BookingDay; ValidateAndCreate rejects anything else.</summary>
        public DateTime Date { get; set; }

        public int RoomId { get; set; }
        public int TimeSlotId { get; set; }

        public string Purpose { get; set; }

        public int NumberOfMembers { get; set; }

        public List<QCUCRAM_MemberInput> Members { get; set; }

        // ---- the drop-down sources and the limits ----

        public List<QCUCRAM_Room> Rooms { get; set; }
        public List<QCUCRAM_TimeSlot> TimeSlots { get; set; }

        public DateTime BookingDay { get; set; }
        public string RulesHtml { get; set; }

        public int MinMembers { get; set; }
        public int MaxMembers { get; set; }
        public int MaxPurposeLength { get; set; }

        // ---- display helpers ----

        public string RequesterFullName
        {
            get
            {
                if (Requester == null) { return string.Empty; }
                string mi = string.IsNullOrWhiteSpace(Requester.MI) ? string.Empty : " " + Requester.MI + ".";
                return Requester.FirstName + mi + " " + Requester.LastName;
            }
        }

        public string BookingDayLabel
        {
            get { return BookingDay.ToString("dddd, MMMM d, yyyy"); }
        }

        /// <summary>True when a room and a slot were both supplied by the availability grid.</summary>
        public bool HasPreselection
        {
            get { return RoomId > 0 && TimeSlotId > 0; }
        }

        /// <summary>"9:00 AM - 10:00 AM" for one slot id, or an empty string.</summary>
        public string SlotLabel(int timeSlotId)
        {
            foreach (QCUCRAM_TimeSlot slot in TimeSlots)
            {
                if (slot.TimeSlotID != timeSlotId) { continue; }
                DateTime d = DateTime.Today;
                return d.Add(slot.StartTime).ToString("h:mm tt") + " - " +
                       d.Add(slot.EndTime).ToString("h:mm tt");
            }
            return string.Empty;
        }
    }
}
