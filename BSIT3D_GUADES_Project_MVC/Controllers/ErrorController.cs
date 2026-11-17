using System;
using System.Configuration;
using System.Web.Mvc;
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
    /// </summary>
    public class ErrorController : Controller
    {
        public ActionResult Index()
        {
            Response.StatusCode = 500;
            Response.TrySkipIisCustomErrors = true;

            return View("~/Views/Shared/QCU-CRAM_Error.cshtml", Build(500));
        }

        public ActionResult NotFound()
        {
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;

            return View("~/Views/Shared/QCU-CRAM_Error.cshtml", Build(404));
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

            if (status == 404)
            {
                vm.Title = "Page not found";
                vm.Message = "That page does not exist, or it has moved. Check the address and try again.";
            }
            else
            {
                vm.Title = "Something went wrong";
                vm.Message = "An unexpected error stopped that page from loading. Please try again, "
                           + "and quote the reference below if you contact the library.";
            }

            return vm;
        }
    }
}
