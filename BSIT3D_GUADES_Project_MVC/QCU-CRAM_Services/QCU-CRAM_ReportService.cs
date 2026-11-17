using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Services
{
    /// <summary>
    /// Phase 09 - the Daily Schedule sheet and the printable booking report.
    ///
    /// Port of Guades_QCUCram_Admin/Guades_QCUCram_DailySchedule.aspx.cs and
    /// Guades_QCUCram_Admin/Guades_QCUCram_PrintBooking.aspx.cs.
    ///
    /// Both Web Forms pages built their output with a StringBuilder and wrote it
    /// straight to the response. Here they return a view model and the Razor file
    /// renders it, which is what makes the sheets styleable and printable.
    ///
    /// The confirmed statuses come from QCUCRAM_Status, never from a literal, so a
    /// new status can never silently drop off the sheet.
    /// </summary>
    public class QCUCRAMReportService
    {
        private readonly QCUCRAM_Db _db;

        public QCUCRAMReportService() : this(new QCUCRAM_Db()) { }

        public QCUCRAMReportService(QCUCRAM_Db db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        // =================================================================
        //  Daily Schedule
        // =================================================================

        /// <summary>
        /// Every confirmed booking on one day, ordered by start time then room.
        ///
        /// Only the four "the slot is held" statuses are listed - Pending,
        /// Declined, Cancelled and Forfeited are not on the sheet, because the
        /// library assistant uses it to see who is actually coming in.
        /// </summary>
        public QCUCRAMDailyScheduleViewModel DailySchedule(DateTime date)
        {
            DateTime day = date.Date;

            QCUCRAMDailyScheduleViewModel vm = new QCUCRAMDailyScheduleViewModel();
            vm.Date = day;
            vm.DateTitle = day.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture);
            vm.PrintedAt = QCUCRAM_Clock.Now;
            vm.PrintedBy = QCUCRAM_Session.UserName;

            // StartTime / EndTime are projected raw: FormatSlot is a C# method and
            // EF cannot translate it, so the label is built on the row itself.
            List<QCUCRAMScheduleRow> rows = _db.Context.Reservations
                .Where(r => r.ReservationDate == day
                         && (r.Status == QCUCRAM_Status.Approved
                          || r.Status == QCUCRAM_Status.Reassigned
                          || r.Status == QCUCRAM_Status.CheckedIn
                          || r.Status == QCUCRAM_Status.Completed))
                .OrderBy(r => r.TimeSlot.StartTime)
                .ThenBy(r => r.Room.RoomName)
                .Select(r => new QCUCRAMScheduleRow
                {
                    StartTime = r.TimeSlot.StartTime,
                    EndTime = r.TimeSlot.EndTime,
                    RoomName = r.Room.RoomName,
                    StudentNumber = r.Student.StudentNumber,
                    RequesterName = r.Student.LastName + ", " + r.Student.FirstName,
                    RequesterType = r.Student.RequesterType,
                    Program = r.Student.Program,
                    NumberOfMembers = r.NumberOfMembers,
                    Purpose = r.Purpose,
                    Status = r.Status
                })
                .ToList();

            vm.Rows = rows;

            // A separate count: the sheet lists confirmed bookings, but the
            // assistant still needs to know how many requests are waiting.
            vm.PendingCount = _db.Context.Reservations
                .Count(r => r.ReservationDate == day && r.Status == QCUCRAM_Status.Pending);

            return vm;
        }

        // =================================================================
        //  Printable booking sheet
        // =================================================================

        /// <summary>
        /// One reservation, ready to print.
        ///
        /// A student may only open their own booking. A missing id and somebody
        /// else's id return the same "could not be found" message, so the screen
        /// cannot be used to probe which reservation numbers exist.
        /// </summary>
        public QCUCRAMPrintBookingViewModel PrintBooking(int reservationId, bool isAdmin, int currentUserId)
        {
            QCUCRAMPrintBookingViewModel vm = new QCUCRAMPrintBookingViewModel();
            vm.IsAdmin = isAdmin;
            vm.BackUrl = isAdmin ? "/admin/records" : "/student/reservations";
            vm.PrintedAt = QCUCRAM_Clock.Now;

            var data = _db.Context.Reservations
                .Where(r => r.ReservationID == reservationId
                         && (isAdmin || r.StudentID == currentUserId))
                .Select(r => new
                {
                    r.ReservationID,
                    r.Status,
                    r.Purpose,
                    r.NumberOfMembers,
                    r.ReservationDate,
                    r.CreatedAt,
                    r.CheckedInAt,
                    StartTime = r.TimeSlot.StartTime,
                    EndTime = r.TimeSlot.EndTime,
                    RoomName = r.Room.RoomName,
                    StudentNumber = r.Student.StudentNumber,
                    LastName = r.Student.LastName,
                    FirstName = r.Student.FirstName,
                    MI = r.Student.MI,
                    Program = r.Student.Program,
                    RequesterType = r.Student.RequesterType,
                    Email = r.Student.Email
                })
                .FirstOrDefault();

            if (data == null)
            {
                vm.Found = false;
                vm.ErrorMessage = "That booking could not be found.";
                return vm;
            }

            vm.Found = true;
            vm.ReservationId = data.ReservationID;
            vm.Status = data.Status;
            vm.Purpose = data.Purpose;
            vm.NumberOfMembers = data.NumberOfMembers;
            vm.ReservationDate = data.ReservationDate;
            vm.CreatedAt = data.CreatedAt;
            vm.CheckedInAt = data.CheckedInAt;
            vm.TimeText = QCUCRAM_ReservationInfo.FormatSlot(data.StartTime, data.EndTime);
            vm.RoomName = data.RoomName;
            vm.StudentNumber = data.StudentNumber;
            vm.Program = data.Program;
            vm.RequesterType = data.RequesterType;
            vm.Email = data.Email;

            string name = (data.LastName ?? string.Empty) + ", " + (data.FirstName ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(data.MI)) { name += " " + data.MI.Trim() + "."; }
            vm.RequesterName = name.Trim().TrimEnd(',');

            // ---- group members, in the order they were entered ----
            vm.Members = _db.Context.ReservationMembers
                .Where(m => m.ReservationID == reservationId)
                .OrderBy(m => m.MemberID)
                .Select(m => new QCUCRAMPrintMemberRow
                {
                    LastName = m.LastName,
                    FirstName = m.FirstName,
                    MI = m.MI
                })
                .ToList();

            // ---- status history, oldest first ----
            List<QCUCRAM_ReservationStatusHistory> history = _db.Context.ReservationStatusHistories
                .Where(h => h.ReservationID == reservationId)
                .OrderBy(h => h.ChangedAt)
                .ThenBy(h => h.HistoryID)
                .ToList();

            // One extra query resolves every admin name at once, rather than one
            // query per history row.
            List<int> adminIds = history
                .Where(h => string.Equals(h.ChangedByRole, QCUCRAM_Constants.Roles.Admin, StringComparison.OrdinalIgnoreCase)
                         && h.ChangedBy.HasValue)
                .Select(h => h.ChangedBy.Value)
                .Distinct()
                .ToList();

            Dictionary<int, string> adminNames = new Dictionary<int, string>();
            if (adminIds.Count > 0)
            {
                adminNames = _db.Context.AdminUsers
                    .Where(a => adminIds.Contains(a.AdminID))
                    .Select(a => new { a.AdminID, a.FullName })
                    .ToList()
                    .ToDictionary(a => a.AdminID, a => a.FullName);
            }

            foreach (QCUCRAM_ReservationStatusHistory h in history)
            {
                string changedBy;
                if (string.Equals(h.ChangedByRole, QCUCRAM_Constants.Roles.Admin, StringComparison.OrdinalIgnoreCase))
                {
                    string full;
                    changedBy = (h.ChangedBy.HasValue && adminNames.TryGetValue(h.ChangedBy.Value, out full)
                                 && !string.IsNullOrWhiteSpace(full))
                              ? full
                              : "Library Assistant";
                }
                else if (string.Equals(h.ChangedByRole, QCUCRAM_Constants.Roles.Student, StringComparison.OrdinalIgnoreCase))
                {
                    changedBy = "Requester";
                }
                else
                {
                    changedBy = "System";
                }

                vm.History.Add(new QCUCRAMPrintHistoryRow
                {
                    OldStatus = h.OldStatus,
                    NewStatus = h.NewStatus,
                    ChangedAt = h.ChangedAt,
                    Notes = h.Notes,
                    ChangedByText = changedBy
                });
            }

            return vm;
        }
    }
}
