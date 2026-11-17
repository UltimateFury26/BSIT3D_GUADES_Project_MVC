using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data.QCUCRAM_Configurations
{
    public class QCUCRAM_ReservationConfiguration : EntityTypeConfiguration<QCUCRAM_Reservation>
    {
        public QCUCRAM_ReservationConfiguration()
        {
            ToTable("Reservations", "dbo");
            HasKey(r => r.ReservationID);
            Property(r => r.ReservationID).HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity);

            Property(r => r.StudentID).IsRequired();
            Property(r => r.RoomID).IsRequired();
            Property(r => r.TimeSlotID).IsRequired();
            Property(r => r.ReservationDate).IsRequired().HasColumnType("date");
            Property(r => r.Purpose).IsRequired().HasMaxLength(300).IsUnicode(true);
            Property(r => r.NumberOfMembers).IsRequired();
            Property(r => r.Status).IsRequired().HasMaxLength(20).IsUnicode(true);
            Property(r => r.CreatedAt).IsRequired().HasColumnType("datetime");
            Property(r => r.CheckedInAt).HasColumnType("datetime").IsOptional();

            HasRequired(r => r.Student)
                .WithMany(s => s.Reservations)
                .HasForeignKey(r => r.StudentID)
                .WillCascadeOnDelete(false);

            HasRequired(r => r.Room)
                .WithMany(x => x.Reservations)
                .HasForeignKey(r => r.RoomID)
                .WillCascadeOnDelete(false);

            HasRequired(r => r.TimeSlot)
                .WithMany(x => x.Reservations)
                .HasForeignKey(r => r.TimeSlotID)
                .WillCascadeOnDelete(false);

            // IX_Res_Date and IX_Res_Student.
            HasIndex(r => r.ReservationDate);
            HasIndex(r => r.StudentID);

            // UX_Res_ActiveSlot is a FILTERED unique index; EF 6 cannot express it.
            // It is created by raw SQL in the baseline migration.
        }
    }
}
