using System;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Security
{
    /// <summary>
    /// Every magic string the security layer needs, in one place.
    ///
    /// These are the same strings the Web Forms application used:
    ///   Session["Role"] / Session["UserID"] / Session["UserName"]   (Guades_QCUCram_SecurePage)
    ///   ?msg=login|role|registered|loggedout|pwchanged              (Login.aspx.cs)
    /// If a value has to change it changes here and nowhere else.
    /// </summary>
    public static class QCUCRAM_Constants
    {
        /// <summary>The two account types. A Student row covers student / faculty / visitor requesters.</summary>
        public static class Roles
        {
            public const string Student = "Student";
            public const string Admin = "Admin";
        }

        /// <summary>Session keys - identical to Guades_QCUCram_SecurePage.SessionRole/SessionUserId/SessionUserName.</summary>
        public static class SessionKeys
        {
            public const string Role = "Role";
            public const string UserId = "UserID";
            public const string UserName = "UserName";

            /// <summary>When the account was last re-checked against the database (ticks, as a string).</summary>
            public const string VerifiedAt = "QCUCRAM_VerifiedAt";
        }

        /// <summary>Query-string keys.</summary>
        public static class QueryKeys
        {
            public const string Message = "msg";
            public const string ReturnUrl = "ReturnUrl";
        }

        /// <summary>The ?msg= vocabulary of the original Login page.</summary>
        public static class Messages
        {
            public const string Login = "login";
            public const string Role = "role";
            public const string Registered = "registered";
            public const string LoggedOut = "loggedout";
            public const string PasswordChanged = "pwchanged";
            public const string Deactivated = "deactivated";
        }

        /// <summary>TempData slots used by the Flash helpers on the base controllers.</summary>
        public static class TempDataKeys
        {
            public const string Notice = "QCUCRAM_Notice";
            public const string Success = "QCUCRAM_Success";
            public const string Error = "QCUCRAM_Error";
        }

        /// <summary>Virtual paths, so no controller hard-codes a URL.</summary>
        public static class Paths
        {
            public const string Login = "~/login";
            public const string Logout = "~/logout";
            public const string Register = "~/register";
            public const string Error = "~/error";
            public const string Home = "~/";
            public const string StudentHome = "~/student";
            public const string AdminHome = "~/admin";
        }

        /// <summary>Matches &lt;sessionState timeout="30" /&gt; in Web.config.</summary>
        public const int SessionTimeoutMinutes = 30;

        /// <summary>
        /// How often the "is this account still valid?" check hits the database for one session.
        /// The original checked only at login (session based); a 60-second window keeps that
        /// behaviour cheap while still catching a deactivated library assistant quickly.
        /// </summary>
        public const int VerifyIntervalSeconds = 60;
    }
}
