using System;
using System.Data.Entity;
using System.IO;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
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
                System.Diagnostics.Trace.TraceError("EnsureDefaultAdmin failed: " + ex);
            }

            // Background sweep: forfeits, expiries, completions, restriction lifts.
            try
            {
                QCUCRAM_MaintenanceService.StartBackgroundTimer(60);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("StartBackgroundTimer failed: " + ex);
            }
        }

        protected void Application_End(object sender, EventArgs e)
        {
            QCUCRAM_MaintenanceService.StopBackgroundTimer();
        }

        /// <summary>
        /// Rebuilds HttpContext.User from the session identity so User.IsInRole("Admin")
        /// works in views and controllers.
        ///
        /// This is the MVC replacement for Global.Application_PostAuthenticateRequest in the
        /// Web Forms project. It runs at PostAcquireRequestState rather than
        /// PostAuthenticateRequest because Session state is not readable until the session
        /// module has run - reading it earlier throws "Session state is not available".
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

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();
            if (ex == null) { return; }

            try
            {
                string folder = Server.MapPath("~/App_Data");
                if (!Directory.Exists(folder)) { Directory.CreateDirectory(folder); }

                string logPath = Path.Combine(folder, "QCU-CRAM_error.log");
                string entry = string.Format("{0:u} | {1} | {2}{3}{4}{5}",
                    DateTime.UtcNow,
                    Request != null ? Request.RawUrl : "(no request)",
                    ex.GetType().Name,
                    Environment.NewLine, ex, Environment.NewLine + "----" + Environment.NewLine);

                File.AppendAllText(logPath, entry);
            }
            catch
            {
                // Logging must never throw.
            }
        }
    }
}
