using System;
using System.Collections.Generic;
using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Services;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.Controllers.Student
{
    /// <summary>
    /// Phase 06 - the whole student module, in one controller.
    ///
    /// Replaces these five Web Forms pages:
    ///
    ///   Guades_QCUCram_Student/Guades_QCUCram_Dashboard.aspx       -> Dashboard
    ///   Guades_QCUCram_Student/Guades_QCUCram_Reserve.aspx         -> Reserve (GET/POST)
    ///   Guades_QCUCram_Student/Guades_QCUCram_MyReservations.aspx  -> MyReservations, Cancel
    ///   Guades_QCUCram_Student/Guades_QCUCram_Notifications.aspx   -> Notifications, MarkRead, MarkAllRead
    ///   Guades_QCUCram_Student/Guades_QCUCram_Profile.aspx         -> Profile (GET/POST), ChangePassword
    ///
    /// URL shape. The class sits in Controllers.Student and derives QCUCRAM_StudentController,
    /// which already carries [QCUCRAM_RequireStudent] and [QCUCRAM_RequireLogin]. Attribute
    /// routing is used so the URLs read /student/dashboard rather than /student/student/dashboard,
    /// while the conventional "student" route still serves /student for the dashboard.
    /// </summary>
    [RoutePrefix("student")]
    public class StudentController : QCUCRAM_StudentController
    {
        private const int ReservationsPageSize = 10;
        private const int NotificationsPageSize = 15;

        private readonly QCUCRAMStudentService _service;

        public StudentController()
        {
            _service = new QCUCRAMStudentService();
        }

        public StudentController(QCUCRAMStudentService service)
        {
            _service = service ?? new QCUCRAMStudentService();
        }

        /// <summary>
        /// Disposes the service (and through it every repository / EF context) at the
        /// end of the request. Controller.Dispose runs after the result is executed,
        /// so the whole action has finished using the connection by then.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_service != null) { _service.Dispose(); }
            }
            base.Dispose(disposing);
        }

        // ==================================================================
        //  Dashboard
        // ==================================================================

        [HttpGet]
        [Route("~/student")]
        [Route("dashboard")]
        public ActionResult Dashboard(string month)
        {
            QCUCRAMStudentDashboardViewModel model = _service.BuildDashboard(CurrentUserId, ParseMonth(month));

            if (model == null || model.Student == null)
            {
                // The account vanished between the session check and the read.
                return RedirectToAction("Logout", "Account");
            }

            ViewBag.Title = "Dashboard";
            return View("~/Views/Student/QCU-CRAM_Dashboard.cshtml", model);
        }

        // ==================================================================
        //  Reserve
        // ==================================================================

        [HttpGet]
        [Route("reserve")]
        public ActionResult Reserve(int? room, int? slot, string date)
        {
            QCUCRAMReserveViewModel model = _service.BuildReserve(
                CurrentUserId, room, slot, ParseDate(date, null));

            ViewBag.Title = "Reserve a Room";
            return View("~/Views/Student/QCU-CRAM_Reserve.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("reserve")]
        public ActionResult Reserve(QCUCRAMReserveViewModel model)
        {
            if (model == null)
            {
                model = new QCUCRAMReserveViewModel();
            }

            // A hand-rolled post can arrive with no member rows at all, or with an empty slot.
            List<QCUCRAM_MemberInput> members = new List<QCUCRAM_MemberInput>();
            if (model.Members != null)
            {
                foreach (QCUCRAM_MemberInput m in model.Members)
                {
                    members.Add(m ?? new QCUCRAM_MemberInput());
                }
            }
            model.Members = members;

            // btnSubmit_Click read the date as exactly yyyy-MM-dd; anything else = missing/invalid
            // (DateTime.MinValue), which the service reports as "Please choose a valid date."
            DateTime postedDate;
            bool dateOk = DateTime.TryParseExact((Request.Form["Date"] ?? string.Empty).Trim(), "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out postedDate);
            model.Date = dateOk ? postedDate.Date : DateTime.MinValue;

            // Every rule is checked by the service. Binder complaints about an empty or malformed
            // field ("A value is required.") must not show up as extra lines in the error list.
            ModelState.Clear();

            // The requester is always the signed-in student; a posted StudentID is never read.
            QCUCRAM_CreateRequest request = new QCUCRAM_CreateRequest
            {
                StudentId = CurrentUserId,
                Date = model.Date,
                RoomId = model.RoomId,
                TimeSlotId = model.TimeSlotId,
                Purpose = (model.Purpose ?? string.Empty).Trim(),
                NumberOfMembers = model.NumberOfMembers,
                Members = members
            };

            QCUCRAM_CreateResult result;
            try
            {
                result = _service.Create(request);
            }
            catch (Exception ex)
            {
                // The transaction has already rolled back; nothing was saved.
                QCUCRAMErrorLog.Write(ex, "student-reserve", CurrentUserId.ToString());
                FlashError("Something went wrong while saving your request. Please try again.");
                Rehydrate(model);
                ViewBag.Title = "Reserve a Room";
                return View("~/Views/Student/QCU-CRAM_Reserve.cshtml", model);
            }

            if (result.Success)
            {
                // Step 18 redirected with ?created=ID; MyReservations shows the confirmation.
                return RedirectToAction("MyReservations", new { created = result.ReservationId });
            }

            // Show every rule violation at once, the way the Web Forms ValidationSummary did.
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            Rehydrate(model);
            ViewBag.Title = "Reserve a Room";
            return View("~/Views/Student/QCU-CRAM_Reserve.cshtml", model);
        }

        // ==================================================================
        //  My Reservations
        // ==================================================================

        [HttpGet]
        [Route("my-reservations")]
        public ActionResult MyReservations(string status, int? page, int? created)
        {
            QCUCRAMMyReservationsViewModel model = _service.GetMyReservations(
                CurrentUserId,
                status,
                page ?? 1,
                ReservationsPageSize,
                p => Url.Action("MyReservations", new { status, page = p }),
                created);

            // Phase 13: the Web Forms page showed this as the upper-right toast, only for the
            // requester's own booking (the service already nulls CreatedId otherwise), and only
            // on the first load. A flash/?msg= notice already queued by the filter is kept.
            if (model.CreatedId.HasValue && ViewBag.QCUCRAM_NoticeText == null)
            {
                ViewBag.QCUCRAM_NoticeText = "Your request #" + model.CreatedId.Value +
                                             " was submitted. You'll be notified once it's reviewed.";
                ViewBag.QCUCRAM_NoticeKind = "success";
            }

            ViewBag.Title = "My Reservations";
            return View("~/Views/Student/QCU-CRAM_MyReservations.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("cancel")]
        public ActionResult Cancel(int? id, string status, int? page)
        {
            // Web Forms: a command argument that is not a number is ignored silently.
            if (!id.HasValue)
            {
                return RedirectToAction("MyReservations", new { status, page = page ?? 1 });
            }

            string error;
            bool ok = _service.Cancel(id.Value, CurrentUserId, out error);

            if (ok)
            {
                Flash("Reservation #" + id.Value + " was cancelled. The slot is open again.");
            }
            else
            {
                FlashError(error ?? "Something went wrong while cancelling. Please try again.");
            }

            return RedirectToAction("MyReservations", new { status, page = page ?? 1 });
        }

        // ==================================================================
        //  Notifications
        // ==================================================================

        [HttpGet]
        [Route("notifications")]
        public ActionResult Notifications(bool unread = false, int? page = null)
        {
            QCUCRAMNotificationsViewModel model = _service.GetNotifications(
                CurrentUserId,
                unread,
                page ?? 1,
                NotificationsPageSize,
                p => Url.Action("Notifications", new { unread, page = p }));

            ViewBag.Title = "Notifications";
            return View("~/Views/Student/QCU-CRAM_Notifications.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("notifications/read")]
        public ActionResult MarkRead(int? id, bool unread = false, int? page = null)
        {
            // Web Forms: a command argument that is not a number is ignored silently.
            // The service/repository UPDATE is scoped by StudentID, so another student's id changes nothing.
            if (id.HasValue)
            {
                _service.MarkRead(id.Value, CurrentUserId);
            }

            return RedirectToAction("Notifications", new { unread, page = page ?? 1 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("notifications/read-all")]
        public ActionResult MarkAllRead(bool unread = false, int? page = null)
        {
            int changed = _service.MarkAllRead(CurrentUserId);

            // Same wording as btnMarkAll_Click.
            Flash(changed == 0
                ? "You have no unread notifications."
                : changed + " notification(s) marked as read.");

            return RedirectToAction("Notifications", new { unread, page = page ?? 1 });
        }

        // ==================================================================
        //  Profile
        // ==================================================================

        [HttpGet]
        [Route("profile")]
        [ActionName("Profile")]
        public ActionResult ProfilePage()
        {
            QCUCRAMProfileViewModel model = _service.GetProfile(CurrentUserId);

            if (model == null)
            {
                return RedirectToAction("Logout", "Account");
            }

            ViewBag.Title = "Profile";
            return View("~/Views/Student/QCU-CRAM_Profile.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("profile")]
        [ActionName("Profile")]
        public ActionResult ProfilePage(QCUCRAMProfileViewModel model)
        {
            if (model == null)
            {
                model = new QCUCRAMProfileViewModel();
            }

            if (!ModelState.IsValid)
            {
                Rehydrate(model);
                ViewBag.Title = "Profile";
                return View("~/Views/Student/QCU-CRAM_Profile.cshtml", model);
            }

            int code;
            try
            {
                code = _service.UpdateProfile(
                    CurrentUserId,
                    model.LastName,
                    model.FirstName,
                    model.MI,
                    model.Email,
                    model.Program);
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                // 2601 / 2627 = unique constraint (someone took the e-mail a moment ago)
                code = (ex.Number == 2601 || ex.Number == 2627) ? 1 : -1;
            }
            catch (Exception)
            {
                code = -1;
            }

            switch (code)
            {
                case 0:
                    // Keep the "Logged in as ..." name in step with the new name (first + last, as Login does).
                    QCUCRAM_Session.RefreshName((model.FirstName ?? string.Empty).Trim() + " " + (model.LastName ?? string.Empty).Trim());
                    Flash("Your profile has been updated.");
                    return RedirectToAction("Profile");

                case 1:
                    ModelState.AddModelError("Email", "That email address is already used by another account. Please use a different one.");
                    break;

                case 2:
                    FlashError("Your account could not be found. Please sign in again.");
                    return RedirectToAction("Logout", "Account");

                case 3:
                    ModelState.AddModelError(string.Empty, "Some of the details were not accepted. Please check them and try again.");
                    break;

                default:
                    ModelState.AddModelError(string.Empty, "Sorry, your changes could not be saved. Please try again.");
                    break;
            }

            Rehydrate(model);
            ViewBag.Title = "Profile";
            return View("~/Views/Student/QCU-CRAM_Profile.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("profile/password")]
        public ActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
            {
                FlashError("Please fill in your current password and your new password.");
                return RedirectToAction("Profile");
            }

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                FlashError("The new password and the confirmation do not match.");
                return RedirectToAction("Profile");
            }

            // Same policy the repository enforces, checked here first so the student gets
            // the precise reason instead of a generic rejection after the round trip.
            string strength = QCUCRAM_PasswordHash.GetStrengthMessage(newPassword);
            if (strength != null)
            {
                FlashError(strength);
                return RedirectToAction("Profile");
            }

            int code = _service.ChangePassword(CurrentUserId, currentPassword, newPassword);

            switch (code)
            {
                case 0:
                    Flash("Your password was changed.");
                    break;

                case 1:
                    FlashError("Your current password is not correct.");
                    break;

                case 2:
                    FlashError("Your account could not be found. Please sign in again.");
                    break;

                case 3:
                    FlashError(QCUCRAM_PasswordHash.GetStrengthMessage(newPassword)
                               ?? "The new password was not accepted.");
                    break;

                default:
                    FlashError("The new password was not accepted. Use at least 8 characters.");
                    break;
            }

            return RedirectToAction("Profile");
        }

        // ==================================================================
        //  Helpers
        // ==================================================================

        /// <summary>
        /// Refills the read-only parts of the Reserve model after a failed post, so the
        /// re-rendered page still has its room and slot drop-downs, the rules list and the
        /// requester banner.
        /// </summary>
        private void Rehydrate(QCUCRAMReserveViewModel model)
        {
            QCUCRAMReserveViewModel fresh = _service.BuildReserve(
                CurrentUserId, model.RoomId, model.TimeSlotId, model.Date);

            model.Requester = fresh.Requester;
            model.IsRestricted = fresh.IsRestricted;
            model.NoShowCount = fresh.NoShowCount;
            model.RestrictionLiftDate = fresh.RestrictionLiftDate;
            model.Rooms = fresh.Rooms;
            model.TimeSlots = fresh.TimeSlots;
            model.RulesHtml = fresh.RulesHtml;
            model.BookingDay = fresh.BookingDay;
            model.MinMembers = fresh.MinMembers;
            model.MaxMembers = fresh.MaxMembers;
            model.MaxPurposeLength = fresh.MaxPurposeLength;

            // Keep the rows the student typed, but never fewer than the minimum.
            if (model.Members == null)
            {
                model.Members = new List<QCUCRAM_MemberInput>();
            }

            while (model.Members.Count < model.MinMembers)
            {
                model.Members.Add(new QCUCRAM_MemberInput());
            }
        }

        /// <summary>Refills the read-only parts of the Profile model after a failed save.</summary>
        private void Rehydrate(QCUCRAMProfileViewModel model)
        {
            QCUCRAMProfileViewModel fresh = _service.GetProfile(CurrentUserId);
            if (fresh == null) { return; }

            model.StudentID = fresh.StudentID;
            model.StudentNumber = fresh.StudentNumber;
            model.RequesterType = fresh.RequesterType;
            model.IsRestricted = fresh.IsRestricted;
            model.NoShowCount = fresh.NoShowCount;
            model.NoShowThreshold = fresh.NoShowThreshold;
            model.RestrictionLiftDate = fresh.RestrictionLiftDate;
            model.Stats = fresh.Stats;
        }

        private static DateTime? ParseDate(string value, DateTime? fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) { return fallback; }

            DateTime parsed;
            if (DateTime.TryParse(value.Trim(), System.Globalization.CultureInfo.InvariantCulture,
                                  System.Globalization.DateTimeStyles.None, out parsed))
            {
                return parsed.Date;
            }

            return fallback;
        }

        /// <summary>
        /// Reads the calendar's yyyy-MM (or any parseable date) into the first day of that
        /// month. Null means "whatever month it is now", which the service decides.
        /// </summary>
        private static DateTime? ParseMonth(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) { return null; }

            DateTime parsed;

            if (DateTime.TryParseExact(value.Trim(), "yyyy-MM",
                                       System.Globalization.CultureInfo.InvariantCulture,
                                       System.Globalization.DateTimeStyles.None, out parsed))
            {
                return new DateTime(parsed.Year, parsed.Month, 1);
            }

            if (DateTime.TryParse(value.Trim(), System.Globalization.CultureInfo.InvariantCulture,
                                  System.Globalization.DateTimeStyles.None, out parsed))
            {
                return new DateTime(parsed.Year, parsed.Month, 1);
            }

            return null;
        }
    }
}
