using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;
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

        // Phase 07: same patterns as Guades_QCUCram_Register.aspx.cs.
        private static readonly Regex StudentNumberRegex = new Regex(@"^\d{2}-\d{4}$");
        private static readonly Regex OtherIdRegex = new Regex(@"^[A-Za-z0-9\-]{3,20}$");

        /// <summary>
        /// Phase 07: the Web Forms pages showed lblMessage as an upper-right toast
        /// (SecurePage.ShowMessage -> QueueToast). The toast partial reads these two fields.
        /// </summary>
        private void Notice(string text, bool isError)
        {
            ViewBag.QCUCRAM_NoticeText = text;
            ViewBag.QCUCRAM_NoticeKind = isError ? "error" : "success";
        }

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

            // The radio button is a required field, but a hand-rolled POST can omit it.
            if (string.IsNullOrEmpty(model.Role)) { model.Role = QCUCRAM_Constants.Roles.Student; }

            // Web Forms: Page.IsValid == false -> the validators' inline text, nothing else.
            // The Identifier message follows the role exactly like the original's script did.
            if (string.IsNullOrWhiteSpace(model.Identifier) || string.IsNullOrEmpty(model.Password))
            {
                ModelState.Clear();
                if (string.IsNullOrWhiteSpace(model.Identifier))
                {
                    ModelState.AddModelError("Identifier",
                        model.Role == QCUCRAM_Constants.Roles.Admin
                            ? "Username is required." : "Student number / ID is required.");
                }
                if (string.IsNullOrEmpty(model.Password))
                {
                    ModelState.AddModelError("Password", "Password is required.");
                }
                return View(model);
            }

            string id = model.Identifier.Trim();
            string password = model.Password;

            QCUCRAM_LoginResult result = (model.Role == QCUCRAM_Constants.Roles.Admin)
                ? TryAdminLogin(id, password)
                : TryStudentLogin(id, password);

            // ResultCode 2 = the database itself failed. That is not a credential problem.
            if (result.ResultCode == 2)
            {
                Notice(SignInFailed, true);
                return View(model);
            }

            if (!result.Success)
            {
                // Same message for wrong ID and wrong password (and an inactive assistant).
                Notice(InvalidCredentials, true);
                return View(model);
            }

            // Wipes any previous login, sets the 3 session keys, rebuilds the principal.
            QCUCRAM_Session.SignIn(HttpContext, result.Role, result.UserId.Value, result.FullName);

            // Phase 07: the original always went to the role's dashboard (it had no ReturnUrl).
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
        //  POST /register   (port of Guades_QCUCram_Register.aspx.cs btnRegister_Click)
        // ------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(QCUCRAM_RegisterViewModel model)
        {
            if (model == null) { model = new QCUCRAM_RegisterViewModel(); }

            // Web Forms: the page validators (required / email pattern / confirm compare) run first.
            if (!ModelState.IsValid) { return View(model); }

            string type = model.RequesterType;
            string number = (model.StudentNumber ?? string.Empty).Trim();
            string last = (model.LastName ?? string.Empty).Trim();
            string first = (model.FirstName ?? string.Empty).Trim();
            string mi = (model.MI ?? string.Empty).Trim().TrimEnd('.').ToUpperInvariant();
            if (mi.Length > 2) { mi = mi.Substring(0, 2); }
            string email = (model.Email ?? string.Empty).Trim();
            string program = model.Program;
            string password = model.Password ?? string.Empty;
            string confirm = model.ConfirmPassword ?? string.Empty;

            // A student typing 230001 means 23-0001
            if (type == "Student") { number = QCUCRAM_Programs.NormalizeStudentNumber(number); }

            // ---- server-side re-checks, same order and wording as the original ----
            List<string> errors = new List<string>();

            if (!QCUCRAM_Programs.IsValidRequesterType(type))
                errors.Add("Please select a valid requester type.");
            if (!QCUCRAM_Programs.IsValidProgram(program))
                errors.Add("Please select a valid program.");
            if (type == "Student")
            {
                if (!StudentNumberRegex.IsMatch(number))
                    errors.Add("Student number must look like 23-0001.");
            }
            else if (!OtherIdRegex.IsMatch(number))
            {
                errors.Add("ID number must be 3-20 letters, numbers, or dashes only.");
            }
            if (last.Length == 0 || first.Length == 0)
                errors.Add("Last name and first name are required.");
            if (email.Length == 0 || email.Length > 120)
                errors.Add("Please enter a valid email address.");
            string pwIssue = QCUCRAM_PasswordHash.GetStrengthMessage(password);
            if (pwIssue != null)
                errors.Add(pwIssue);
            if (password != confirm)
                errors.Add("Passwords do not match.");

            if (errors.Count > 0)
            {
                Notice(string.Join(" ", errors), true);
                return View(model);
            }

            try
            {
                QCUCRAM_StudentRepository repo = new QCUCRAM_StudentRepository();

                // ---- friendly duplicate checks: BOTH are reported together, like the original ----
                if (repo.GetByStudentNumber(number) != null)
                    errors.Add("That ID / Student Number is already registered. Try logging in instead.");
                if (repo.GetByEmail(email) != null)
                    errors.Add("That email address is already registered.");

                if (errors.Count > 0)
                {
                    Notice(string.Join(" ", errors), true);
                    return View(model);
                }

                QCUCRAM_RegisterResult result = repo.Register(number, last, first, mi, email,
                                                              program, type, password);

                if (result.ResultCode == 0)
                {
                    return Redirect(Url.Content(
                        QCUCRAM_Constants.Paths.Login + "?" +
                        QCUCRAM_Constants.QueryKeys.Message + "=" + QCUCRAM_Constants.Messages.Registered));
                }

                // 1 / 2 = unique index hit by two people registering at the same moment.
                if (result.ResultCode == 1 || result.ResultCode == 2)
                    Notice("That ID / Student Number or email is already registered.", true);
                else
                    Notice("Sorry, registration could not be completed. Please try again.", true);
            }
            catch (Exception ex)
            {
                Trace.TraceError("QCU-CRAM register failed: " + ex);
                Notice("Sorry, registration could not be completed. Please try again.", true);
            }

            return View(model);
        }

        // ------------------------------------------------------------------
        //  GET /account/changepassword
        // ------------------------------------------------------------------
        [HttpGet]
        [QCUCRAM_RequireLogin]
        public ActionResult ChangePassword()
        {
            return View(new QCUCRAM_ChangePasswordViewModel());
        }

        // ------------------------------------------------------------------
        //  POST /account/changepassword   (port of ChangePassword.aspx.cs btnChange_Click)
        //  Same order as the original: strength -> match -> current password -> must differ.
        //  Success stays on the page with a toast; the session is NOT ended.
        // ------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        [QCUCRAM_RequireLogin]
        public ActionResult ChangePassword(QCUCRAM_ChangePasswordViewModel model)
        {
            if (model == null) { model = new QCUCRAM_ChangePasswordViewModel(); }

            // Required fields and the "Passwords do not match." compare (inline, like the validators).
            if (!ModelState.IsValid) { return View(model); }

            string current = model.CurrentPassword ?? string.Empty;
            string newPassword = model.NewPassword ?? string.Empty;

            string pwIssue = QCUCRAM_PasswordHash.GetStrengthMessage(newPassword);
            if (pwIssue != null)
            {
                Notice(pwIssue, true);
                return View(model);
            }

            try
            {
                string stored;
                if (QCUCRAM_Session.IsAdmin)
                {
                    QCUCRAM_AdminUser admin = new QCUCRAM_AdminUserRepository().GetById(QCUCRAM_Session.UserId);
                    stored = admin == null ? null : admin.PasswordHash;
                }
                else
                {
                    QCUCRAM_Student student = new QCUCRAM_StudentRepository().GetById(QCUCRAM_Session.UserId);
                    stored = student == null ? null : student.PasswordHash;
                }

                if (stored == null || !QCUCRAM_PasswordHash.Verify(current, stored))
                {
                    Notice("Your current password is incorrect.", true);
                    return View(model);
                }

                if (current == newPassword)
                {
                    Notice("The new password must be different from the current one.", true);
                    return View(model);
                }

                int code = QCUCRAM_Session.IsAdmin
                    ? new QCUCRAM_AdminUserRepository().ChangePassword(QCUCRAM_Session.UserId, current, newPassword)
                    : new QCUCRAM_StudentRepository().ChangePassword(QCUCRAM_Session.UserId, current, newPassword);

                if (code != 0)
                {
                    Notice("Sorry, the password could not be changed. Please try again.", true);
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("QCU-CRAM change password failed: " + ex);
                Notice("Sorry, the password could not be changed. Please try again.", true);
                return View(model);
            }

            Notice("Your password has been changed.", false);
            return View(new QCUCRAM_ChangePasswordViewModel());
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
