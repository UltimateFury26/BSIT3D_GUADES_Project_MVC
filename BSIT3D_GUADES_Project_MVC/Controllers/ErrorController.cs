using System;
using System.Web.Mvc;

namespace BSIT3D_GUADES_Project_MVC.Controllers
{
    /// <summary>
    /// Phase 04 - the friendly error page.
    ///
    /// Web.config points customErrors at ~/error, and Global.asax.cs writes the real
    /// exception to App_Data/QCU-CRAM_error.log. The page therefore shows a reference
    /// number only - never a stack trace - which is the same posture the Web Forms
    /// application had.
    /// </summary>
    public class ErrorController : Controller
    {
        public ActionResult Index()
        {
            Response.StatusCode = 500;
            Response.TrySkipIisCustomErrors = true;

            ViewBag.QCUCRAM_ErrorReference = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
            return View();
        }

        public ActionResult NotFound()
        {
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            return View("Index");
        }
    }
}
