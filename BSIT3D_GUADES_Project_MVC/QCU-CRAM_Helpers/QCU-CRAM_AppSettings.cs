using System.Configuration;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers
{
    /// <summary>
    /// Single access point for configuration. No other file should read
    /// ConfigurationManager directly.
    /// </summary>
    public static class QCUCRAM_AppSettings
    {
        public const string ConnectionName = "QCU-CRAM_Connection";

        public static string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings[ConnectionName].ConnectionString; }
        }

        public static string AppName
        {
            get { return ConfigurationManager.AppSettings["QCU-CRAM_AppName"] ?? "QCU-CRAM"; }
        }

        public static string AppLongName
        {
            get { return ConfigurationManager.AppSettings["QCU-CRAM_AppLongName"] ?? "Quezon City University - Collaboration Room Access and Monitoring"; }
        }

        public static string SupportEmail
        {
            get { return ConfigurationManager.AppSettings["QCU-CRAM_SupportEmail"] ?? "cram@qcu.edu.ph"; }
        }

        public static string UploadFolder
        {
            get { return ConfigurationManager.AppSettings["QCU-CRAM_UploadFolder"] ?? "QCU-CRAM_Uploads"; }
        }

        public static int MaxUploadBytes
        {
            get
            {
                int value;
                return int.TryParse(ConfigurationManager.AppSettings["QCU-CRAM_MaxUploadBytes"], out value)
                    ? value : 5 * 1024 * 1024;
            }
        }

        public static int SweepIntervalSeconds
        {
            get
            {
                int value;
                return int.TryParse(ConfigurationManager.AppSettings["QCU-CRAM_SweepIntervalSeconds"], out value)
                    ? value : 30;
            }
        }

        public static bool EnableDevClock
        {
            get
            {
                bool value;
                return bool.TryParse(ConfigurationManager.AppSettings["QCU-CRAM_EnableDevClock"], out value) && value;
            }
        }

        public static bool SeedOnStartup
        {
            get
            {
                bool value;
                return bool.TryParse(ConfigurationManager.AppSettings["QCU-CRAM_SeedOnStartup"], out value) && value;
            }
        }
    }
}
