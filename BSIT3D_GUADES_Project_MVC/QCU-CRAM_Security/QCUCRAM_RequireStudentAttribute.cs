using System;
using System.Web.Mvc;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Security
{
    /// <summary>
    /// The MVC equivalent of Guades_QCUCram_StudentPage (RequiredRole = "Student").
    ///
    /// NOTE: this is ASP.NET MVC 5, so the interface method is
    /// OnAuthorization(AuthorizationContext). AuthorizationFilterContext belongs to
    /// ASP.NET Core and does not exist in System.Web.Mvc.
    ///
    ///     [QCUCRAM_RequireStudent]
    ///     [QCUCRAM_RequireLogin]
    ///     public class StudentDashboardController : QCUCRAM_StudentController
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class QCUCRAM_RequireStudentAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext == null || filterContext.HttpContext == null) { return; }

            if (!QCUCRAM_Session.IsAuthenticated)
            {
                filterContext.Result = QCUCRAM_RequireLoginAttribute.RedirectToLogin(filterContext.HttpContext);
                return;
            }

            if (!QCUCRAM_Session.IsStudent)
            {
                filterContext.Result = new RedirectResult(
                    QCUCRAM_Constants.Paths.Login + "?" + QCUCRAM_Constants.QueryKeys.Message
                    + "=" + QCUCRAM_Constants.Messages.Role);
            }
        }
    }
}
