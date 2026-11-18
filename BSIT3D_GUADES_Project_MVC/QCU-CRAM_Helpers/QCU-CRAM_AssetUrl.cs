using System;
using System.IO;
using System.Web;
using System.Web.Hosting;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers
{
    /// <summary>
    /// Port of Guades_QCUCram_Site.Master.cs -> AssetUrl(string virtualPath).
    ///
    /// The Web Forms master never linked a stylesheet or script bare. It appended
    /// the file's last-write time:
    ///
    ///     &lt;link href="&lt;%= AssetUrl("~/Content/Site.css") %&gt;" rel="stylesheet" /&gt;
    ///     --&gt; /Content/Site.css?v=20251117153107
    ///
    /// That stamp is what makes an edited stylesheet show up on the next refresh.
    /// Without it the browser happily keeps the copy it already has, so a page can
    /// look "not fixed" even though the file on disk is correct.
    ///
    /// This matters most in Debug. With compilation debug="true" the bundler runs
    /// with BundleTable.EnableOptimizations = false, and in that mode
    /// Styles.Render emits one plain &lt;link&gt; per file with no version query
    /// string at all. The layout's script tags are plain &lt;script src&gt; tags, so
    /// they never had one either. Release is fine (the bundle URL carries a content
    /// hash), but the version you actually develop against is Debug - hence this
    /// helper, used for the debug-mode links.
    ///
    /// Any failure falls back to the plain URL: a cache stamp must never break a page.
    /// </summary>
    public static class QCUCRAM_AssetUrl
    {
        /// <summary>
        /// "/Content/QCU-CRAM_Site.css?v=20251117153107" for an existing file,
        /// otherwise just the resolved path.
        /// </summary>
        public static string For(string virtualPath)
        {
            if (string.IsNullOrWhiteSpace(virtualPath)) { return virtualPath; }

            string url;
            try
            {
                url = VirtualPathUtility.ToAbsolute(virtualPath);
            }
            catch (Exception)
            {
                return virtualPath;
            }

            try
            {
                string file = HostingEnvironment.MapPath(virtualPath);
                if (!string.IsNullOrEmpty(file) && File.Exists(file))
                {
                    return url + "?v=" + File.GetLastWriteTimeUtc(file).ToString("yyyyMMddHHmmss");
                }
            }
            catch (Exception)
            {
                // never break a page because of a cache stamp
            }

            return url;
        }

        /// <summary>
        /// Same as <see cref="For(string)"/> but for a path built at runtime
        /// (for example "~/Scripts/jquery-3.7.0" + ".min" + ".js").
        /// </summary>
        public static string For(string virtualPath, string suffix)
        {
            return For(virtualPath + (suffix ?? string.Empty));
        }
    }
}
