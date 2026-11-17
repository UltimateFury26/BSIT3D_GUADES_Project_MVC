using System;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Models
{
    public class QCUCRAM_Notification
    {
        public int NotificationID { get; set; }
        public int StudentID { get; set; }
        public int? ReservationID { get; set; }
        public string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual QCUCRAM_Student Student { get; set; }
        public virtual QCUCRAM_Reservation Reservation { get; set; }
    }
}
