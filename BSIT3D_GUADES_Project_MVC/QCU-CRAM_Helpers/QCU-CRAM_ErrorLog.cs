using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Hosting;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers
{
    /// <summary>
    /// QCU-CRAM  |  QCU-CRAM_Helpers/QCU-CRAM_ErrorLog.cs
    ///
    /// The one place unhandled exceptions are written to disk. Replaces the ad-hoc
    /// file logging that used to live in Global.asax.cs.
    ///
    /// Design rules:
    ///   * Nothing here may throw. A logging failure must never turn into a second
    ///     exception on the error page.
    ///   * One file per day: QCU-CRAM_yyyy-MM-dd.log under ~/App_Data/logs.
    ///   * Files older than 30 days are pruned at most once an hour.
    ///   * Every entry carries a short reference code that the user sees on the error
    ///     page, so a support email can be matched to a line in the log.
    /// </summary>
    public static class QCUCRAMErrorLog
    {
        private const int RetentionDays = 30;
        private const int MaxEntryLength = 8000;

        private static readonly object _sync = new object();
        private static DateTime _lastPrune = DateTime.MinValue;

        /// <summary>The folder log files are written to. Never null.</summary>
        public static string FolderPath
        {
            get
            {
                string mapped = null;
                try { mapped = HostingEnvironment.MapPath("~/App_Data/logs"); }
                catch { /* not hosted - fall through to the base directory */ }

                if (string.IsNullOrWhiteSpace(mapped))
                {
                    mapped = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "logs");
                }

                return mapped;
            }
        }

        /// <summary>
        /// Writes an exception and returns the 8-character reference shown to the user.
        /// Returns an empty string only if even the reference could not be produced.
        /// </summary>
        public static string Write(Exception ex, string area, string reference)
        {
            string code = string.IsNullOrWhiteSpace(reference) ? NewReference() : reference;

            try
            {
                if (ex == null) { return code; }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("------------------------------------------------------------");
                sb.AppendLine("Reference : " + code);
                sb.AppendLine("When (UTC): " + DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture));
                sb.AppendLine("Area      : " + (area ?? "(unknown)"));
                sb.AppendLine("Type      : " + ex.GetType().FullName);
                sb.AppendLine("Message   : " + Truncate(ex.Message, 2000));
                sb.AppendLine("Source    : " + (ex.Source ?? string.Empty));
                sb.AppendLine("Target    : " + (ex.TargetSite != null ? ex.TargetSite.ToString() : string.Empty));
                sb.AppendLine("Stack     :");
                sb.AppendLine(Truncate(ex.StackTrace, MaxEntryLength));
                sb.AppendLine();

                Append(sb.ToString());
            }
            catch
            {
                // Logging must never throw.
            }

            return code;
        }

        /// <summary>Writes a plain message (no exception). Returns the reference code.</summary>
        public static string Write(string message, string area)
        {
            string code = NewReference();

            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("------------------------------------------------------------");
                sb.AppendLine("Reference : " + code);
                sb.AppendLine("When (UTC): " + DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture));
                sb.AppendLine("Area      : " + (area ?? "(unknown)"));
                sb.AppendLine("Message   : " + Truncate(message, MaxEntryLength));
                sb.AppendLine();

                Append(sb.ToString());
            }
            catch
            {
                // Logging must never throw.
            }

            return code;
        }

        // ------------------------------------------------------------------

        private static void Append(string text)
        {
            string folder = FolderPath;
            if (!Directory.Exists(folder)) { Directory.CreateDirectory(folder); }

            string file = Path.Combine(folder,
                "QCU-CRAM_" + DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");

            lock (_sync)
            {
                File.AppendAllText(file, text, Encoding.UTF8);
                Prune(folder);
            }
        }

        /// <summary>Deletes day-files older than the retention window, at most once an hour.</summary>
        private static void Prune(string folder)
        {
            try
            {
                if ((DateTime.UtcNow - _lastPrune).TotalHours < 1) { return; }
                _lastPrune = DateTime.UtcNow;

                DateTime cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
                foreach (string path in Directory.GetFiles(folder, "QCU-CRAM_*.log"))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(path) < cutoff) { File.Delete(path); }
                    }
                    catch { /* a locked file just stays another hour */ }
                }
            }
            catch
            {
                // Pruning is best-effort only.
            }
        }

        private static string NewReference()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) { return string.Empty; }
            return value.Length <= max ? value : value.Substring(0, max) + "...";
        }
    }
}
