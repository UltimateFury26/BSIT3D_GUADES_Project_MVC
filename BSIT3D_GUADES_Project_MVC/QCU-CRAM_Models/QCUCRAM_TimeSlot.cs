using System;
using System.Collections.Generic;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Models
{
    public class QCUCRAM_TimeSlot
    {
        public int TimeSlotID { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsActive { get; set; }

        public virtual ICollection<QCUCRAM_Reservation> Reservations { get; set; }

        public QCUCRAM_TimeSlot()
        {
            Reservations = new HashSet<QCUCRAM_Reservation>();
        }
    }
}
