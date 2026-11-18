using System;
using System.Globalization;
using System.Text;
using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Services;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.Controllers
{
    /// <summary>
    /// Phase 07 - the searchable reservation records screen.
    ///
    /// Replaces Guades_QCUCram_Admin/Guades_QCUCram_Records.aspx. The Web Forms page
    /// kept the filter, the sort and the page number in ViewState; here they live in
    /// the query string, which means a filtered view can be bookmarked, shared with
    /// another assistant, and reloaded after a decision without losing its place.
    ///
    /// The CSV export streams the same filtered set the table is showing, not just
    /// the current page - that is what the original "Export CSV" button did.
    /// </summary>
    [RoutePrefix("admin/records")]
    public class AdminRecordsController : QCUCRAM_AdminController
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly QCUCRAMAdminService _service;

        public AdminRecordsController()
        {
            _service = new QCUCRAMAdminService();
        }

        public AdminRecordsController(QCUCRAMAdminService service)
        {
            _service = service ?? new QCUCRAMAdminService();
        }

        [HttpGet]
        [Route("")]
        public ActionResult Index(string status, int? room, string program,
                                  string from, string to, string q,
                                  string sort, string dir, int? page, int? detail)
        {
            QCUCRAMRecordsFilter filter = BuildFilter(status, room, program, from, to, q, sort, dir, page);

            // Phase 16A: GetFilteredTable showed the message and an empty grid.
            string error = ValidateDates(from, to);

            QCUCRAMRecordsViewModel model = _service.Records(filter);

            if (error != null)
            {
                model.FilterError = error;
                model.Rows.Clear();
                model.TotalRows = 0;
                return View("~/Views/Admin/QCU-CRAM_Records.cshtml", model);
            }

            if (detail.HasValue && detail.Value > 0)
            {
                model.Detail = _service.Detail(detail.Value);
            }

            return View("~/Views/Admin/QCU-CRAM_Records.cshtml", model);
        }

        /// <summary>
        /// The slide-over detail panel. Loaded by the records table with a plain GET
        /// so the browser can cache nothing and the panel always shows live data.
        /// </summary>
        [HttpGet]
        [Route("{id:int}/detail")]
        public ActionResult Detail(int id)
        {
            QCUCRAMReservationDetailViewModel model = _service.Detail(id);

            if (model == null)
            {
                return HttpNotFound("That reservation no longer exists.");
            }

            return PartialView("~/Views/Admin/_QCU-CRAM_ReservationDetail.cshtml", model);
        }

        [HttpGet]
        [Route("export")]
        public ActionResult ExportCsv(string status, int? room, string program,
                                      string from, string to, string q,
                                      string sort, string dir, int? page)
        {
            QCUCRAMRecordsFilter filter = BuildFilter(status, room, program, from, to, q, sort, dir, page);

            object back = new { status = status, room = room, program = program, from = from, to = to, q = q, sort = sort, dir = dir };

            // An invalid date: the Index page shows the same message the source showed.
            if (ValidateDates(from, to) != null)
            {
                return RedirectToAction("Index", back);
            }

            string csv;
            try
            {
                csv = _service.ExportCsv(filter);
            }
            catch (Exception)
            {
                FlashError("Sorry, the export could not be created. Please try again.");
                return RedirectToAction("Index", back);
            }

            if (csv == null)
            {
                FlashError("There are no reservations to export for these filters.");
                return RedirectToAction("Index", back);
            }

            // UTF-8 with a BOM so Excel opens the en-dash and the Filipino names
            // correctly instead of guessing the ANSI code page.
            byte[] bytes = new UTF8Encoding(true).GetBytes(csv);

            string name = "CRAM_Reservations_" +
                          QCUCRAM_Clock.Now.ToString("yyyyMMdd_HHmm", Inv) + ".csv";

            return File(bytes, "text/csv", name);
        }

        // ==================================================================
        //  Query-string parsing
        // ==================================================================

        private static QCUCRAMRecordsFilter BuildFilter(string status, int? room, string program,
                                                        string from, string to, string q,
                                                        string sort, string dir, int? page)
        {
            QCUCRAMRecordsFilter filter = new QCUCRAMRecordsFilter
            {
                // Records.aspx.cs only applied ?status= when it was one of the list items.
                Status = Array.IndexOf(QCUCRAM_Status.All, (status ?? string.Empty).Trim()) >= 0
                    ? status.Trim() : string.Empty,
                RoomID = room.HasValue && room.Value > 0 ? room : (int?)null,
                Program = (program ?? string.Empty).Trim(),
                From = ParseDate(from),
                To = ParseDate(to),
                Search = (q ?? string.Empty).Trim(),
                Page = page.HasValue && page.Value > 0 ? page.Value : 1
            };

            // Whitelist the sort key, exactly like the Web Forms SortKeys dictionary:
            // anything else falls back to the default instead of reaching the SQL.
            string key = (sort ?? string.Empty).Trim();
            filter.Sort = QCUCRAMRecordsFilter.IsSortKey(key) ? key : "ReservationDate";

            filter.Dir = string.Equals((dir ?? string.Empty).Trim(), "asc",
                                       StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";

            return filter;
        }

        /// <summary>The two messages Records.aspx.cs GetFilteredTable showed; null when the dates are fine.</summary>
        private static string ValidateDates(string from, string to)
        {
            bool hasFrom = !string.IsNullOrWhiteSpace(from);
            bool hasTo = !string.IsNullOrWhiteSpace(to);
            DateTime? f = ParseDate(from);
            DateTime? t = ParseDate(to);

            if ((hasFrom && !f.HasValue) || (hasTo && !t.HasValue))
            {
                return "Enter the dates as year-month-day, for example 2026-10-06.";
            }
            if (f.HasValue && t.HasValue && f.Value > t.Value)
            {
                return "The From date is after the To date.";
            }
            return null;
        }

        /// <summary>
        /// Parses the yyyy-MM-dd the date inputs post. InvariantCulture is deliberate:
        /// a machine set to en-PH would otherwise accept 03/04/2026 and read it as
        /// March 4th, which silently changes the filter.
        /// </summary>
        private static DateTime? ParseDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) { return null; }

            DateTime parsed;
            if (DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", Inv,
                                       DateTimeStyles.None, out parsed))
            {
                return parsed;
            }

            return null;
        }
    }
}
