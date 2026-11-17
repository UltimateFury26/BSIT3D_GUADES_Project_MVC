using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data.QCUCRAM_Configurations
{
    public class QCUCRAM_SettingConfiguration : EntityTypeConfiguration<QCUCRAM_Setting>
    {
        public QCUCRAM_SettingConfiguration()
        {
            ToTable("Settings", "dbo");
            HasKey(s => s.SettingKey);

            Property(s => s.SettingKey).IsRequired().HasMaxLength(50).IsUnicode(true);
            Property(s => s.SettingValue).IsRequired().HasMaxLength(100).IsUnicode(true);
            Property(s => s.Description).HasMaxLength(200).IsUnicode(true).IsOptional();
        }
    }
}
