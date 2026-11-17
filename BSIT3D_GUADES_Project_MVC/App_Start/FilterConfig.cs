using System.Web.Mvc;

namespace BSIT3D_GUADES_Project_MVC
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            // Phase 10 swaps this for QCUCRAMHandleErrorAttribute.
            filters.Add(new HandleErrorAttribute());
        }
    }
}
