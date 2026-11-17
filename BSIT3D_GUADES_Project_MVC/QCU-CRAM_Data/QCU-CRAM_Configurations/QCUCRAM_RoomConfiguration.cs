using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data.QCUCRAM_Configurations
{
    public class QCUCRAM_RoomConfiguration : EntityTypeConfiguration<QCUCRAM_Room>
    {
        public QCUCRAM_RoomConfiguration()
        {
            ToTable("Rooms", "dbo");
            HasKey(r => r.RoomID);
            Property(r => r.RoomID).HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity);

            Property(r => r.RoomName).IsRequired().HasMaxLength(50).IsUnicode(true);
            Property(r => r.MinCapacity).IsRequired();
            Property(r => r.MaxCapacity).IsRequired();
            Property(r => r.IsActive).IsRequired();

            HasIndex(r => r.RoomName).IsUnique();
            HasIndex(r => r.IsActive);
        }
    }
}
