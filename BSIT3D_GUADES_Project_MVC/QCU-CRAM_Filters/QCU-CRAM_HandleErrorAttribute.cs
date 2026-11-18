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
            if (httpEx != null)
            {
                // Phase 32: keep the code the exception already carries.
                //
                // This used to downgrade everything except 404 to 500. An HttpException
                // thrown with 403 (an access check inside an action, or a request the
                // server refused) then reached the browser as "Internal Server Error",
                // which is both wrong for the user and wrong for any monitoring that
                // watches the status code. 401/403/404 are preserved; anything else
                // still becomes 500 because an unhandled 5xx has no better answer.
                int code = httpEx.GetHttpCode();
                if (code == 401 || code == 403 || code == 404) { status = code; }
            }

            string area = DescribeArea(filterContext);
            string reference = QCUCRAMErrorLog.Write(ex, area, null);

            filterContext.ExceptionHandled = true;
            filterContext.HttpContext.Response.Clear();
            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
            filterContext.HttpContext.Response.StatusCode = status;
            filterContext.HttpContext.Response.StatusDescription = StatusDescriptionFor(status);

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
                        message = MessageFor(status) + " Reference " + reference + ".",
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

            vm.Title = TitleFor(status);
            vm.Message = MessageFor(status);

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

        // Phase 32: the wording for each status lives in exactly one place so the
        // filter and ErrorController cannot drift apart.
        internal static string StatusDescriptionFor(int status)
        {
            switch (status)
            {
                case 401: return "Unauthorized";
                case 403: return "Forbidden";
                case 404: return "Not Found";
                default: return "Internal Server Error";
            }
        }

        internal static string TitleFor(int status)
        {
            switch (status)
            {
                case 401: return "Please sign in";
                case 403: return "Access denied";
                case 404: return "Page not found";
                default: return "Something went wrong";
            }
        }

        internal static string MessageFor(int status)
        {
            switch (status)
            {
                case 401:
                    return "Your session has ended. Please log in again to continue.";
                case 403:
                    return "You do not have access to that page. Log in with the correct account type.";
                case 404:
                    return "That page does not exist, or it has moved. Check the address and try again.";
                default:
                    return "An unexpected error stopped that page from loading. Please try again, "
                         + "and quote the reference below if you contact the library.";
            }
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
