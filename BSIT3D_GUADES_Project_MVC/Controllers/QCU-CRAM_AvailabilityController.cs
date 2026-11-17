using System;
using System.Globalization;
using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Services;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.Controllers
{
    /// <summary>
    /// Phase 06 - the MVC replacement for Guades_QCUCram_Availability.aspx.
    ///
    /// The original page was a SecurePage, so it demanded a signed-in user. The grid
    /// itself, though, is harmless to show to a guest: it is the same rooms and slots
    /// that the public landing page advertises. This controller therefore derives from
    /// QCUCRAM_BaseController (not QCUCRAM_StudentController) and marks Index
    /// [AllowAnonymous]. Signed-in users still get the extras:
    ///
    ///   * a student sees which held cells are their own ("mine");
    ///   * an admin sees the requester name in the tooltip;
    ///   * only a student on the exact booking day gets a working "Reserve" link.
    ///
    /// Query string, mirroring the Web Forms post-backs:
    ///   ?date=yyyy-MM-dd   the day being looked at (defaults to the booking day)
    ///   ?room=&amp;slot=       the cell to highlight after a redirect back from Reserve
    /// </summary>
    public class AvailabilityController : QCUCRAM_BaseController
    {
        private readonly QCUCRAMAvailabilityService _service;

        public AvailabilityController()
        {
            _service = new QCUCRAMAvailabilityService();
        }

        public AvailabilityController(QCUCRAMAvailabilityService service)
        {
            _service = service ?? new QCUCRAMAvailabilityService();
        }

        // ------------------------------------------------------------------
        //  GET /availability
        // ------------------------------------------------------------------
        [AllowAnonymous]
        [HttpGet]
        public ActionResult Index(string date, int? room, int? slot)
        {
            DateTime day = ParseDate(date, _service.BookingDay);

            int? studentId = QCUCRAM_Session.IsStudent ? (int?)QCUCRAM_Session.UserId : null;

            QCUCRAMAvailabilityViewModel model = _service.Build(
                day,
                studentId,
                QCUCRAM_Session.IsAdmin,
                room,
                slot);

            return View("~/Views/Availability/QCU-CRAM_Index.cshtml", model);
        }

        // ------------------------------------------------------------------
        //  GET /availability/day/2026-03-14  (optional friendly URL)
        // ------------------------------------------------------------------
        [AllowAnonymous]
        [HttpGet]
        public ActionResult Day(string id, int? room, int? slot)
        {
            return Index(id, room, slot);
        }

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Reads a yyyy-MM-dd date, falling back to <paramref name="fallback"/> when the
        /// value is missing or unparseable. Invariant culture on purpose: the original
        /// page used DateTime.ParseExact with "yyyy-MM-dd" so a dd/MM/yyyy browser could
        /// never silently flip the month and the day.
        /// </summary>
        private static DateTime ParseDate(string value, DateTime fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback.Date;
            }

            DateTime parsed;

            if (DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd",
                                       CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out parsed))
            {
                return parsed.Date;
            }

            if (DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture,
                                  DateTimeStyles.None, out parsed))
            {
                return parsed.Date;
            }

            return fallback.Date;
        }
    }
}
