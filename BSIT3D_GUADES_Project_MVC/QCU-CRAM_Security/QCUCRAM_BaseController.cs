using System;
using System.Diagnostics;
using System.Web;
using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Services;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Security
{
    /// <summary>
    /// Shared plumbing for every QCU-CRAM controller.
    ///
    /// Ports three things from Guades_QCUCram_SecurePage:
    ///   * the no-cache headers, so the browser Back button cannot show a signed-in page
    ///     after logout;
    ///   * the automatic sweep (expiries, forfeits, completions, restriction lifts) that ran
    ///     in OnLoad, throttled to QCU-CRAM_SweepIntervalSeconds and never allowed to throw;
    ///   * the layout's "who is signed in" fields, plus the unread-notification badge count.
    /// </summary>
    public abstract class QCUCRAM_BaseController : Controller
    {
        // ---- the CurrentUser properties of the original base page ----
        protected int CurrentUserId { get { return QCUCRAM_Session.UserId; } }
        protected string CurrentRole { get { return QCUCRAM_Session.Role; } }
        protected string CurrentUserName { get { return QCUCRAM_Session.UserName; } }
        protected bool IsAdmin { get { return QCUCRAM_Session.IsAdmin; } }
        protected bool IsStudent { get { return QCUCRAM_Session.IsStudent; } }

        /// <summary>HttpContext.Items flag read by the request-sweep filter.</summary>
        public const string SkipSweepKey = "QCUCRAM_SkipSweep";

        /// <summary>
        /// SecurePage.RunSweepOnLoad (default true). A controller returns false for the actions
        /// that must not run the automatic rules before they render.
        /// </summary>
        protected virtual bool RunSweepOnLoad(string actionName)
        {
            return true;
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

            HttpContextBase http = filterContext.HttpContext;

            // ---- SecurePage.OnInit: no cached copies of a signed-in page ----
            http.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            http.Response.Cache.SetNoStore();
            http.Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));

            // ---- SecurePage.OnLoad: run the automatic rules before the page reads data ----
            // SecurePage.RunSweepOnLoad: pages that must not sweep (Dev Tools) switch it off.
            string actionName = filterContext.ActionDescriptor != null ? filterContext.ActionDescriptor.ActionName : null;
            if (!RunSweepOnLoad(actionName))
            {
                http.Items[SkipSweepKey] = true;   // the global request sweep honours this too
            }
            else if (QCUCRAM_Session.IsAuthenticated)
            {
                QCUCRAM_MaintenanceService.TryRunIfDue(QCUCRAM_AppSettings.SweepIntervalSeconds);
            }

            // ---- Site.Master: the fields the layout binds to ----
            ViewBag.QCUCRAM_CurrentUserName = QCUCRAM_Session.UserName;
            ViewBag.QCUCRAM_CurrentRole = QCUCRAM_Session.Role;
            ViewBag.QCUCRAM_IsAdmin = QCUCRAM_Session.IsAdmin;
            ViewBag.QCUCRAM_IsStudent = QCUCRAM_Session.IsStudent;
            ViewBag.QCUCRAM_IsAuthenticated = QCUCRAM_Session.IsAuthenticated;
            ViewBag.QCUCRAM_UnreadCount = UnreadCount();

            // The clock banner in the layout (only when Dev Tools has shifted the clock).
            // Phase 05: Site.Master showed the banner whenever the offset was not 0. QCUCRAM_Clock.Now
            // applies the offset regardless of QCU-CRAM_EnableDevClock, so the banner must too.
            ViewBag.QCUCRAM_ClockOffset = QCUCRAM_Clock.OffsetMinutes;
        }

        /// <summary>Unread notifications for the signed-in student; 0 for guests and admins.</summary>
        protected int UnreadCount()
        {
            if (!QCUCRAM_Session.IsStudent) { return 0; }

            try
            {
                return new QCUCRAM_NotificationRepository().CountUnread(QCUCRAM_Session.UserId);
            }
            catch (Exception ex)
            {
                // A badge must never break a page.
                Trace.TraceError("QCUCRAM unread count failed: " + ex);
                return 0;
            }
        }

        // ---- feedback helpers (SecurePage.ShowMessage, toast flavour) ----

        /// <summary>Queues a green message for the next rendered page.</summary>
        protected void Flash(string message)
        {
            TempData[QCUCRAM_Constants.TempDataKeys.Success] = message;
        }

        /// <summary>Queues a red message for the next rendered page.</summary>
        protected void FlashError(string message)
        {
            TempData[QCUCRAM_Constants.TempDataKeys.Error] = message;
        }

        /// <summary>
        /// Only allow a local return URL, never an absolute one (open-redirect guard).
        /// Returns the fallback when the value is unusable.
        /// </summary>
        protected static string SafeReturnUrl(string returnUrl, string fallback)
        {
            if (string.IsNullOrWhiteSpace(returnUrl)) { return fallback; }
            if (returnUrl.IndexOf("://", StringComparison.Ordinal) >= 0) { return fallback; }
            if (returnUrl.StartsWith("//", StringComparison.Ordinal)) { return fallback; }
            if (!returnUrl.StartsWith("/", StringComparison.Ordinal)) { return fallback; }

            return returnUrl;
        }
    }
}
