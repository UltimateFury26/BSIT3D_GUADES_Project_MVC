using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;

namespace BSIT3D_GUADES_Project_MVC.Controllers
{
    /// <summary>
    /// Phase 04 - the MVC replacement for Guades_QCUCram_Default.aspx.
    ///
    /// The Web Forms page did no rendering at all: it looked at the session role and
    /// redirected to the student or admin dashboard, falling back to the login page.
    /// This controller does exactly the same, so "~/" keeps working as the entry point.
    ///
    /// The Phase 1 / Phase 2 smoke-test actions (QCUCRAM_DbCheck, QCUCRAM_ModelCheck)
    /// have been removed - they were unauthenticated diagnostics that read the database.
    /// </summary>
    public class HomeController : QCUCRAM_BaseController
    {
        public ActionResult Index()
        {
            if (QCUCRAM_Session.IsAdmin)
            {
                return Redirect(Url.Content(QCUCRAM_Constants.Paths.AdminHome));
            }

            if (QCUCRAM_Session.IsStudent)
            {
                return Redirect(Url.Content(QCUCRAM_Constants.Paths.StudentHome));
            }

            return Redirect(Url.Content(
                QCUCRAM_Constants.Paths.Login + "?" +
                QCUCRAM_Constants.QueryKeys.Message + "=" + QCUCRAM_Constants.Messages.Login));
        }
    }
}
