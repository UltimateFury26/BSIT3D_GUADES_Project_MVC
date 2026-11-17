using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;

namespace BSIT3D_GUADES_Project_MVC.Controllers
{
    /// <summary>
    /// A one-action shim so the "Restrictions" link already sitting in
    /// _QCU-CRAM_LoginPartial.cshtml keeps working.
    ///
    /// That partial was written in Phase 01 against the Web Forms page names and
    /// calls Url.Action("Index", "AdminRestrictions"), which the conventional
    /// "admin" route turns into /admin/adminrestrictions/index. Phase 07 puts the
    /// real screen on AdminController.Restrictions (/admin/restrictions), so this
    /// class simply forwards. It carries the same two security attributes through
    /// QCUCRAM_AdminController, so the redirect can never be used to probe.
    ///
    /// Keeping the shim instead of editing the partial means the nav markup stays
    /// exactly as Phase 01 left it and nothing else has to be re-verified.
    /// </summary>
    [RoutePrefix("admin/adminrestrictions")]
    public class AdminRestrictionsController : QCUCRAM_AdminController
    {
        [HttpGet]
        [Route("")]
        [Route("index")]
        public ActionResult Index()
        {
            return RedirectToAction("Restrictions", "Admin");
        }
    }
}
