using System;
using System.Configuration;
using System.Web;
using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Filters
{
    /// <summary>
    /// QCU-CRAM  |  QCU-CRAM_Filters/QCU-CRAM_HandleErrorAttribute.cs
    ///
    /// Replaces the stock HandleErrorAttribute. Differences that matter:
    ///   * The exception is logged FIRST, so even a failure to render the error page
    ///     leaves a record behind.
    ///   * AJAX requests get a small JSON body instead of an HTML page.
    ///   * The status code is set explicitly (404 stays 404, everything else 500).
    ///   * The user sees a short reference code that matches the log entry.
    /// </summary>
    public sealed class QCUCRAMHandleErrorAttribute : FilterAttribute, IExceptionFilter
    {
        public void OnException(ExceptionContext filterContext)
        {
            if (filterContext == null || filterContext.Exception == null) { return; }
            if (filterContext.ExceptionHandled) { return; }

            Exception ex = filterContext.Exception;
            HttpContextBase http = filterContext.HttpContext;

            int status = 500;
            HttpException httpEx = ex as HttpException;
            if (httpEx != null && httpEx.GetHttpCode() == 404) { status = 404; }

            string area = DescribeArea(filterContext);
            string reference = QCUCRAMErrorLog.Write(ex, area, null);

            filterContext.ExceptionHandled = true;
            filterContext.HttpContext.Response.Clear();
            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
            filterContext.HttpContext.Response.StatusCode = status;
            filterContext.HttpContext.Response.StatusDescription = status == 404 ? "Not Found" : "Internal Server Error";

            bool showDetail = string.Equals(
                ConfigurationManager.AppSettings["QCU-CRAM_ShowErrorDetail"], "true",
                StringComparison.OrdinalIgnoreCase);

            // AJAX / fetch: a tiny JSON envelope the client script can read.
            if (IsAjax(http.Request))
            {
                filterContext.Result = new JsonResult
                {
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                    Data = new
                    {
                        ok = false,
                        status = status,
                        message = "Something went wrong. Reference " + reference + ".",
                        reference = reference
                    }
                };
                return;
            }

            QCUCRAMErrorViewModel vm = new QCUCRAMErrorViewModel
            {
                StatusCode = status,
                Reference = reference,
                ShowDetail = showDetail,
                Detail = showDetail ? ex.ToString() : null,
                ReturnUrl = http.Request != null && http.Request.Url != null
                    ? http.Request.Url.PathAndQuery
                    : null
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

            // Copy the controller's ViewData so the shared layout still sees
            // ViewBag.Title / the notice keys, then put our model on top.
            ViewDataDictionary viewData = new ViewDataDictionary(filterContext.Controller.ViewData);
            viewData.Model = vm;

            filterContext.Result = new ViewResult
            {
                ViewName = "~/Views/Shared/QCU-CRAM_Error.cshtml",
                ViewData = viewData,
                TempData = filterContext.Controller.TempData
            };
        }

        private static bool IsAjax(HttpRequestBase request)
        {
            if (request == null) { return false; }
            if (string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            string accept = request.Headers["Accept"];
            return !string.IsNullOrEmpty(accept)
                && accept.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string DescribeArea(ExceptionContext filterContext)
        {
            try
            {
                string controller = Convert.ToString(filterContext.RouteData.Values["controller"]);
                string action = Convert.ToString(filterContext.RouteData.Values["action"]);
                return controller + "/" + action;
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
