namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Models
{
    public class QCUCRAM_ReservationMember
    {
        public int MemberID { get; set; }
        public int ReservationID { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string MI { get; set; }

        public virtual QCUCRAM_Reservation Reservation { get; set; }
    }
}
