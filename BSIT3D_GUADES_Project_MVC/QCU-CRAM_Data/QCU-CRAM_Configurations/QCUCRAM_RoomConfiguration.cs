using System.Data.Entity;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Infrastructure.Annotations;
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

            Property(r => r.RoomName).HasColumnAnnotation(IndexAnnotation.AnnotationName, new IndexAnnotation(new IndexAttribute { IsUnique = true }));
        }
    }
}
