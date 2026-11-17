using System;
using System.Web;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers
{
    /// <summary>
    /// ALWAYS use this instead of DateTime.Now so the Dev Tools time-shift works.
    ///
    /// The offset lives in Settings under "DevTimeOffsetMinutes" and is cached for the
    /// lifetime of the request in HttpContext.Items, so a page that calls Now fifty times
    /// still only reads the setting once.
    /// </summary>
    public static class QCUCRAM_Clock
    {
        private const string CacheKey = "QCUCRAM_ClockOffset";
        public const string SettingKey = "DevTimeOffsetMinutes";

        public static int OffsetMinutes
        {
            get
            {
                HttpContext ctx = HttpContext.Current;
                if (ctx != null && ctx.Items[CacheKey] != null)
                {
                    return (int)ctx.Items[CacheKey];
                }

                int offset = ReadOffset();
                if (ctx != null) { ctx.Items[CacheKey] = offset; }
                return offset;
            }
        }

        private static int ReadOffset()
        {
            try
            {
                using (QCUCRAM_Db db = new QCUCRAM_Db())
                {
                    QCUCRAM_Setting row = db.QueryFirstOrDefault<QCUCRAM_Setting>(
                        "SELECT SettingKey, SettingValue, Description FROM Settings WHERE SettingKey = @k",
                        QCUCRAM_Db.P("@k", SettingKey));

                    int parsed;
                    if (row != null && int.TryParse(row.SettingValue, out parsed)) { return parsed; }
                }
            }
            catch
            {
                // The clock must never be the reason a page fails; fall back to real time.
            }
            return 0;
        }

        /// <summary>Current time, shifted by the Dev Tools offset.</summary>
        public static DateTime Now
        {
            get { return DateTime.Now.AddMinutes(OffsetMinutes); }
        }

        /// <summary>Current date, shifted by the Dev Tools offset.</summary>
        public static DateTime Today
        {
            get { return Now.Date; }
        }

        /// <summary>Drops the cached offset so the next read picks up a Settings change.</summary>
        public static void Invalidate()
        {
            HttpContext ctx = HttpContext.Current;
            if (ctx != null) { ctx.Items.Remove(CacheKey); }
        }
    }
}
