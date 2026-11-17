using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data.QCUCRAM_Configurations;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data
{
    public class QCUCRAMContext : DbContext
    {
        /// <summary>
        /// The database is created and owned by Guades_QCUCram_Schema.sql.
        /// EF must never create, drop, or auto-migrate it.
        /// </summary>
        static QCUCRAMContext()
        {
            Database.SetInitializer<QCUCRAMContext>(null);
        }

        public QCUCRAMContext()
            : base("name=QCU-CRAM_Connection")
        {
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
        }

        public DbSet<QCUCRAM_Room> Rooms { get; set; }
        public DbSet<QCUCRAM_TimeSlot> TimeSlots { get; set; }
        public DbSet<QCUCRAM_Student> Students { get; set; }
        public DbSet<QCUCRAM_AdminUser> AdminUsers { get; set; }
        public DbSet<QCUCRAM_Reservation> Reservations { get; set; }
        public DbSet<QCUCRAM_ReservationMember> ReservationMembers { get; set; }
        public DbSet<QCUCRAM_ReservationStatusHistory> ReservationStatusHistories { get; set; }
        public DbSet<QCUCRAM_Notification> Notifications { get; set; }
        public DbSet<QCUCRAM_Setting> Settings { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

            modelBuilder.Configurations.Add(new QCUCRAM_RoomConfiguration());
            modelBuilder.Configurations.Add(new QCUCRAM_TimeSlotConfiguration());
            modelBuilder.Configurations.Add(new QCUCRAM_StudentConfiguration());
            modelBuilder.Configurations.Add(new QCUCRAM_AdminUserConfiguration());
            modelBuilder.Configurations.Add(new QCUCRAM_ReservationConfiguration());
            modelBuilder.Configurations.Add(new QCUCRAM_ReservationMemberConfiguration());
            modelBuilder.Configurations.Add(new QCUCRAM_ReservationStatusHistoryConfiguration());
            modelBuilder.Configurations.Add(new QCUCRAM_NotificationConfiguration());
            modelBuilder.Configurations.Add(new QCUCRAM_SettingConfiguration());

            base.OnModelCreating(modelBuilder);
        }
    }
}
