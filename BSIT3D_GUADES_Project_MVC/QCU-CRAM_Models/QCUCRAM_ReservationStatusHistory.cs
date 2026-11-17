using System;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Models
{
    public class QCUCRAM_ReservationStatusHistory
    {
        public int HistoryID { get; set; }
        public int ReservationID { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public string ChangedByRole { get; set; }
        public int? ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; }
        public string Notes { get; set; }

        public virtual QCUCRAM_Reservation Reservation { get; set; }
    }
}
