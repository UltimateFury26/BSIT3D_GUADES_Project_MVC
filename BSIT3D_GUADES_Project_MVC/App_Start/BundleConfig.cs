using System.Web;
using System.Web.Optimization;

namespace BSIT3D_GUADES_Project_MVC
{
    /// <summary>
    /// Phase 05 - bundles for the Razor layout.
    ///
    /// Replaces the Web Forms ~/Content/css and ~/Scripts/*.js references in
    /// Guades_QCUCram_Site.Master. The layout renders exactly two things:
    ///
    ///     @Styles.Render("~/Content/QCU-CRAM_css")     in &lt;head&gt;
    ///     @Scripts.Render("~/bundles/QCU-CRAM_js")     before &lt;/body&gt;
    ///
    /// Do not add the .min.js files explicitly - the bundler minifies in
    /// Release, and including both copies duplicates every definition.
    /// </summary>
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            // The default ignore list is left alone. Clearing it (bundles.IgnoreList.Clear())
            // can make Scripts.Render throw a NullReferenceException on some setups, and every
            // file below is listed explicitly, so nothing needed is ever ignored.

            // ------------------------------------------------------------------ CSS
            bundles.Add(new StyleBundle("~/Content/QCU-CRAM_css")
                .Include("~/Content/bootstrap.css")
                .Include("~/Content/QCU-CRAM_Site.css"));

            // ------------------------------------------------------------------- JS
            bundles.Add(new ScriptBundle("~/bundles/QCU-CRAM_jquery")
                .Include("~/Scripts/jquery-3.7.0.js"));

            bundles.Add(new ScriptBundle("~/bundles/QCU-CRAM_bootstrap")
                .Include("~/Scripts/bootstrap.bundle.js"));

            bundles.Add(new ScriptBundle("~/bundles/QCU-CRAM_validate")
                .Include("~/Scripts/jquery.validate.js")
                .Include("~/Scripts/jquery.validate.unobtrusive.js"));

            bundles.Add(new ScriptBundle("~/bundles/QCU-CRAM_reserve")
                .Include("~/Scripts/QCU-CRAM_reserve.js"));

            // Phase 05: Include() takes FILE paths. The old version listed the three
            // bundle paths above, which are not files, so jQuery, Bootstrap and
            // jquery.validate were silently left out of the layout bundle (no navbar
            // collapse/dropdowns, no qcuConfirm modal, no client validation).
            // Order matters: jQuery, Bootstrap, validate, then the site scripts.
            bundles.Add(new ScriptBundle("~/bundles/QCU-CRAM_js")
                .Include("~/Scripts/jquery-3.7.0.js")
                .Include("~/Scripts/bootstrap.bundle.js")
                .Include("~/Scripts/jquery.validate.js")
                .Include("~/Scripts/jquery.validate.unobtrusive.js")
                .Include("~/Scripts/QCU-CRAM_site.js")
                .Include("~/Scripts/QCU-CRAM_ui.js"));

#if DEBUG
            // Readable files and real line numbers while developing.
            BundleTable.EnableOptimizations = false;
#else
            BundleTable.EnableOptimizations = true;
#endif
        }
    }
}
