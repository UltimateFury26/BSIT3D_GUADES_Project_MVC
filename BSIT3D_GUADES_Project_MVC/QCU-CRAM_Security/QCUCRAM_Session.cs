using System;
using System.Security.Principal;
using System.Web;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Security
{
    /// <summary>
    /// The MVC replacement for Guades_QCUCram_SecurePage's session block.
    ///
    /// QCU-CRAM authenticates with Session state (three keys), not with a Forms
    /// Authentication ticket, so this class is the one and only place that reads or
    /// writes those keys. Nothing else in the application should touch Session["Role"].
    /// </summary>
    public static class QCUCRAM_Session
    {
        // ------------------------------------------------------------------
        //  Sign in / sign out
        // ------------------------------------------------------------------

        /// <summary>
        /// Wipes any previous login (a student switching to an admin account, or the
        /// reverse) and writes the three session keys, exactly like
        /// Guades_QCUCram_Login.StartSession.
        /// </summary>
        public static void SignIn(HttpContextBase http, string role, int userId, string displayName)
        {
            if (http == null || http.Session == null) { throw new InvalidOperationException("Session state is not available."); }
            if (string.IsNullOrWhiteSpace(role)) { throw new ArgumentException("A role is required.", "role"); }
            if (userId <= 0) { throw new ArgumentException("A user id is required.", "userId"); }

            http.Session.Clear();
            http.Session[QCUCRAM_Constants.SessionKeys.Role] = role;
            http.Session[QCUCRAM_Constants.SessionKeys.UserId] = userId;
            http.Session[QCUCRAM_Constants.SessionKeys.UserName] = displayName ?? string.Empty;
            http.Session[QCUCRAM_Constants.SessionKeys.VerifiedAt] = DateTime.UtcNow.Ticks.ToString();

            // Make the principal correct for the rest of THIS request too.
            ApplyPrincipal(http, role, displayName);
        }

        /// <summary>
        /// Clears the session, abandons it and expires the session cookie so the old
        /// session id can never be replayed (Guades_QCUCram_Logout.aspx.cs).
        /// </summary>
        public static void SignOut(HttpContextBase http)
        {
            if (http == null) { return; }

            try
            {
                if (http.Session != null)
                {
                    http.Session.Clear();
                    http.Session.Abandon();
                }
            }
            catch (Exception)
            {
                // Abandon after the response has started throws; the Clear above already
                // removed the identity, so there is nothing left to protect.
            }

            try
            {
                HttpCookie dead = new HttpCookie("ASP.NET_SessionId", string.Empty);
                dead.Expires = DateTime.Now.AddYears(-1);
                dead.Path = "/";
                http.Response.Cookies.Add(dead);
            }
            catch (Exception)
            {
                // A cookie that cannot be written must never break a sign-out.
            }

            http.User = new GenericPrincipal(new GenericIdentity(string.Empty), new string[0]);
        }

        /// <summary>Rebuilds HttpContext.User from the session so User.IsInRole(...) works.</summary>
        public static void ApplyPrincipal(HttpContextBase http, string role, string displayName)
        {
            if (http == null) { return; }

            string name = string.IsNullOrWhiteSpace(displayName) ? role : displayName;
            string[] roles = string.IsNullOrWhiteSpace(role) ? new string[0] : new[] { role };
            http.User = new GenericPrincipal(new GenericIdentity(name), roles);
        }

        // ------------------------------------------------------------------
        //  Current user (the CurrentUser property of SecurePage)
        // ------------------------------------------------------------------

        private static HttpContextBase Current
        {
            get { return HttpContext.Current == null ? null : new HttpContextWrapper(HttpContext.Current); }
        }

        public static string Role
        {
            get
            {
                HttpContextBase http = Current;
                if (http == null || http.Session == null) { return null; }
                return http.Session[QCUCRAM_Constants.SessionKeys.Role] as string;
            }
        }

        public static int UserId
        {
            get
            {
                HttpContextBase http = Current;
                if (http == null || http.Session == null) { return 0; }

                object value = http.Session[QCUCRAM_Constants.SessionKeys.UserId];
                return (value is int) ? (int)value : 0;
            }
        }

        public static string UserName
        {
            get
            {
                HttpContextBase http = Current;
                if (http == null || http.Session == null) { return string.Empty; }
                return http.Session[QCUCRAM_Constants.SessionKeys.UserName] as string ?? string.Empty;
            }
        }

        /// <summary>
        /// Rewrites the cached display name after a self-edit (Phase 08, Admin Accounts)
        /// so the header shows the new name on the very next request.
        /// </summary>
        public static void RefreshName(string displayName)
        {
            HttpContextBase http = Current;
            if (http == null || http.Session == null) { return; }
            http.Session[QCUCRAM_Constants.SessionKeys.UserName] = displayName ?? string.Empty;
        }

        /// <summary>True only when a role AND a positive id are present - the original's test.</summary>
        public static bool IsAuthenticated
        {
            get { return !string.IsNullOrEmpty(Role) && UserId > 0; }
        }

        public static bool IsAdmin
        {
            get { return string.Equals(Role, QCUCRAM_Constants.Roles.Admin, StringComparison.OrdinalIgnoreCase); }
        }

        public static bool IsStudent
        {
            get { return string.Equals(Role, QCUCRAM_Constants.Roles.Student, StringComparison.OrdinalIgnoreCase); }
        }

        // ------------------------------------------------------------------
        //  Re-verification bookkeeping
        // ------------------------------------------------------------------

        /// <summary>True when the account has not been re-checked within the interval.</summary>
        public static bool NeedsVerification(HttpContextBase http, int intervalSeconds)
        {
            if (http == null || http.Session == null) { return true; }

            object raw = http.Session[QCUCRAM_Constants.SessionKeys.VerifiedAt];
            long ticks;
            if (raw == null || !long.TryParse(raw.ToString(), out ticks) || ticks <= 0) { return true; }

            return (DateTime.UtcNow - new DateTime(ticks)).TotalSeconds >= intervalSeconds;
        }

        /// <summary>Stamps "just verified" so the next requests skip the database check.</summary>
        public static void MarkVerified(HttpContextBase http)
        {
            if (http == null || http.Session == null) { return; }
            http.Session[QCUCRAM_Constants.SessionKeys.VerifiedAt] = DateTime.UtcNow.Ticks.ToString();
        }

        /// <summary>The dashboard a role belongs on.</summary>
        public static string HomeFor(string role)
        {
            return string.Equals(role, QCUCRAM_Constants.Roles.Admin, StringComparison.OrdinalIgnoreCase)
                ? QCUCRAM_Constants.Paths.AdminHome
                : QCUCRAM_Constants.Paths.StudentHome;
        }
    }
}
