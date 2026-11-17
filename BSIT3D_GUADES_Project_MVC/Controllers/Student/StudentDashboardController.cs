using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;

namespace BSIT3D_GUADES_Project_MVC.Controllers.Student
{
    /// <summary>
    /// Phase 04 placeholder for Guades_QCUCram_Student/Guades_QCUCram_Dashboard.aspx.
    ///
    /// It exists now only so the "student" route has a real default target and a freshly
    /// signed-in student lands somewhere instead of a 404. The upcoming-bookings card,
    /// the notifications panel and the Reserve shortcut arrive in Phase 05.
    /// </summary>
    [QCUCRAM_RequireStudent]
    [QCUCRAM_RequireLogin]
    public class StudentDashboardController : QCUCRAM_BaseController
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Student Dashboard";
            return View();
        }
    }
}
