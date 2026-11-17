using System.Web.Mvc;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Security
{
    /// <summary>
    /// Turns ?msg=... and the TempData flash slots into two view fields, so no view ever has
    /// to look at a query string:
    ///
    ///     ViewBag.QCUCRAM_NoticeText   the sentence to show (null when there is nothing)
    ///     ViewBag.QCUCRAM_NoticeKind   "success" or "error"
    ///
    /// Registered globally in App_Start/FilterConfig.cs.
    /// </summary>
    public class QCUCRAM_NoticeFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null) { return; }

            Controller controller = filterContext.Controller as Controller;
            if (controller == null) { return; }

            // 1. ?msg=code from a redirect
            string code = filterContext.HttpContext.Request.QueryString[QCUCRAM_Constants.QueryKeys.Message];
            string text = QCUCRAM_Notices.TextFor(code);

            if (text != null)
            {
                controller.ViewBag.QCUCRAM_NoticeText = text;
                controller.ViewBag.QCUCRAM_NoticeKind = QCUCRAM_Notices.KindFor(code);
            }

            // 2. a flash message set by the previous action wins over the query string
            object success = controller.TempData[QCUCRAM_Constants.TempDataKeys.Success];
            object error = controller.TempData[QCUCRAM_Constants.TempDataKeys.Error];

            if (success != null)
            {
                controller.ViewBag.QCUCRAM_NoticeText = success.ToString();
                controller.ViewBag.QCUCRAM_NoticeKind = "success";
            }
            else if (error != null)
            {
                controller.ViewBag.QCUCRAM_NoticeText = error.ToString();
                controller.ViewBag.QCUCRAM_NoticeKind = "error";
            }
        }
    }
}
