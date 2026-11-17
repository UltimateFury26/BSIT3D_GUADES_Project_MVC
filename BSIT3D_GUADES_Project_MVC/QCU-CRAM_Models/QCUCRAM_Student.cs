using System;
using System.Collections.Generic;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Models
{
    public class QCUCRAM_Student
    {
        public int StudentID { get; set; }
        public string StudentNumber { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string MI { get; set; }
        public string Email { get; set; }
        public string Program { get; set; }
        public string RequesterType { get; set; }
        public string PasswordHash { get; set; }
        public int NoShowCount { get; set; }
        public bool IsRestricted { get; set; }
        public DateTime? RestrictionLiftDate { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual ICollection<QCUCRAM_Reservation> Reservations { get; set; }
        public virtual ICollection<QCUCRAM_Notification> Notifications { get; set; }

        public QCUCRAM_Student()
        {
            Reservations = new HashSet<QCUCRAM_Reservation>();
            Notifications = new HashSet<QCUCRAM_Notification>();
        }
    }
}
