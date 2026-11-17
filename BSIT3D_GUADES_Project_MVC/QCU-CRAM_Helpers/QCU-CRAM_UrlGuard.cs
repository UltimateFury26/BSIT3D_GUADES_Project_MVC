using System;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers
{
    /// <summary>
    /// Keeps "returnUrl" honest. An open redirect is the classic MVC mistake:
    /// ?returnUrl=https://evil.example/ turns the login page into a phishing hop.
    /// </summary>
    public static class QCUCRAM_UrlGuard
    {
        /// <summary>
        /// True only for URLs that stay inside this application: "~/x", "/x", or a bare
        /// relative path. Protocol-relative ("//host") and absolute ("http://...") are rejected.
        /// </summary>
        public static bool IsLocalUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) { return false; }
            if (url.Length > 1 && url[0] == '~' && url[1] == '/') { return true; }
            if (url[0] != '/') { return false; }
            // "//host" and "/\host" are treated by browsers as protocol-relative.
            if (url.Length > 1 && (url[1] == '/' || url[1] == '\\')) { return false; }
            return true;
        }

        /// <summary>Returns the URL when it is local, otherwise the fallback.</summary>
        public static string SafeReturnUrl(string url, string fallback)
        {
            return IsLocalUrl(url) ? url : fallback;
        }

        /// <summary>
        /// Normalises "~/Area/Page" to "/Area/Page" so it can be handed to Redirect().
        /// </summary>
        public static string ToAppRelative(string url)
        {
            if (string.IsNullOrEmpty(url)) { return "/"; }
            if (url.StartsWith("~/", StringComparison.Ordinal))
            {
                return System.Web.VirtualPathUtility.ToAbsolute(url);
            }
            return url;
        }
    }
}
