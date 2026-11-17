using System.Collections.Generic;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Models
{
    public class QCUCRAM_Room
    {
        public int RoomID { get; set; }
        public string RoomName { get; set; }
        public int MinCapacity { get; set; }
        public int MaxCapacity { get; set; }
        public bool IsActive { get; set; }

        public virtual ICollection<QCUCRAM_Reservation> Reservations { get; set; }

        public QCUCRAM_Room()
        {
            Reservations = new HashSet<QCUCRAM_Reservation>();
        }
    }
}
