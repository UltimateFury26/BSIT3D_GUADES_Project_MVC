using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Filters;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;

namespace BSIT3D_GUADES_Project_MVC
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            // Phase 10: our own error filter replaces the stock HandleErrorAttribute.
            // It logs first, keeps 404 as 404, answers AJAX with JSON and shows the
            // reference code on the shared error page.
            filters.Add(new QCUCRAMHandleErrorAttribute());

            // Phase 10: request-driven maintenance heartbeat. Cheap no-op when the
            // sweep is not due; never throws into the page.
            filters.Add(new QCUCRAMSweepOnRequestAttribute());

            // Phase 04: turns ?msg=... and TempData flash messages into
            // ViewBag.QCUCRAM_NoticeText / QCUCRAM_NoticeKind for the layout.
            filters.Add(new QCUCRAM_NoticeFilter());
        }
    }
}
