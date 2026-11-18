using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Web;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Services
{
    /// <summary>
    /// Phase 08 - the Developer Tools screen.
    ///
    /// Port of Guades_QCUCram_Admin/Guades_QCUCram_DevTools.aspx.cs. Every
    /// message string is copied verbatim from the original page so the MVC
    /// screen reads exactly like the Web Forms screen.
    ///
    /// Everything here exists for demonstration and grading only. In a real
    /// deployment set QCU-CRAM_DevToolsEnabled to false in Web.config and the
    /// controller will answer 404.
    /// </summary>
    public static class QCUCRAMDevToolsService
    {
        public const string OffsetKey = "DevTimeOffsetMinutes";
        public const int MaxOffset = 1000000;   // same limit as the Settings page

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Reads QCU-CRAM_DevToolsEnabled from Web.config (default true).</summary>
        public static bool Enabled
        {
            get
            {
                string raw = ConfigurationManager.AppSettings["QCU-CRAM_DevToolsEnabled"];
                bool value;
                if (bool.TryParse(raw, out value)) { return value; }
                return true;
            }
        }

        // ------------------------------------------------------------------ page state
        public static QCUCRAMDevToolsViewModel Load()
        {
            int offset;

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                offset = new QCUCRAM_ContentRepository(db).GetSettingInt(OffsetKey, 0);
            }

            DateTime real = DateTime.Now;

            QCUCRAMDevToolsViewModel vm = new QCUCRAMDevToolsViewModel
            {
                RealNow = real,
                SimulatedNow = real.AddMinutes(offset),
                OffsetMinutes = offset
            };

            QCUCRAM_SampleData.Counts c = QCUCRAM_SampleData.GetCounts();

            vm.Students = c.Students;
            vm.SampleStudents = c.SampleStudents;
            vm.SampleRestricted = c.SampleRestricted;
            vm.SampleOneStrikeAway = c.SampleOneStrikeAway;
            vm.Reservations = c.Reservations;
            vm.SampleReservations = c.SampleReservations;
            vm.Notifications = c.Notifications;

            return vm;
        }

        // ------------------------------------------------------------------ clock
        public static string ShiftClock(string command)
        {
            if (string.Equals(command, "reset", StringComparison.OrdinalIgnoreCase))
            {
                SetOffset(0);
                return Message("Clock reset to real time.", false);
            }

            int delta;
            if (!int.TryParse(command, NumberStyles.Integer, Inv, out delta))
            {
                return Message("Unknown clock command.", true);
            }

            int current;
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                current = new QCUCRAM_ContentRepository(db).GetSettingInt(OffsetKey, 0);
            }

            long next = (long)current + delta;
            if (next < 0) { next = 0; }
            if (next > MaxOffset) { next = MaxOffset; }

            SetOffset((int)next);
            return Message("Clock moved forward. The offset is now " + next + " minute(s).", false);
        }

        private static void SetOffset(int minutes)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                new QCUCRAM_ContentRepository(db).SetSetting(
                    OffsetKey, minutes.ToString(Inv));
            }

            QCUCRAM_Clock.Invalidate();
        }

        // ------------------------------------------------------------------ sweep
        public static string RunSweep()
        {
            try
            {
                QCUCRAM_MaintenanceService.SweepResult result =
                    QCUCRAM_MaintenanceService.RunSweep();

                string css = result.Errors > 0
                    ? "alert-danger"
                    : (result.ChangedAnything ? "alert-success" : "alert-info");

                return "<div class=\"alert " + css + "\">" +
                       HttpUtility.HtmlEncode(result.Summary) + "</div>";
            }
            catch (Exception ex)
            {
                return "<div class=\"alert alert-danger\">The sweep failed: " +
                       HttpUtility.HtmlEncode(ex.Message) + "</div>";
            }
        }

        // ------------------------------------------------------------------ sample data
        public static string GenerateStudents(int adminId)
        {
            try
            {
                QCUCRAM_SampleData.StudentBatch b =
                    QCUCRAM_SampleData.GenerateStudents(
                        QCUCRAM_SampleData.StudentsPerClick, adminId);

                return Message(
                    "<strong>" + b.Created + " students added</strong> (" +
                    b.Clean + " clean &middot; " + b.OneStrikeAway + " one strike away &middot; " +
                    b.RestrictedNow + " restricted now &middot; " + b.ExpiredRestriction +
                    " expired restriction; " +
                    (b.Created - b.Faculty - b.Visitors) + " students, " +
                    b.Faculty + " faculty, " + b.Visitors + " visitor). " +
                    "Matching forfeited reservations added: " + b.StrikeReservations + ".<br />" +
                    "Log in with: " + HttpUtility.HtmlEncode(string.Join(", ", b.Numbers.ToArray())) +
                    " &middot; password <code>" +
                    HttpUtility.HtmlEncode(QCUCRAM_SampleData.SamplePassword) + "</code>",
                    false);
            }
            catch (Exception)
            {
                return Message("Could not create the sample students.", true);
            }
        }

        public static string GenerateReservations(int daysBack, int daysAhead, int adminId)
        {
            try
            {
                QCUCRAM_SampleData.ReservationBatch b =
                    QCUCRAM_SampleData.GenerateReservations(daysBack, daysAhead, adminId);

                string[] order =
                {
                    "Pending", "Approved", "Reassigned", "Declined",
                    "Cancelled", "CheckedIn", "Completed", "Forfeited"
                };

                List<string> parts = new List<string>();
                foreach (string s in order) { parts.Add(s + " " + b.Count(s)); }

                string dates = (b.FirstDate.HasValue && b.LastDate.HasValue)
                    ? ", " + b.FirstDate.Value.ToString("MMM d", Inv) +
                      " to " + b.LastDate.Value.ToString("MMM d, yyyy", Inv)
                    : "";

                string html =
                    "<strong>" + b.Created + " sample reservations added</strong> (" +
                    string.Join(" &middot; ", parts.ToArray()) + "). " +
                    b.RoomsCovered + " of " + b.RoomsTotal + " rooms covered" + dates + ".";

                if (b.StrikeReservations > 0)
                {
                    html += " " + b.StrikeReservations +
                            " of them are forfeited reservations that back the students' strikes.";
                }

                if (b.CheckedInSkipped)
                {
                    html += "<br /><strong>No CheckedIn reservation was made:</strong> " +
                            "no time slot is running right now. Move the clock into library " +
                            "hours (clock buttons above) and click again.";
                }

                return Message(html, false);
            }
            catch (InvalidOperationException ex)
            {
                // "Generate the sample students first." / "There are no active rooms ..."
                return Message(HttpUtility.HtmlEncode(ex.Message), true);
            }
            catch (Exception)
            {
                return Message("Could not create the sample reservations.", true);
            }
        }

        public static string ClearSampleReservations()
        {
            try
            {
                int n = QCUCRAM_SampleData.ClearSampleReservations();
                return Message(n + " sample reservations deleted.", false);
            }
            catch (Exception)
            {
                return Message("Could not delete the sample reservations.", true);
            }
        }

        public static string DeleteSampleStudents()
        {
            try
            {
                int n = QCUCRAM_SampleData.DeleteSampleStudents();
                return Message(n + " sample students deleted.", false);
            }
            catch (Exception)
            {
                return Message("Could not delete the sample students.", true);
            }
        }

        // ------------------------------------------------------------------ danger zone
        /// <summary>
        /// Mirrors btnClearAll_Click: the box must read DELETE (all capitals) and
        /// the "I understand" check must be ticked before anything is removed.
        /// </summary>
        public static string ClearAllReservations(string confirmText, bool confirmCheck)
        {
            if (!string.Equals((confirmText ?? string.Empty).Trim(), "DELETE", StringComparison.Ordinal))
            {
                return Message(
                    "Type DELETE (all capitals) in the box to confirm. Nothing was deleted.", true);
            }

            if (!confirmCheck)
            {
                return Message(
                    "Tick \u201cI understand this cannot be undone.\u201d to confirm. Nothing was deleted.", true);
            }

            try
            {
                int n = QCUCRAM_SampleData.ClearAllReservations();
                return Message(
                    "Deleted " + n + " reservation(s). Strikes and restrictions were reset.", false);
            }
            catch (Exception)
            {
                return Message("Could not clear the data.", true);
            }
        }

        // ------------------------------------------------------------------ helper
        /// <summary>Same wrapper the original ShowResult() / ShowMessage() produce.</summary>
        private static string Message(string html, bool isError)
        {
            string css = isError
                ? "alert alert-danger py-2 small"
                : "alert alert-success py-2 small";

            return "<div class=\"" + css + "\">" + html + "</div>";
        }
    }
}
