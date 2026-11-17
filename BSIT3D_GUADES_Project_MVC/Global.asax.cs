using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
// Phase 2 adds: using System.Data.Entity;
// Phase 2 adds: using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;

namespace BSIT3D_GUADES_Project_MVC
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // Phase 2 enables this once QCUCRAMContext exists. EF must never
            // drop/recreate the database on model change; migrations are applied
            // explicitly (Update-Database) so schema drift can't destroy history.
            // Database.SetInitializer<QCUCRAMContext>(null);

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Phase 2/3 replaces this with the real initializer call.
            // QCUCRAMDbInitializer.RunIfConfigured();
        }

        protected void Application_BeginRequest()
        {
            // The Web Forms app set a no-cache policy on every secured page via
            // SecurePage.OnInit. MVC has no page lifecycle, so apply it globally.
            // Static assets are skipped so Bootstrap/jQuery stay cacheable, and
            // the login page opts out.
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
