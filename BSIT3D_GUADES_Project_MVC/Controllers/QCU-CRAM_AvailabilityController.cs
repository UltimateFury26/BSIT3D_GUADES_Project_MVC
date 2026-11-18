using System;
using System.Globalization;
using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Services;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.Controllers
{
    /// <summary>
    /// The MVC replacement for Guades_QCUCram_Availability.aspx.
    ///
    /// The original page inherited Guades_QCUCram_SecurePage, so it demanded a
    /// signed-in user of ANY role (student or library assistant) and bounced a
    /// guest to the login page with ?msg=login. [QCUCRAM_RequireLogin] is the MVC
    /// equivalent of that OnInit gate. The grid is not a public page.
    ///
    /// Phase 09: the page now reads ONLY ?date=yyyy-MM-dd, like the original.
    /// The MVC-only /availability/day/{id} URL and the ?room=&amp;slot= highlight
    /// were not in the source and have been removed.
    ///
    /// The toolbar buttons were post-backs in Web Forms. Here they are GET submits
    /// of one form (nav = show | prev | next | booking) that redirect to
    /// /availability?date=..., so Previous / Next day still start from whatever is
    /// in the date box, exactly like btnPrev_Click / btnNext_Click.
    /// </summary>
    public class AvailabilityController : QCUCRAM_BaseController
    {
        private const string DateFormat = "yyyy-MM-dd";

        // Wording copied from Guades_QCUCram_Availability.aspx.cs GetSelectedDate().
        private const string InvalidDateMessage = "That date isn't valid, so the booking day is shown instead.";

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
        //  GET /availability?date=yyyy-MM-dd
        // ------------------------------------------------------------------
        [QCUCRAM_RequireLogin]
        [HttpGet]
        public ActionResult Index(string date, string nav)
        {
            if (!string.IsNullOrEmpty(nav))
            {
                return Navigate(date, nav);
            }

            // Page_Load: default = the booking day, or ?date= when it is an exact yyyy-MM-dd.
            DateTime day;
            if (!TryParseExact(date, out day))
            {
                day = _service.BookingDay;
            }

            int? studentId = QCUCRAM_Session.IsStudent ? (int?)QCUCRAM_Session.UserId : null;

            QCUCRAMAvailabilityViewModel model = _service.Build(day, studentId, QCUCRAM_Session.IsAdmin);

            return View("~/Views/Availability/QCU-CRAM_Index.cshtml", model);
        }

        // ------------------------------------------------------------------
        //  Show / Previous day / Next day / Booking day
        // ------------------------------------------------------------------
        private ActionResult Navigate(string date, string nav)
        {
            DateTime bookingDay = _service.BookingDay;
            DateTime target;

            if (string.Equals(nav, "booking", StringComparison.OrdinalIgnoreCase))
            {
                // btnBookingDay_Click ignores the box.
                target = bookingDay;
            }
            else
            {
                // GetSelectedDate(): an unreadable box falls back to the booking day and says so.
                DateTime selected;
                if (!TryParseExact(date, out selected))
                {
                    selected = bookingDay;
                    FlashError(InvalidDateMessage);
                }

                if (string.Equals(nav, "prev", StringComparison.OrdinalIgnoreCase)) { target = selected.AddDays(-1); }
                else if (string.Equals(nav, "next", StringComparison.OrdinalIgnoreCase)) { target = selected.AddDays(1); }
                else { target = selected; }   // "show"
            }

            return RedirectToAction("Index", new { date = target.ToString(DateFormat, CultureInfo.InvariantCulture) });
        }

        /// <summary>
        /// Exact yyyy-MM-dd only (invariant culture), the same DateTime.TryParseExact the
        /// original used, so a dd/MM/yyyy browser can never flip the month and the day and
        /// free-text such as "March 5" is not accepted.
        /// </summary>
        private static bool TryParseExact(string value, out DateTime date)
        {
            DateTime parsed;
            if (DateTime.TryParseExact((value ?? string.Empty).Trim(), DateFormat,
                                       CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                date = parsed.Date;
                return true;
            }

            date = DateTime.MinValue;
            return false;
        }
    }
}
