namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Migrations
{
    using System.Data.Entity.Migrations;
    using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;

    internal sealed class Configuration : DbMigrationsConfiguration<QCUCRAMContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
            AutomaticMigrationDataLossAllowed = false;
            MigrationsDirectory = "QCU-CRAM_Migrations";
            ContextKey = "BSIT3D_GUADES_Project_MVC.QCUCRAM_Data.QCUCRAMContext";
        }

        protected override void Seed(QCUCRAMContext context)
        {
            // Intentionally empty. Data is seeded by QCU-CRAM_Seed.sql
            // and QCUCRAM_AdminUserRepository.EnsureDefaultAdmin().
        }
    }
}
