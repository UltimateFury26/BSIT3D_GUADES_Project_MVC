using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data.QCUCRAM_Configurations
{
    public class QCUCRAM_NotificationConfiguration : EntityTypeConfiguration<QCUCRAM_Notification>
    {
        public QCUCRAM_NotificationConfiguration()
        {
            ToTable("Notifications", "dbo");
            HasKey(n => n.NotificationID);
            Property(n => n.NotificationID).HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity);

            Property(n => n.StudentID).IsRequired();
            Property(n => n.ReservationID).IsOptional();
            Property(n => n.Message).IsRequired().HasMaxLength(400).IsUnicode(true);
            Property(n => n.IsRead).IsRequired();
            Property(n => n.CreatedAt).IsRequired().HasColumnType("datetime");

            HasRequired(n => n.Student)
                .WithMany(s => s.Notifications)
                .HasForeignKey(n => n.StudentID)
                .WillCascadeOnDelete(false);

            // FK_Notifications_Reservation is ON DELETE SET NULL in SQL Server.
            // EF 6 cannot emit SET NULL, so the FK is mapped without cascade here
            // and the real rule stays in QCU-CRAM_Schema.sql.
            HasOptional(n => n.Reservation)
                .WithMany(r => r.Notifications)
                .HasForeignKey(n => n.ReservationID)
                .WillCascadeOnDelete(false);

        }
    }
}
