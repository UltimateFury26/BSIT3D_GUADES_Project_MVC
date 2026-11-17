using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Data.Entity;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;

namespace BSIT3D_GUADES_Project_MVC
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // The database is owned by Guades_QCUCram_Schema.sql. EF must never
            // drop/recreate it on model change; migrations are applied explicitly.
            Database.SetInitializer<QCUCRAMContext>(null);

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Phase 3 replaces this with the real initializer call.
            // QCUCRAMDbInitializer.RunIfConfigured();
        }

        protected void Application_BeginRequest()
        {
            var path = Request.AppRelativeCurrentExecutionFilePath ?? string.Empty;

            if (path.StartsWith("~/Content", System.StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("~/Scripts", System.StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("~/bundles", System.StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("login", System.StringComparison.OrdinalIgnoreCase))
                return;

            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.Cache.SetExpires(System.DateTime.UtcNow.AddDays(-1));
        }

        protected void Application_Error()
        {
            // Phase 10 replaces this with structured logging.
        }
    }
}
