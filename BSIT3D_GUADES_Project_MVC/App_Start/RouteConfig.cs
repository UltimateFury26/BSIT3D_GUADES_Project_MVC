using System.Web.Mvc;
using System.Web.Routing;

namespace BSIT3D_GUADES_Project_MVC
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // Let [Route] / [RoutePrefix] attributes on controllers win.
            routes.MapMvcAttributeRoutes();

            routes.LowercaseUrls = true;

            // Friendly, lower-case URLs that mirror the QCU-CRAM information
            // architecture. These are conventional routes so that a typo in an
            // attribute route never leaves a page unreachable.
            routes.MapRoute(
                name: "QCU-CRAM_Reports_PrintBooking",
                url: "reports/print-booking/{id}",
                defaults: new { controller = "Reports", action = "PrintBooking", id = UrlParameter.Optional });

            routes.MapRoute(
                name: "QCU-CRAM_Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional });
        }
    }
}
