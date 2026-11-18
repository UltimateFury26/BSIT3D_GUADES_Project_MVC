using System;
using System.Configuration;
using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Filters;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.Controllers
{
    /// <summary>
    /// QCU-CRAM  |  Controllers/ErrorController.cs
    ///
    /// The friendly error page. Web.config points customErrors at ~/error, and
    /// QCUCRAMHandleErrorAttribute / Application_Error log the real exception.
    /// The page shows a reference code only - never a stack trace - which is the
    /// same posture the Web Forms application had.
    ///
    /// Phase 32: the wording and the status line now come from one place
    /// (QCUCRAMHandleErrorAttribute.TitleFor / MessageFor / StatusDescriptionFor), so a
    /// page reached through customErrors and a page rendered by the exception filter
    /// say exactly the same thing. Forbidden was added because a role mistake is not a
    /// server fault: 403 keeps the browser history, the monitoring and the log honest.
    /// </summary>
    public class ErrorController : Controller
    {
        public ActionResult Index()
        {
            return Render(500);
        }

        public ActionResult NotFound()
        {
            return Render(404);
        }

        /// <summary>
        /// GET /error/forbidden - the page for a signed-in user who is not allowed to
        /// open the address they asked for. Reachable from Web.config's
        /// &lt;error statusCode="403"&gt; entry and from any RedirectToAction.
        /// </summary>
        public ActionResult Forbidden()
        {
            return Render(403);
        }

        /// <summary>
        /// GET /error/unauthorized - the session ended mid-request. The login page is
        /// still the usual destination; this exists so an explicit 401 has somewhere to
        /// land instead of being reported as a 500.
        /// </summary>
        public ActionResult Unauthorized()
        {
            return Render(401);
        }

        private ActionResult Render(int status)
        {
            Response.StatusCode = status;
            Response.StatusDescription = QCUCRAMHandleErrorAttribute.StatusDescriptionFor(status);
            Response.TrySkipIisCustomErrors = true;

            return View("~/Views/Shared/QCU-CRAM_Error.cshtml", Build(status));
        }

        private static QCUCRAMErrorViewModel Build(int status)
        {
            bool showDetail = string.Equals(
                ConfigurationManager.AppSettings["QCU-CRAM_ShowErrorDetail"], "true",
                StringComparison.OrdinalIgnoreCase);

            QCUCRAMErrorViewModel vm = new QCUCRAMErrorViewModel
            {
                StatusCode = status,
                Reference = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant(),
                ShowDetail = showDetail
            };

            vm.Title = QCUCRAMHandleErrorAttribute.TitleFor(status);
            vm.Message = QCUCRAMHandleErrorAttribute.MessageFor(status);

            return vm;
        }
    }
}
