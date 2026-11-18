using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data.QCUCRAM_Configurations
{
    public class QCUCRAM_ReservationStatusHistoryConfiguration : EntityTypeConfiguration<QCUCRAM_ReservationStatusHistory>
    {
        public QCUCRAM_ReservationStatusHistoryConfiguration()
        {
            ToTable("ReservationStatusHistory", "dbo");
            HasKey(h => h.HistoryID);
            Property(h => h.HistoryID).HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity);

            Property(h => h.ReservationID).IsRequired();
            Property(h => h.OldStatus).HasMaxLength(20).IsUnicode(true).IsOptional();
            Property(h => h.NewStatus).IsRequired().HasMaxLength(20).IsUnicode(true);
            Property(h => h.ChangedByRole).IsRequired().HasMaxLength(10).IsUnicode(true);
            Property(h => h.ChangedBy).IsOptional();
            Property(h => h.ChangedAt).IsRequired().HasColumnType("datetime");
            Property(h => h.Notes).HasMaxLength(300).IsUnicode(true).IsOptional();

            HasRequired(h => h.Reservation)
                .WithMany(r => r.StatusHistory)
                .HasForeignKey(h => h.ReservationID)
                .WillCascadeOnDelete(true);

        }
    }
}
