using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;

namespace BSIT3D_GUADES_Project_MVC.Controllers.Admin
{
    /// <summary>
    /// Phase 04 placeholder, retired in Phase 07.
    ///
    /// RouteConfig's "QCU-CRAM_Admin" route defaults to AdminDashboard/Index, so
    /// /admin, /admin/admindashboard and /admin/admindashboard/index all used to
    /// land on the placeholder cards. Phase 07 puts the real dashboard on
    /// AdminController.Dashboard, which owns [Route("~/admin")]. The attribute
    /// route is mapped first, so /admin already reaches the real screen; this
    /// class stays only so any bookmarked /admin/admindashboard URL still works,
    /// and it now forwards instead of rendering stale numbers.
    ///
    /// The two attributes are what make this an AdminPage: [QCUCRAM_RequireAdmin]
    /// is an IAuthorizationFilter, so it runs before [QCUCRAM_RequireLogin] - a
    /// signed-out visitor is sent to the login page, a signed-in student is sent
    /// to ?msg=role.
    /// </summary>
    [QCUCRAM_RequireAdmin]
    [QCUCRAM_RequireLogin]
    public class AdminDashboardController : QCUCRAM_BaseController
    {
        public ActionResult Index()
        {
            return RedirectToAction("Dashboard", "Admin");
        }
    }
}
