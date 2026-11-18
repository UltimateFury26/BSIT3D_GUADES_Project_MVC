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

            // ------------------------------------------------------------------
            //  Phase 04: the auth entry points, as short lower-case URLs.
            //  These are the MVC equivalents of
            //    ~/Guades_QCUCram_Account/Guades_QCUCram_Login.aspx
            //    ~/Guades_QCUCram_Account/Guades_QCUCram_Logout.aspx
            //    ~/Guades_QCUCram_Account/Guades_QCUCram_Register.aspx
            // ------------------------------------------------------------------
            routes.MapRoute(
                name: "QCU-CRAM_Login",
                url: "login",
                defaults: new { controller = "Account", action = "Login" });

            routes.MapRoute(
                name: "QCU-CRAM_Logout",
                url: "logout",
                defaults: new { controller = "Account", action = "Logout" });

            routes.MapRoute(
                name: "QCU-CRAM_Register",
                url: "register",
                defaults: new { controller = "Account", action = "Register" });

            routes.MapRoute(
                name: "QCU-CRAM_Error",
                url: "error",
                defaults: new { controller = "Error", action = "Index" });

            // ------------------------------------------------------------------
            //  Phase 33: the error pages Web.config redirects to, spelled out.
            //
            //  ~/error/notfound worked through the generic {controller}/{action} route,
            //  but only by accident of ordering - and it silently broke if anyone added a
            //  route above it. These three are declared here so the customErrors targets
            //  are guaranteed to resolve, and so /error/forbidden and /error/unauthorized
            //  exist for the 403/401 paths added in Phase 32.
            // ------------------------------------------------------------------
            routes.MapRoute(
                name: "QCU-CRAM_Error_NotFound",
                url: "error/notfound",
                defaults: new { controller = "Error", action = "NotFound" });

            routes.MapRoute(
                name: "QCU-CRAM_Error_Forbidden",
                url: "error/forbidden",
                defaults: new { controller = "Error", action = "Forbidden" });

            routes.MapRoute(
                name: "QCU-CRAM_Error_Unauthorized",
                url: "error/unauthorized",
                defaults: new { controller = "Error", action = "Unauthorized" });

            // ------------------------------------------------------------------
            //  Phase 04: the two signed-in shells. /admin/... and /student/...
            //  keep the Web Forms folder split (Guades_QCUCram_Admin,
            //  Guades_QCUCram_Student) so a role mistake is visible in the URL.
            // ------------------------------------------------------------------
            // ------------------------------------------------------------------
            //  Phase 05: AdminSetupController (Rooms, TimeSlots, Students, AdminUsers,
            //  Settings, DevTools) lives in the ROOT Controllers namespace and has no
            //  [Route] attributes. The generic "QCU-CRAM_Admin" route below is limited
            //  to Controllers.Admin, so Url.Action("Rooms", "AdminSetup") produced
            //  /admin/adminsetup/rooms but the request could not find the controller
            //  (404). This route keeps the same URL shape and points it at the right
            //  namespace. It must stay above "QCU-CRAM_Admin".
            // ------------------------------------------------------------------
            routes.MapRoute(
                name: "QCU-CRAM_AdminSetup",
                url: "admin/adminsetup/{action}/{id}",
                defaults: new { controller = "AdminSetup", action = "Rooms", id = UrlParameter.Optional },
                namespaces: new[] { "BSIT3D_GUADES_Project_MVC.Controllers" });

            routes.MapRoute(
                name: "QCU-CRAM_Admin",
                url: "admin/{controller}/{action}/{id}",
                defaults: new { controller = "AdminDashboard", action = "Index", id = UrlParameter.Optional },
                namespaces: new[] { "BSIT3D_GUADES_Project_MVC.Controllers.Admin" });

            routes.MapRoute(
                name: "QCU-CRAM_Student",
                url: "student/{controller}/{action}/{id}",
                defaults: new { controller = "StudentDashboard", action = "Index", id = UrlParameter.Optional },
                namespaces: new[] { "BSIT3D_GUADES_Project_MVC.Controllers.Student" });

            // ------------------------------------------------------------------
            //  Shared pages that both roles can open.
            // ------------------------------------------------------------------
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
