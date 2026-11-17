using System;
using System.Web.Mvc;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Security
{
    /// <summary>
    /// The MVC equivalent of Guades_QCUCram_AdminPage (RequiredRole = "Admin").
    ///
    /// NOTE: this is ASP.NET MVC 5, so the interface method is
    /// OnAuthorization(AuthorizationContext). AuthorizationFilterContext belongs to
    /// ASP.NET Core and does not exist in System.Web.Mvc.
    ///
    /// Runs as an IAuthorizationFilter, so it fires before any action filter. Always pair it
    /// with [QCUCRAM_RequireLogin] so the "account deactivated" check still happens for admins:
    ///
    ///     [QCUCRAM_RequireAdmin]
    ///     [QCUCRAM_RequireLogin]
    ///     public class AdminUsersController : QCUCRAM_AdminController
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class QCUCRAM_RequireAdminAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext == null || filterContext.HttpContext == null) { return; }

            if (!QCUCRAM_Session.IsAuthenticated)
            {
                filterContext.Result = QCUCRAM_RequireLoginAttribute.RedirectToLogin(filterContext.HttpContext);
                return;
            }

            if (!QCUCRAM_Session.IsAdmin)
            {
                // Same destination the Web Forms app used: back to the login page, which
                // explains that the account type is wrong (?msg=role).
                filterContext.Result = new RedirectResult(
                    QCUCRAM_Constants.Paths.Login + "?" + QCUCRAM_Constants.QueryKeys.Message
                    + "=" + QCUCRAM_Constants.Messages.Role);
            }
        }
    }
}
