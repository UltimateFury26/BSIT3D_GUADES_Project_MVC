using System;
using System.Configuration;
using System.Diagnostics;
using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Services;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Filters
{
    /// <summary>
    /// QCU-CRAM  |  QCU-CRAM_Filters/QCU-CRAM_SweepOnRequestAttribute.cs
    ///
    /// A cheap heartbeat: on a normal page GET it asks the maintenance service to run the
    /// sweep if it is due. TryRunIfDue never throws and does nothing when the last run is
    /// still fresh, so this costs one comparison on almost every request.
    ///
    /// This complements QCUCRAMSweepTimer (which runs on a background thread). On shared
    /// hosting where the app pool is recycled for inactivity the timer can be asleep, and
    /// this request-driven call keeps expiries and no-shows from piling up.
    /// </summary>
    public sealed class QCUCRAMSweepOnRequestAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

            try
            {
                if (filterContext == null) { return; }

                // Child actions (Html.Action / RenderAction) must not trigger a sweep.
                if (filterContext.IsChildAction) { return; }

                // Only reads. A POST is already doing real work of its own.
                if (filterContext.HttpContext == null || filterContext.HttpContext.Request == null) { return; }
                if (!string.Equals(filterContext.HttpContext.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // A page that switched the sweep off (Dev Tools) stays off here too.
                if (filterContext.HttpContext.Items[QCUCRAM_BaseController.SkipSweepKey] != null) { return; }

                if (!IsOn("QCU-CRAM_SweepOnRequest", true)) { return; }

                // Phase 17: DevTools.aspx.cs RunSweepOnLoad = false - that page must not sweep
                // by itself, so "Run sweep now" reports real counts.
                if (filterContext.ActionDescriptor != null &&
                    string.Equals(filterContext.ActionDescriptor.ActionName, "DevTools",
                                  StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                int seconds = ReadInt("QCU-CRAM_SweepIntervalSeconds", 30, 5);

                QCUCRAM_MaintenanceService.SweepResult result = QCUCRAM_MaintenanceService.RunIfDue(seconds);
                if (result != null && result.ChangedAnything)
                {
                    Trace.TraceInformation("QCU-CRAM sweep (on request): " + result.Summary);
                }
            }
            catch (Exception ex)
            {
                // A maintenance problem must never break the page the user asked for.
                QCUCRAMErrorLog.Write(ex, "sweep-on-request", null);
            }
        }

        private static bool IsOn(string key, bool fallback)
        {
            string raw = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(raw)) { return fallback; }
            return string.Equals(raw.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }

        private static int ReadInt(string key, int fallback, int minimum)
        {
            string raw = ConfigurationManager.AppSettings[key];
            int value;
            if (!int.TryParse(raw, out value)) { value = fallback; }
            return value < minimum ? minimum : value;
        }
    }
}
