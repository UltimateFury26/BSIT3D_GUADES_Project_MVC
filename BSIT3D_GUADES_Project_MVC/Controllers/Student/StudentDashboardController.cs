using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;

namespace BSIT3D_GUADES_Project_MVC.Controllers.Student
{
    /// <summary>
    /// Phase 04 placeholder for Guades_QCUCram_Student/Guades_QCUCram_Dashboard.aspx,
    /// retired in Phase 05.
    ///
    /// RouteConfig's "QCU-CRAM_Student" route defaults to StudentDashboard/Index, so
    /// /student/studentdashboard and /student/studentdashboard/index used to land on
    /// the Phase 04 "coming in Phase 05" cards. Phase 05 puts the real dashboard on
    /// StudentController.Dashboard, which owns [Route("~/student")]. The attribute
    /// route is mapped first, so /student already reaches the real screen; this class
    /// stays only so any bookmarked /student/studentdashboard URL still works, and it
    /// now forwards instead of rendering stale numbers.
    ///
    /// The two attributes are what make this a StudentPage: [QCUCRAM_RequireStudent]
    /// is an IAuthorizationFilter, so it runs before [QCUCRAM_RequireLogin] - a
    /// signed-out visitor is sent to the login page, a signed-in admin is sent to
    /// ?msg=role.
    /// </summary>
    [QCUCRAM_RequireStudent]
    [QCUCRAM_RequireLogin]
    public class StudentDashboardController : QCUCRAM_BaseController
    {
        public ActionResult Index()
        {
            return RedirectToAction("Dashboard", "Student");
        }
    }
}
