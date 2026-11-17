using System;
using System.Web.Mvc;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Security
{
    /// <summary>
    /// The MVC equivalent of the "guest page" behaviour in Guades_QCUCram_Login.aspx.cs:
    ///
    ///   * a signed-in user who opens /login or /register with no ?msg= is sent to the
    ///     dashboard for their role (the original redirected to Default.aspx, which then
    ///     chose the dashboard);
    ///   * a signed-in user who arrives WITH a ?msg= stays on the page so the message can be
    ///     read (that is how ?msg=role explains a wrong-account-type redirect);
    ///   * an admin who opens /login is sent to the admin dashboard, never the student one.
    /// </summary>
    public abstract class QCUCRAM_GuestController : QCUCRAM_BaseController
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

            if (!QCUCRAM_Session.IsAuthenticated) { return; }

            // A guest-only controller may still host one signed-in-only action -
            // AccountController.ChangePassword is the one that does. When the action
            // carries [QCUCRAM_RequireLogin] the guest bounce must not run, otherwise
            // a signed-in user could never open the change-password form.
            bool needsLogin = filterContext.ActionDescriptor
                .GetCustomAttributes(typeof(QCUCRAM_RequireLoginAttribute), true).Length > 0;
            if (needsLogin) { return; }

            string code = filterContext.HttpContext.Request.QueryString[QCUCRAM_Constants.QueryKeys.Message];
            if (!string.IsNullOrWhiteSpace(code)) { return; }

            filterContext.Result = new RedirectResult(QCUCRAM_Session.HomeFor(QCUCRAM_Session.Role));
        }
    }
}
