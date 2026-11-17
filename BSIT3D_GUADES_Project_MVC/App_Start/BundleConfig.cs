using System.Web;
using System.Web.Optimization;

namespace BSIT3D_GUADES_Project_MVC
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                        "~/Scripts/jquery.validate*"));

            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new Bundle("~/bundles/bootstrap").Include(
                      "~/Scripts/bootstrap.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/bootstrap.css",
                      "~/Content/site.css"));

            // ---- QCU-CRAM application assets (Phase 05 fills these files) ----
            bundles.Add(new StyleBundle("~/QCU-CRAM_Content/css")
                        .Include("~/QCU-CRAM_Content/QCU-CRAM_Portfolio.css"));

            bundles.Add(new ScriptBundle("~/QCU-CRAM_Scripts/js")
                        .Include("~/QCU-CRAM_Scripts/QCU-CRAM_Portfolio.js"));
        }
    }
}
