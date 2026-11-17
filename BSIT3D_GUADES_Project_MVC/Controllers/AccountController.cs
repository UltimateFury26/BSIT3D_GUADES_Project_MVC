using System;
using System.Diagnostics;
using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.Controllers
{
    /// <summary>
    /// Phase 04 - the MVC replacement for Guades_QCUCram_Login.aspx,
    /// Guades_QCUCram_Logout.aspx and Guades_QCUCram_Register.aspx.
    ///
    /// The sign-in path is deliberately identical to the Web Forms one:
    ///   * the role radio button decides which repository is asked;
    ///   * a student number is tried in three shapes (as typed, dashed, undashed);
    ///   * a wrong ID and a wrong password produce the same message;
    ///   * an unknown ID still spends the time of a real PBKDF2 check;
    ///   * an expired restriction is lifted on the way in;
    ///   * Session.Clear() runs first so a student can switch to admin in the same browser.
    /// </summary>
    // QCUCRAM_NoticeFilter is registered globally in App_Start/FilterConfig.cs, so the
    // ?msg= text is already on ViewBag before any action here runs.
    public class AccountController : QCUCRAM_GuestController
    {
        private const string InvalidCredentials = "Invalid credentials.";
        private const string SignInFailed = "Sorry, we could not sign you in right now. Please try again.";

        // ------------------------------------------------------------------
        //  GET /login
        // ------------------------------------------------------------------
        [HttpGet]
        public ActionResult Login(string msg, string ReturnUrl)
        {
            // The base GuestController has already bounced a signed-in user to their
            // dashboard unless ?msg= was supplied - that is the Web Forms rule too.
            QCUCRAM_LoginViewModel model = new QCUCRAM_LoginViewModel
            {
                ReturnUrl = QCUCRAM_BaseController.SafeReturnUrl(ReturnUrl, null)
            };

            // A role hint survives a round trip so the radio button stays on Admin.
            string role = Request.QueryString["role"];
            if (role == QCUCRAM_Constants.Roles.Admin) { model.Role = QCUCRAM_Constants.Roles.Admin; }

            return View(model);
        }

        // ------------------------------------------------------------------
        //  POST /login
        // ------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(QCUCRAM_LoginViewModel model)
        {
            if (model == null)
            {
                model = new QCUCRAM_LoginViewModel();
            }

            string id = (model.Identifier ?? string.Empty).Trim();
            string password = model.Password ?? string.Empty;

            // The radio button is a required field, but a hand-rolled POST can omit it.
            if (string.IsNullOrEmpty(model.Role)) { model.Role = QCUCRAM_Constants.Roles.Student; }

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError(string.Empty, InvalidCredentials);
                return View(model);
            }

            QCUCRAM_LoginResult result;

            if (model.Role == QCUCRAM_Constants.Roles.Admin)
            {
                result = TryAdminLogin(id, password);
            }
            else
            {
                result = TryStudentLogin(id, password);
            }

            // ResultCode 2 = the database itself failed. That is not a credential problem,
            // so it must not be reported as one.
            if (result.ResultCode == 2)
            {
                ModelState.AddModelError(string.Empty, SignInFailed);
                return View(model);
            }

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, InvalidCredentials);
                return View(model);
            }

            // Wipes any previous login, sets the 3 session keys, rebuilds the principal.
            QCUCRAM_Session.SignIn(HttpContext, result.Role, result.UserId.Value, result.FullName);

            // An explicit ReturnUrl wins, but only if it is a local URL.
            string target = QCUCRAM_BaseController.SafeReturnUrl(model.ReturnUrl, null);
            if (target != null) { return Redirect(target); }

            return Redirect(Url.Content(QCUCRAM_Session.HomeFor(result.Role)));
        }

        // ------------------------------------------------------------------
        //  GET /logout
        // ------------------------------------------------------------------
        [HttpGet]
        public ActionResult Logout()
        {
            QCUCRAM_Session.SignOut(HttpContext);

            // Url.Content turns "~/login" into a real path; Controller.Redirect does not
            // resolve the "~" token on its own.
            return Redirect(Url.Content(
                QCUCRAM_Constants.Paths.Login + "?" +
                QCUCRAM_Constants.QueryKeys.Message + "=" + QCUCRAM_Constants.Messages.LoggedOut));
        }

        // A stray POST to /logout must not 404 - treat it the same as GET.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout(QCUCRAM_LoginViewModel ignored)
        {
            return Logout();
        }

        // ------------------------------------------------------------------
        //  GET /register
        // ------------------------------------------------------------------
        [HttpGet]
        public ActionResult Register()
        {
            return View(new QCUCRAM_RegisterViewModel());
        }

        // ------------------------------------------------------------------
        //  POST /register
        // ------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(QCUCRAM_RegisterViewModel model)
        {
            if (model == null) { model = new QCUCRAM_RegisterViewModel(); }

            if (!ModelState.IsValid) { return View(model); }

            QCUCRAM_RegisterResult result;
            try
            {
                result = new QCUCRAM_StudentRepository().Register(
                    model.StudentNumber,
                    model.LastName,
                    model.FirstName,
                    model.MI,
                    model.Email,
                    model.Program,
                    model.RequesterType,
                    model.Password);
            }
            catch (Exception ex)
            {
                Trace.TraceError("QCU-CRAM register failed: " + ex);
                ModelState.AddModelError(string.Empty,
                    "Sorry, we could not create your account right now. Please try again.");
                return View(model);
            }

            switch (result.ResultCode)
            {
                case 0:
                    return Redirect(Url.Content(
                        QCUCRAM_Constants.Paths.Login + "?" +
                        QCUCRAM_Constants.QueryKeys.Message + "=" + QCUCRAM_Constants.Messages.Registered));

                case 1:
                    ModelState.AddModelError("StudentNumber", "That ID / student number is already registered.");
                    break;

                case 2:
                    ModelState.AddModelError("Email", "That e-mail address is already registered.");
                    break;

                default:
                    ModelState.AddModelError(string.Empty,
                        "Please check the form - some details were not accepted.");
                    break;
            }

            return View(model);
        }

        // ==================================================================
        //  Sign-in helpers
        // ==================================================================

        /// <summary>
        /// Tries the typed value, its normalised (dashed) form and its undashed form.
        /// A database failure stops the loop immediately so it is not retried three times.
        /// </summary>
        private static QCUCRAM_LoginResult TryStudentLogin(string typed, string password)
        {
            QCUCRAM_StudentRepository repo = new QCUCRAM_StudentRepository();
            QCUCRAM_LoginResult last = new QCUCRAM_LoginResult { ResultCode = 1 };

            foreach (string candidate in QCUCRAM_StudentRepository.StudentNumberCandidates(typed))
            {
                last = repo.Login(candidate, password);

                if (last.ResultCode == 0) { return last; }   // success
                if (last.ResultCode == 2) { return last; }   // database problem - stop trying
            }

            return last;
        }

        /// <summary>Admin sign-in. The repository already restricts to IsActive = 1.</summary>
        private static QCUCRAM_LoginResult TryAdminLogin(string username, string password)
        {
            try
            {
                return new QCUCRAM_AdminUserRepository().Login(username, password);
            }
            catch (Exception ex)
            {
                Trace.TraceError("QCU-CRAM admin login failed: " + ex);
                return new QCUCRAM_LoginResult { ResultCode = 2 };
            }
        }
    }
}
