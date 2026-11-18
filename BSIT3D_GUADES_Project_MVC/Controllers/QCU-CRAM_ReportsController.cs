using System;
using System.Diagnostics;
using System.Globalization;
using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Services;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.Controllers
{
    /// <summary>
    /// Phase 09 - the three Reports screens.
    ///
    /// Replaces three Web Forms pages:
    ///   Guades_QCUCram_Analytics.aspx      -> Analytics
    ///   Guades_QCUCram_DailySchedule.aspx  -> DailySchedule
    ///   Guades_QCUCram_PrintBooking.aspx   -> PrintBooking
    ///
    /// Analytics and Daily Schedule are admin-only, exactly as the Web Forms
    /// pages were (both inherited Guades_QCUCram_AdminPage). Print Booking is
    /// shared: an assistant can print any booking, a student can print their own.
    ///
    /// All three are GETs, so there is no [ValidateAntiForgeryToken] anywhere -
    /// a token on a GET would break bookmarking and the browser's Back button.
    /// </summary>
    [RoutePrefix("reports")]
    public class ReportsController : QCUCRAM_BaseController
    {
        private const string MaxRangeMessage = "Please choose a range of 10 years or less.";
        private const string BadOrderMessage = "The From date cannot be later than the To date.";
        private const string LoadFailedMessage = "Sorry, the analytics could not be loaded. Please try again.";
        private const string InvalidDateMessage = "Please choose a valid date.";
        private const string ScheduleLoadFailedMessage = "Sorry, the schedule could not be loaded. Please try again.";

        private readonly QCUCRAMAnalyticsService _analytics;
        private readonly QCUCRAMReportService _reports;

        public ReportsController()
        {
            _analytics = new QCUCRAMAnalyticsService();
            _reports = new QCUCRAMReportService();
        }

        public ReportsController(QCUCRAMAnalyticsService analytics, QCUCRAMReportService reports)
        {
            _analytics = analytics ?? new QCUCRAMAnalyticsService();
            _reports = reports ?? new QCUCRAMReportService();
        }

        // =================================================================
        //  Analytics
        // =================================================================

        /// <summary>
        /// Booking trends, room utilisation and no-show statistics.
        ///
        /// The date range comes from the query string (?from=&amp;to= or ?range=),
        /// which means a particular view can be bookmarked and shared, unlike the
        /// Web Forms page where the range lived in ViewState.
        /// </summary>
        [HttpGet]
        [Route("analytics")]
        [QCUCRAM_RequireAdmin]
        [QCUCRAM_RequireLogin]
        public ActionResult Analytics(string from, string to, string range)
        {
            DateTime start;
            DateTime end;
            string preset;
            string error;

            if (!TryResolveRange(from, to, range, out start, out end, out preset, out error))
            {
                QCUCRAMAnalyticsViewModel invalid = new QCUCRAMAnalyticsViewModel();
                invalid.From = start;
                invalid.To = end;
                invalid.Preset = preset;
                invalid.HasResults = false;

                // R09: Analytics.aspx.cs ShowError -> ShowMessage(lblMessage, text, true) is an error toast,
                // and pnlResults.Visible = false. The view is rendered in this request, so flash it here.
                FlashError(error);

                return View("QCU-CRAM_Analytics", invalid);
            }

            try
            {
                QCUCRAMAnalyticsViewModel model = _analytics.Build(start, end, preset);
                return View("QCU-CRAM_Analytics", model);
            }
            catch (Exception ex)
            {
                Trace.TraceError("QCUCRAM analytics failed: " + ex);

                QCUCRAMAnalyticsViewModel failed = new QCUCRAMAnalyticsViewModel();
                failed.From = start;
                failed.To = end;
                failed.Preset = preset;
                failed.HasResults = false;
                FlashError(LoadFailedMessage);

                return View("QCU-CRAM_Analytics", failed);
            }
        }

        /// <summary>
        /// Works out which dates the assistant asked for.
        ///
        /// Order of precedence:
        ///   1. an explicit ?from=&amp;to= pair always wins and is labelled "custom";
        ///   2. ?range=month  -> the 1st to the last day of the current month;
        ///   3. ?range=all    -> the earliest to the latest reservation date;
        ///   4. anything else -> the last 30 days.
        ///
        /// Returns false (with a message) for an inverted range or one longer than
        /// ten years, so the caller can render the form again without the cards.
        /// </summary>
        private bool TryResolveRange(string from, string to, string range,
                                     out DateTime start, out DateTime end,
                                     out string preset, out string error)
        {
            DateTime today = QCUCRAM_Clock.Today;

            preset = "last30";
            error = null;

            if (from != null || to != null)
            {
                // Phase 17: Analytics.aspx.cs TryReadRange - both dates, yyyy-MM-dd exactly.
                DateTime f, t;
                bool okFrom = DateTime.TryParseExact((from ?? string.Empty).Trim(), "yyyy-MM-dd",
                                  CultureInfo.InvariantCulture, DateTimeStyles.None, out f);
                bool okTo = DateTime.TryParseExact((to ?? string.Empty).Trim(), "yyyy-MM-dd",
                                  CultureInfo.InvariantCulture, DateTimeStyles.None, out t);

                preset = "custom";

                if (!okFrom || !okTo)
                {
                    start = okFrom ? f.Date : today.AddDays(-29);
                    end = okTo ? t.Date : today;
                    error = "Please enter both a valid From date and To date.";
                    return false;
                }

                start = f.Date;
                end = t.Date;
            }
            else if (string.Equals(range, "month", StringComparison.OrdinalIgnoreCase))
            {
                DateTime first = new DateTime(today.Year, today.Month, 1);
                start = first;
                end = first.AddMonths(1).AddDays(-1);
                preset = "month";
            }
            else if (string.Equals(range, "all", StringComparison.OrdinalIgnoreCase))
            {
                QCUCRAMDateBounds bounds = _analytics.DateBounds();

                if (bounds.HasValue)
                {
                    start = bounds.Min.Value.Date;
                    end = bounds.Max.Value.Date;
                }
                else
                {
                    // Nothing in the table yet - show an empty last-30 window rather
                    // than a range that cannot be rendered.
                    start = today.AddDays(-29);
                    end = today;
                }

                preset = "all";
            }
            else
            {
                start = today.AddDays(-29);
                end = today;
                preset = "last30";
            }

            if (start > end)
            {
                error = BadOrderMessage;
                return false;
            }

            if ((end - start).TotalDays > 3660)
            {
                error = MaxRangeMessage;
                return false;
            }

            return true;
        }

        // =================================================================
        //  Daily Schedule
        // =================================================================

        /// <summary>
        /// One day's confirmed bookings, ready to print in landscape.
        ///
        /// The Web Forms page had five postback buttons (previous, next, today,
        /// tomorrow, show). Here they are plain links carrying ?date=, so every
        /// view is a real URL that can be bookmarked.
        /// </summary>
        [HttpGet]
        [Route("daily-schedule")]
        [QCUCRAM_RequireAdmin]
        [QCUCRAM_RequireLogin]
        public ActionResult DailySchedule(string date)
        {
            // Phase 17: pressing Show with the box emptied gave "Please choose a valid date."
            // and hid the results (btnShow_Click -> TryReadDate -> ShowError).
            string raw = Request.QueryString["date"];
            if (raw != null && raw.Trim().Length == 0)
            {
                return DailyScheduleError(QCUCRAM_Clock.Today, InvalidDateMessage);
            }

            // R10: DailySchedule.aspx.cs Page_Load - ?date= must be exactly yyyy-MM-dd,
            // anything else silently falls back to today (Clock.Today, no message).
            DateTime day;
            if (!DateTime.TryParseExact((date ?? string.Empty).Trim(), "yyyy-MM-dd",
                                        CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
            {
                day = QCUCRAM_Clock.Today;
            }

            // R10: DailySchedule.aspx.cs LoadSchedule - the try/catch around the query shows
            // "Sorry, the schedule could not be loaded..." as an error toast and hides the results.
            try
            {
                QCUCRAMDailyScheduleViewModel model = _reports.DailySchedule(day.Date);
                return View("QCU-CRAM_DailySchedule", model);
            }
            catch (Exception ex)
            {
                QCUCRAMErrorLog.Write(ex, "reports-daily-schedule", day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                return DailyScheduleError(day.Date, ScheduleLoadFailedMessage);
            }
        }

        /// <summary>
        /// R10: DailySchedule.aspx.cs ShowError = pnlResults.Visible = false + ShowMessage(lblMessage, text, true),
        /// and ShowMessage queues an error toast. The view is rendered in this request, so flash it here.
        /// A non-empty ErrorMessage tells the view to leave the results out.
        /// </summary>
        private ActionResult DailyScheduleError(DateTime day, string message)
        {
            QCUCRAMDailyScheduleViewModel failed = new QCUCRAMDailyScheduleViewModel();
            failed.Date = day;
            failed.ErrorMessage = message;
            FlashError(message);
            return View("QCU-CRAM_DailySchedule", failed);
        }

        // =================================================================
        //  Printable booking sheet
        // =================================================================

        /// <summary>
        /// The printable sheet for one reservation.
        ///
        /// The id arrives as a string so a non-numeric value ("abc") can return a
        /// real 404 instead of being silently bound to 0 and reported as "not found".
        /// </summary>
        [HttpGet]
        [Route("print-booking/{id}")]
        [QCUCRAM_RequireLogin]
        public ActionResult PrintBooking(string id)
        {
            int reservationId;
            if (!int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out reservationId))
            {
                reservationId = 0;
            }

            // PrintBooking.aspx.cs: a bad or foreign id keeps the Back button and shows a toast.
            QCUCRAMPrintBookingViewModel model;
            try
            {
                model = _reports.PrintBooking(reservationId, IsAdmin, CurrentUserId);
            }
            catch (Exception ex)
            {
                QCUCRAMErrorLog.Write(ex, "reports-print-booking", id);
                model = new QCUCRAMPrintBookingViewModel
                {
                    IsAdmin = IsAdmin,
                    BackUrl = IsAdmin ? "/admin/records" : "/student/my-reservations"
                };
                FlashError("Sorry, the report could not be loaded. Please try again.");
                return View("QCU-CRAM_PrintBooking", model);
            }

            if (!model.Found) { FlashError(model.ErrorMessage); }

            return View("QCU-CRAM_PrintBooking", model);
        }
    }
}
