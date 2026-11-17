using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;

namespace BSIT3D_GUADES_Project_MVC.Controllers.Admin
{
    /// <summary>
    /// Phase 04 placeholder for Guades_QCUCram_Admin/Guades_QCUCram_Dashboard.aspx.
    ///
    /// It exists now only so the "admin" route has a real default target and a freshly
    /// signed-in admin lands somewhere instead of a 404. The dashboard cards, the
    /// pending queue and the approve / decline actions arrive in Phase 05.
    ///
    /// The two attributes are what make this an AdminPage: [QCUCRAM_RequireAdmin] is an
    /// IAuthorizationFilter, so it runs before [QCUCRAM_RequireLogin] - a signed-out
    /// visitor is sent to the login page, a signed-in student is sent to ?msg=role.
    /// </summary>
    [QCUCRAM_RequireAdmin]
    [QCUCRAM_RequireLogin]
    public class AdminDashboardController : QCUCRAM_BaseController
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Admin Dashboard";
            return View();
        }
    }
}
