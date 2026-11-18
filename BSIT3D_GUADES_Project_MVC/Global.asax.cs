using System;
using System.Data.Entity;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Infrastructure;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Services;

namespace BSIT3D_GUADES_Project_MVC
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            // The database is owned by the SQL scripts. EF must never drop/recreate it.
            Database.SetInitializer<QCUCRAMContext>(null);

            // Do not advertise the MVC version in a response header.
            MvcHandler.DisableMvcResponseHeader = true;

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Make sure a default library assistant exists (admin / admin123).
            // Wrapped so a database problem shows up as a normal page error later instead
            // of stopping the whole site from starting.
            try
            {
                new QCUCRAM_AdminUserRepository().EnsureDefaultAdmin();
            }
            catch (Exception ex)
            {
                QCUCRAMErrorLog.Write(ex, "application-start/EnsureDefaultAdmin", null);
            }

            // Phase 10: the hosted background sweep timer (expiries, no-shows,
            // completions, restriction lifts). QCUCRAM_MaintenanceService keeps its
            // own StartBackgroundTimer for compatibility, but the timer below is the
            // one wired to the app lifecycle.
            try
            {
                QCUCRAMSweepTimer.Start();
            }
            catch (Exception ex)
            {
                QCUCRAMErrorLog.Write(ex, "application-start/sweep-timer", null);
            }
        }

        protected void Application_End(object sender, EventArgs e)
        {
            try { QCUCRAMSweepTimer.Stop(); }
            catch { /* shutting down */ }

            try { QCUCRAM_MaintenanceService.StopBackgroundTimer(); }
            catch { /* shutting down */ }
        }

        /// <summary>
        /// No-cache for authenticated HTML so the browser Back button never shows a stale
        /// dashboard after a reservation changed. The login page is exempt, and so are
        /// static files and bundles: Web.config gives them a 7-day client cache and the
        /// Web Forms site only ever sent no-cache from SecurePage (pages), never for
        /// CSS/JS/images.
        /// </summary>
        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            try
            {
                HttpRequest request = Context != null ? Context.Request : null;
                HttpResponse response = Context != null ? Context.Response : null;
                if (request == null || response == null) { return; }

                string path = request.AppRelativeCurrentExecutionFilePath ?? string.Empty;
                if (path.IndexOf("/login", StringComparison.OrdinalIgnoreCase) >= 0) { return; }
                if (IsStaticAsset(path)) { return; }

                response.Cache.SetCacheability(HttpCacheability.NoCache);
                response.Cache.SetExpires(DateTime.UtcNow.AddDays(-1));
                response.Cache.SetNoStore();
                response.AppendHeader("Pragma", "no-cache");
            }
            catch
            {
                // Never let a header tweak break the request.
            }
        }

        private static bool IsStaticAsset(string appRelativePath)
        {
            if (string.IsNullOrEmpty(appRelativePath)) { return false; }

            // Bundles have no file extension, so they are listed by prefix.
            if (appRelativePath.StartsWith("~/content/", StringComparison.OrdinalIgnoreCase)
                || appRelativePath.StartsWith("~/scripts/", StringComparison.OrdinalIgnoreCase)
                || appRelativePath.StartsWith("~/bundles/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return System.IO.Path.HasExtension(appRelativePath);
        }

        /// <summary>
        /// Rebuilds HttpContext.User from the session identity so User.IsInRole("Admin")
        /// works in views and controllers.
        ///
        /// The Web Forms Global.asax had no principal step (its pages read Session directly),
        /// so this is an MVC-side convenience, not a port. It runs at PostAcquireRequestState
        /// rather than PostAuthenticateRequest because Session state is not readable until the
        /// session module has run - reading it earlier throws "Session state is not available".
        /// </summary>
        protected void Application_PostAcquireRequestState(object sender, EventArgs e)
        {
            if (Context == null || Context.Session == null) { return; }

            string role = Context.Session[QCUCRAM_Constants.SessionKeys.Role] as string;
            object idValue = Context.Session[QCUCRAM_Constants.SessionKeys.UserId];
            string name = Context.Session[QCUCRAM_Constants.SessionKeys.UserName] as string;

            int userId = (idValue is int) ? (int)idValue : 0;
            if (string.IsNullOrEmpty(role) || userId <= 0)
            {
                Context.User = new GenericPrincipal(new GenericIdentity(string.Empty), new string[0]);
                return;
            }

            Context.User = new GenericPrincipal(
                new GenericIdentity(string.IsNullOrWhiteSpace(name) ? role : name),
                new[] { role });
        }

        protected void Session_Start(object sender, EventArgs e)
        {
            // Session keys are created lazily by QCUCRAM_Session.SignIn; nothing to do here.
        }

        protected void Session_End(object sender, EventArgs e)
        {
            // InProc session expiry: nothing to clean up beyond what the runtime drops.
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();
            if (ex == null) { return; }

            string area = "application-error";
            try
            {
                if (Request != null) { area += " " + Request.RawUrl; }
            }
            catch { /* request may already be gone */ }

            // QCUCRAMHandleErrorAttribute normally handles MVC exceptions first; this
            // catches anything that escapes it (routing, static files, module errors).
            QCUCRAMErrorLog.Write(ex, area, null);
        }
    }
}
