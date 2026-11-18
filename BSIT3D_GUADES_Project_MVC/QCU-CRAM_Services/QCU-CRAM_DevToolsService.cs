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
    /// <summary>
    /// What one Dev Tools click produces: the toast (ShowMessage on lblMessage) and,
    /// for the two generators, the detail box that stays on the page (ShowResult).
    /// </summary>
    public class QCUCRAMDevResult
    {
        public string Toast { get; set; }
        public bool IsError { get; set; }
        public string DetailHtml { get; set; }

        public static QCUCRAMDevResult ToastOnly(string message, bool isError)
        {
            return new QCUCRAMDevResult { Toast = message, IsError = isError };
        }
    }

    public static class QCUCRAMDevToolsService
    {
        public const string OffsetKey = "DevTimeOffsetMinutes";
        public const int MaxOffset = 1000000;   // same limit as the Settings page

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Reads QCU-CRAM_DevToolsEnabled from Web.config. The Web Forms page has no such switch
        /// (it is always available to administrators), so the default stays true; setting the key
        /// to false is an extra way to switch every Dev Tools action off.
        /// </summary>
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
        /// <summary>btnShift_Command: toast only; an unknown command does nothing (source: "return;").</summary>
        public static QCUCRAMDevResult ShiftClock(string command)
        {
            if (string.Equals(command, "reset", StringComparison.OrdinalIgnoreCase))
            {
                SetOffset(0);
                return QCUCRAMDevResult.ToastOnly("Clock reset to real time.", false);
            }

            int delta;
            if (!int.TryParse(command, NumberStyles.Integer, Inv, out delta))
            {
                return null;
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
            return QCUCRAMDevResult.ToastOnly(
                "Clock moved forward. The offset is now " + next + " minute(s).", false);
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
        public static QCUCRAMDevResult GenerateStudents(int adminId)
        {
            try
            {
                QCUCRAM_SampleData.StudentBatch b =
                    QCUCRAM_SampleData.GenerateStudents(
                        QCUCRAM_SampleData.StudentsPerClick, adminId);

                return new QCUCRAMDevResult
                {
                    Toast = b.Created + " sample students added.",
                    IsError = false,
                    DetailHtml = Detail(
                        "<strong>" + b.Created + " students added</strong> (" +
                        b.Clean + " clean &middot; " + b.OneStrikeAway + " one strike away &middot; " +
                        b.RestrictedNow + " restricted now &middot; " + b.ExpiredRestriction +
                        " expired restriction; " +
                        (b.Created - b.Faculty - b.Visitors) + " students, " +
                        b.Faculty + " faculty, " + b.Visitors + " visitor). " +
                        "Matching forfeited reservations added: " + b.StrikeReservations + ".<br />" +
                        "Log in with: " + HttpUtility.HtmlEncode(string.Join(", ", b.Numbers.ToArray())) +
                        " &middot; password <code>" +
                        HttpUtility.HtmlEncode(QCUCRAM_SampleData.SamplePassword) + "</code>")
                };
            }
            catch (Exception)
            {
                return QCUCRAMDevResult.ToastOnly("Could not create the sample students.", true);
            }
        }

        public static QCUCRAMDevResult GenerateReservations(int daysBack, int daysAhead, int adminId)
        {
            try
            {
                // The Web Forms drop-downs only offer these values (event validation).
                if (Array.IndexOf(HistoryChoices, daysBack) < 0 || Array.IndexOf(AheadChoices, daysAhead) < 0)
                {
                    return QCUCRAMDevResult.ToastOnly("Could not create the sample reservations.", true);
                }

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

                return new QCUCRAMDevResult
                {
                    Toast = b.Created + " sample reservations added.",
                    IsError = false,
                    DetailHtml = Detail(html)
                };
            }
            catch (InvalidOperationException ex)
            {
                // "Generate the sample students first." / "There are no active rooms ..."
                return QCUCRAMDevResult.ToastOnly(ex.Message, true);
            }
            catch (Exception)
            {
                return QCUCRAMDevResult.ToastOnly("Could not create the sample reservations.", true);
            }
        }

        public static QCUCRAMDevResult ClearSampleReservations()
        {
            try
            {
                int n = QCUCRAM_SampleData.ClearSampleReservations();
                return QCUCRAMDevResult.ToastOnly(n + " sample reservations deleted.", false);
            }
            catch (Exception)
            {
                return QCUCRAMDevResult.ToastOnly("Could not delete the sample reservations.", true);
            }
        }

        public static QCUCRAMDevResult DeleteSampleStudents()
        {
            try
            {
                int n = QCUCRAM_SampleData.DeleteSampleStudents();
                return QCUCRAMDevResult.ToastOnly(n + " sample students deleted.", false);
            }
            catch (Exception)
            {
                return QCUCRAMDevResult.ToastOnly("Could not delete the sample students.", true);
            }
        }

        // ------------------------------------------------------------------ danger zone
        /// <summary>
        /// Mirrors btnClearAll_Click: the box must read DELETE (all capitals). The "I understand this
        /// cannot be undone." tick box belongs to the qcuConfirm pop-up on the button (requireCheck),
        /// exactly as in the .aspx, so the server only checks the typed word.
        /// </summary>
        public static QCUCRAMDevResult ClearAllReservations(string confirmText)
        {
            if (!string.Equals((confirmText ?? string.Empty).Trim(), "DELETE", StringComparison.Ordinal))
            {
                return QCUCRAMDevResult.ToastOnly(
                    "Type DELETE (all capitals) in the box to confirm. Nothing was deleted.", true);
            }

            try
            {
                int n = QCUCRAM_SampleData.ClearAllReservations();
                return QCUCRAMDevResult.ToastOnly(
                    "Deleted " + n + " reservation(s). Strikes and restrictions were reset.", false);
            }
            catch (Exception)
            {
                return QCUCRAMDevResult.ToastOnly("Could not clear the data.", true);
            }
        }

        // ------------------------------------------------------------------ helpers
        private static readonly int[] HistoryChoices = { 14, 30, 60 };
        private static readonly int[] AheadChoices = { 14, 21, 30 };

        /// <summary>DevTools.aspx.cs ShowResult(): the detail box that stays on the page.</summary>
        private static string Detail(string html)
        {
            return "<div class=\"alert alert-success py-2 small\">" + html + "</div>";
        }
    }
}
