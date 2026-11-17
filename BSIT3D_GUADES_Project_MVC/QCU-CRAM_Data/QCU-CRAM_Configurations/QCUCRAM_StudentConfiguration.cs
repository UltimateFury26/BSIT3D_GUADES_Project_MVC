using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data.QCUCRAM_Configurations
{
    public class QCUCRAM_StudentConfiguration : EntityTypeConfiguration<QCUCRAM_Student>
    {
        public QCUCRAM_StudentConfiguration()
        {
            ToTable("Students", "dbo");
            HasKey(s => s.StudentID);
            Property(s => s.StudentID).HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity);

            Property(s => s.StudentNumber).IsRequired().HasMaxLength(20).IsUnicode(true);
            Property(s => s.LastName).IsRequired().HasMaxLength(60).IsUnicode(true);
            Property(s => s.FirstName).IsRequired().HasMaxLength(60).IsUnicode(true);
            Property(s => s.MI).HasMaxLength(5).IsUnicode(true).IsOptional();
            Property(s => s.Email).IsRequired().HasMaxLength(120).IsUnicode(true);
            Property(s => s.Program).IsRequired().HasMaxLength(100).IsUnicode(true);
            Property(s => s.RequesterType).IsRequired().HasMaxLength(20).IsUnicode(true);
            Property(s => s.PasswordHash).IsRequired().HasMaxLength(200).IsUnicode(true);
            Property(s => s.NoShowCount).IsRequired();
            Property(s => s.IsRestricted).IsRequired();
            Property(s => s.RestrictionLiftDate).HasColumnType("datetime").IsOptional();
            Property(s => s.CreatedAt).IsRequired().HasColumnType("datetime");

            HasIndex(s => s.StudentNumber).IsUnique();
            HasIndex(s => s.Email).IsUnique();
            HasIndex(s => s.IsRestricted);
        }
    }
}
