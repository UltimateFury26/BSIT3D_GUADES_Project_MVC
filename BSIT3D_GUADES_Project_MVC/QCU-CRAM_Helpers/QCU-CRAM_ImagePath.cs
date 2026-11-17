using System;
using System.IO;
using System.Web;
using System.Web.Hosting;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers
{
    /// <summary>
    /// Turns the paths stored in the database ("~/QCU-CRAM_Media/rooms/3.jpg") into
    /// something the view can render and the file system can check.
    ///
    /// MVC has no Server.MapPath, so this is the one place that knows the difference
    /// between a virtual path and a physical one.
    /// </summary>
    public static class QCUCRAM_ImagePath
    {
        public const string MediaFolder = "~/QCU-CRAM_Media";
        public const string UploadFolder = "~/QCU-CRAM_Uploads";

        /// <summary>"~/x/y.jpg" or "x/y.jpg" becomes "/x/y.jpg". Null / blank stays null.</summary>
        public static string ToAbsolute(string virtualPath)
        {
            if (string.IsNullOrWhiteSpace(virtualPath)) { return null; }

            string v = virtualPath.Trim().Replace('\\', '/');
            if (v.StartsWith("~/", StringComparison.Ordinal))
            {
                return VirtualPathUtility.ToAbsolute(v);
            }
            if (v.StartsWith("/", StringComparison.Ordinal))
            {
                return v;
            }
            return VirtualPathUtility.ToAbsolute("~/" + v);
        }

        /// <summary>The full disk path, or null when the path is blank.</summary>
        public static string ToPhysical(string virtualPath)
        {
            string absolute = ToAbsolute(virtualPath);
            return absolute == null ? null : HostingEnvironment.MapPath(absolute);
        }

        /// <summary>True when the file is actually on disk (so a view can fall back to a placeholder).</summary>
        public static bool Exists(string virtualPath)
        {
            string physical = ToPhysical(virtualPath);
            return physical != null && File.Exists(physical);
        }

        /// <summary>
        /// Absolute URL for a stored image, or the fallback when the file is missing.
        /// Use this in views so a deleted upload never renders a broken image icon.
        /// </summary>
        public static string Resolve(string virtualPath, string fallback = null)
        {
            if (Exists(virtualPath)) { return ToAbsolute(virtualPath); }
            return fallback == null ? null : ToAbsolute(fallback);
        }

        /// <summary>
        /// A safe, unique file name for an upload: keeps the extension, throws away everything
        /// the browser sent (which may contain "../" or a full path).
        /// </summary>
        public static string SafeFileName(string originalName)
        {
            string name = Path.GetFileName((originalName ?? "").Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(name)) { name = "upload"; }

            string ext = Path.GetExtension(name);
            if (ext == null) { ext = ""; }
            ext = ext.ToLowerInvariant();

            string allowed = ".jpg .jpeg .png .gif .webp .pdf";
            if ((" " + allowed + " ").IndexOf(" " + ext + " ", StringComparison.Ordinal) < 0)
            {
                ext = ".bin";
            }

            return Guid.NewGuid().ToString("N") + ext;
        }
    }
}
