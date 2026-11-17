using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data.QCUCRAM_Configurations
{
    public class QCUCRAM_AdminUserConfiguration : EntityTypeConfiguration<QCUCRAM_AdminUser>
    {
        public QCUCRAM_AdminUserConfiguration()
        {
            ToTable("AdminUsers", "dbo");
            HasKey(a => a.AdminID);
            Property(a => a.AdminID).HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity);

            Property(a => a.Username).IsRequired().HasMaxLength(50).IsUnicode(true);
            Property(a => a.PasswordHash).IsRequired().HasMaxLength(200).IsUnicode(true);
            Property(a => a.FullName).IsRequired().HasMaxLength(120).IsUnicode(true);
            Property(a => a.IsActive).IsRequired();

            HasIndex(a => a.Username).IsUnique();
        }
    }
}
