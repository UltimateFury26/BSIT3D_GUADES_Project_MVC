using System;
using System.Web;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>
    /// Phase 05 - the model behind Views/Shared/_QCU-CRAM_Pager.cshtml.
    ///
    /// The Web Forms pages paged by rebuilding the query string on every
    /// "Page $N" LinkButton. This keeps the same idea but puts the URL
    /// building in one place: UrlFor(page) returns the href for that page.
    /// </summary>
    public class QCUCRAMPagerViewModel
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }

        /// <summary>Builds the href for a page number. Defaults to "?page=N".</summary>
        public Func<int, string> UrlFor { get; set; }

        public QCUCRAMPagerViewModel()
        {
            Page = 1;
            PageSize = 20;
            UrlFor = p => "?page=" + p;
        }

        /// <summary>Never less than 1, so the pager can divide by PageSize safely.</summary>
        public int TotalPages
        {
            get
            {
                if (PageSize <= 0) { return 1; }
                int pages = (int)Math.Ceiling(TotalItems / (double)PageSize);
                return pages < 1 ? 1 : pages;
            }
        }

        /// <summary>True when there is more than one page and the pager should render at all.</summary>
        public bool HasPages { get { return TotalPages > 1; } }

        /// <summary>Clamps an incoming ?page= value to something the pager can draw.</summary>
        public static int ClampPage(int requested, int pageSize, int totalItems)
        {
            int pages = pageSize <= 0 ? 1 : (int)Math.Ceiling(totalItems / (double)pageSize);
            if (pages < 1) { pages = 1; }
            if (requested < 1) { return 1; }
            if (requested > pages) { return pages; }
            return requested;
        }

        /// <summary>Builds a pager that keeps the current query string and only replaces ?page=.</summary>
        public static QCUCRAMPagerViewModel FromRequest(HttpRequestBase request, int page, int pageSize, int total)
        {
            var query = HttpUtility.ParseQueryString(request == null || request.Url == null
                ? string.Empty
                : request.Url.Query);

            return new QCUCRAMPagerViewModel
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                UrlFor = p =>
                {
                    query["page"] = p.ToString();
                    return "?" + query;
                }
            };
        }
    }
}
