using System;
using System.Diagnostics;
using System.Web;
using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Security
{
    /// <summary>
    /// The MVC equivalent of Guades_QCUCram_SecurePage.OnInit:
    ///
    ///   1. no session identity  -> redirect to the login page with ?msg=login
    ///   2. identity present but the account has gone away or been deactivated
    ///                           -> sign out and redirect with ?msg=deactivated
    ///
    /// The database re-check is throttled to QCUCRAM_Constants.VerifyIntervalSeconds so a
    /// signed-in user does not pay for it on every single request, and it is cached in
    /// HttpContext.Items so it happens at most once per request even with several filters.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class QCUCRAM_RequireLoginAttribute : ActionFilterAttribute
    {
        private const string CacheKey = "QCUCRAM_AccountChecked";

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null || filterContext.HttpContext == null) { return; }

            HttpContextBase http = filterContext.HttpContext;

            // ---- 1. is there a session identity at all? ----
            if (!QCUCRAM_Session.IsAuthenticated)
            {
                filterContext.Result = RedirectToLogin(http);
                return;
            }

            // ---- 2. is that account still usable? ----
            if (http.Items[CacheKey] != null) { return; }

            bool ok = true;
            string role = QCUCRAM_Session.Role;
            int id = QCUCRAM_Session.UserId;

            if (QCUCRAM_Session.NeedsVerification(http, QCUCRAM_Constants.VerifyIntervalSeconds))
            {
                try
                {
                    if (string.Equals(role, QCUCRAM_Constants.Roles.Admin, StringComparison.OrdinalIgnoreCase))
                    {
                        QCUCRAM_AdminUser account = new QCUCRAM_AdminUserRepository().GetById(id);
                        ok = account != null && account.IsActive;
                    }
                    else
                    {
                        // Students are never locked out of the site by a booking restriction;
                        // only a deleted row invalidates the session.
                        QCUCRAM_Student account = new QCUCRAM_StudentRepository().GetById(id);
                        ok = account != null;
                    }

                    if (ok) { QCUCRAM_Session.MarkVerified(http); }
                }
                catch (Exception ex)
                {
                    // A transient database problem must not sign everybody out.
                    Trace.TraceError("QCUCRAM_RequireLogin verification failed: " + ex);
                    ok = true;
                }
            }

            http.Items[CacheKey] = true;

            if (!ok)
            {
                QCUCRAM_Session.SignOut(http);
                filterContext.Result = new RedirectResult(
                    QCUCRAM_Constants.Paths.Login + "?" + QCUCRAM_Constants.QueryKeys.Message
                    + "=" + QCUCRAM_Constants.Messages.Deactivated);
            }
        }

        /// <summary>Builds ~/login?msg=login&amp;ReturnUrl=... for the current request.</summary>
        internal static RedirectResult RedirectToLogin(HttpContextBase http)
        {
            string returnUrl = http.Request.RawUrl;
            string url = QCUCRAM_Constants.Paths.Login
                         + "?" + QCUCRAM_Constants.QueryKeys.Message + "=" + QCUCRAM_Constants.Messages.Login;

            if (!string.IsNullOrEmpty(returnUrl))
            {
                url += "&" + QCUCRAM_Constants.QueryKeys.ReturnUrl + "=" + http.Server.UrlEncode(returnUrl);
            }

            return new RedirectResult(url);
        }
    }
}
