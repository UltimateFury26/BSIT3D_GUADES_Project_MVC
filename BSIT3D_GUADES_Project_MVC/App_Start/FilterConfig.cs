using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;

namespace BSIT3D_GUADES_Project_MVC
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            // Phase 10 swaps this for QCUCRAMHandleErrorAttribute.
            filters.Add(new HandleErrorAttribute());

            // Phase 04: turns ?msg=... and TempData flash messages into
            // ViewBag.QCUCRAM_NoticeText / QCUCRAM_NoticeKind for the layout.
            filters.Add(new QCUCRAM_NoticeFilter());
        }
    }
}
