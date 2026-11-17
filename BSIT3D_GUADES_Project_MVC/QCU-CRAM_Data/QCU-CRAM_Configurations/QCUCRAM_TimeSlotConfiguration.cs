using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data.QCUCRAM_Configurations
{
    public class QCUCRAM_TimeSlotConfiguration : EntityTypeConfiguration<QCUCRAM_TimeSlot>
    {
        public QCUCRAM_TimeSlotConfiguration()
        {
            ToTable("TimeSlots", "dbo");
            HasKey(t => t.TimeSlotID);
            Property(t => t.TimeSlotID).HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity);

            // TIME(0) - no fractional seconds.
            Property(t => t.StartTime).IsRequired().HasColumnType("time").HasPrecision(0);
            Property(t => t.EndTime).IsRequired().HasColumnType("time").HasPrecision(0);
            Property(t => t.IsActive).IsRequired();

            HasIndex(t => t.StartTime).IsUnique();
            HasIndex(t => t.IsActive);
        }
    }
}
