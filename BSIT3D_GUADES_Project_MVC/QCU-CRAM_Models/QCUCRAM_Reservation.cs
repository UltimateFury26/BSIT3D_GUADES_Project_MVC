using System;
using System.Collections.Generic;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Models
{
    public class QCUCRAM_Reservation
    {
        public int ReservationID { get; set; }
        public int StudentID { get; set; }
        public int RoomID { get; set; }
        public int TimeSlotID { get; set; }
        public DateTime ReservationDate { get; set; }
        public string Purpose { get; set; }
        public int NumberOfMembers { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CheckedInAt { get; set; }

        public virtual QCUCRAM_Student Student { get; set; }
        public virtual QCUCRAM_Room Room { get; set; }
        public virtual QCUCRAM_TimeSlot TimeSlot { get; set; }
        public virtual ICollection<QCUCRAM_ReservationMember> Members { get; set; }
        public virtual ICollection<QCUCRAM_ReservationStatusHistory> StatusHistory { get; set; }
        public virtual ICollection<QCUCRAM_Notification> Notifications { get; set; }

        public QCUCRAM_Reservation()
        {
            Members = new HashSet<QCUCRAM_ReservationMember>();
            StatusHistory = new HashSet<QCUCRAM_ReservationStatusHistory>();
            Notifications = new HashSet<QCUCRAM_Notification>();
        }
    }
}
