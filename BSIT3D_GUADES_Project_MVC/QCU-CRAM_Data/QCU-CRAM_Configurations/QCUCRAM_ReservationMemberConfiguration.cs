using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data.QCUCRAM_Configurations
{
    public class QCUCRAM_ReservationMemberConfiguration : EntityTypeConfiguration<QCUCRAM_ReservationMember>
    {
        public QCUCRAM_ReservationMemberConfiguration()
        {
            ToTable("ReservationMembers", "dbo");
            HasKey(m => m.MemberID);
            Property(m => m.MemberID).HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity);

            Property(m => m.ReservationID).IsRequired();
            Property(m => m.LastName).IsRequired().HasMaxLength(60).IsUnicode(true);
            Property(m => m.FirstName).IsRequired().HasMaxLength(60).IsUnicode(true);
            Property(m => m.MI).HasMaxLength(5).IsUnicode(true).IsOptional();

            HasRequired(m => m.Reservation)
                .WithMany(r => r.Members)
                .HasForeignKey(m => m.ReservationID)
                .WillCascadeOnDelete(true);
        }
    }
}
