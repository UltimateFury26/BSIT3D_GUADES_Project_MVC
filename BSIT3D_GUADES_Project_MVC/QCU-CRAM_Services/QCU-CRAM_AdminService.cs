using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Services
{
    /// <summary>
    /// The answer to a write operation. The Web Forms pages returned a sentence and
    /// guessed from its wording whether it was good news; an explicit flag is honest
    /// and lets the controller pick Flash or FlashError without string-sniffing.
    /// </summary>
    public class QCUCRAMOperationResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; }

        /// <summary>True when the failure is worth re-opening the panel for (e.g. slot just taken).</summary>
        public bool Retry { get; set; }

        public static QCUCRAMOperationResult Success(string message)
        {
            return new QCUCRAMOperationResult { Ok = true, Message = message };
        }

        public static QCUCRAMOperationResult Failure(string message)
        {
            return new QCUCRAMOperationResult { Ok = false, Message = message };
        }
    }

    /// <summary>
    /// Phase 07 - every admin operation in one place.
    ///
    /// This is the MVC replacement for the code-behind of Dashboard.aspx,
    /// Queue.aspx, CheckIn.aspx, Records.aspx and Restrictions.aspx. Those five
    /// pages each opened their own SqlConnection and built their own DataTable;
    /// here every read and every write goes through the Phase 02 repositories, so
    /// the data layer stays the only place that talks to SQL.
    ///
    /// It is an instance class with constructor injection (the same shape as
    /// QCUCRAMStudentService) rather than the static helper the conversion guide
    /// sketched, because this project has no EF DbContext factory to lean on.
    /// </summary>
    public class QCUCRAMAdminService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly QCUCRAM_ReservationRepository _reservations;
        private readonly QCUCRAM_ContentRepository _content;
        private readonly QCUCRAM_StudentRepository _students;
        private readonly QCUCRAM_NotificationRepository _notifications;
        private readonly QCUCRAM_ReportRepository _reports;

        public QCUCRAMAdminService()
            : this(new QCUCRAM_ReservationRepository(), new QCUCRAM_ContentRepository(),
                   new QCUCRAM_StudentRepository(), new QCUCRAM_NotificationRepository(),
                   new QCUCRAM_ReportRepository())
        {
        }

        public QCUCRAMAdminService(QCUCRAM_ReservationRepository reservations,
                                   QCUCRAM_ContentRepository content,
                                   QCUCRAM_StudentRepository students,
                                   QCUCRAM_NotificationRepository notifications,
                                   QCUCRAM_ReportRepository reports)
        {
            if (reservations == null) { throw new ArgumentNullException("reservations"); }
            if (content == null) { throw new ArgumentNullException("content"); }
            if (students == null) { throw new ArgumentNullException("students"); }
            if (notifications == null) { throw new ArgumentNullException("notifications"); }
            if (reports == null) { throw new ArgumentNullException("reports"); }

            _reservations = reservations;
            _content = content;
            _students = students;
            _notifications = notifications;
            _reports = reports;
        }

        // ==================================================================
        //  Dashboard
        // ==================================================================

        public QCUCRAMAdminDashboardViewModel Dashboard()
        {
            DateTime now = QCUCRAM_Clock.Now;
            DateTime today = now.Date;

            QCUCRAM_DashboardCounts counts = _reports.Dashboard();

            QCUCRAMAdminDashboardViewModel vm = new QCUCRAMAdminDashboardViewModel
            {
                Today = today,
                Now = now,
                PendingCount = counts.PendingToday,
                RestrictedCount = counts.RestrictedStudents
            };

            vm.Cards.Add(new QCUCRAMStatCard
            {
                Label = "Pending today",
                Value = counts.PendingToday.ToString(Inv),
                Hint = counts.PendingToday == 1 ? "request needs a decision" : "requests need a decision",
                Tone = counts.PendingToday > 0 ? "is-warn" : string.Empty,
                Url = "/admin/queue"
            });

            vm.Cards.Add(new QCUCRAMStatCard
            {
                Label = "Approved today",
                Value = counts.ApprovedToday.ToString(Inv),
                Hint = "bookings expected on site",
                Tone = string.Empty,
                Url = "/admin/check-in"
            });

            vm.Cards.Add(new QCUCRAMStatCard
            {
                Label = "Checked in today",
                Value = counts.CheckedInToday.ToString(Inv),
                Hint = "groups already at the desk",
                Tone = string.Empty,
                Url = "/admin/check-in"
            });

            vm.Cards.Add(new QCUCRAMStatCard
            {
                Label = "Registered requesters",
                Value = counts.TotalStudents.ToString(Inv),
                Hint = "students, faculty and visitors",
                Tone = string.Empty,
                Url = "/admin/records"
            });

            vm.Cards.Add(new QCUCRAMStatCard
            {
                Label = "Restricted",
                Value = counts.RestrictedStudents.ToString(Inv),
                Hint = counts.RestrictedStudents == 1 ? "requester cannot book" : "requesters cannot book",
                Tone = counts.RestrictedStudents > 0 ? "is-bad" : string.Empty,
                Url = "/admin/restrictions"
            });

            // Today's confirmed bookings, in slot order.
            Dictionary<int, QCUCRAM_Student> map = LoadStudents();

            foreach (QCUCRAM_ReservationListItem row in _reservations.GetByDate(today))
            {
                if (row.Status != QCUCRAM_Status.Approved &&
                    row.Status != QCUCRAM_Status.Reassigned &&
                    row.Status != QCUCRAM_Status.CheckedIn)
                {
                    continue;
                }

                DateTime start = row.ReservationDate.Date.Add(row.StartTime);
                DateTime end = row.ReservationDate.Date.Add(row.EndTime);

                vm.Upcoming.Add(new QCUCRAMUpcomingRow
                {
                    ReservationID = row.ReservationId,
                    Requester = DisplayName(map, row.StudentId),
                    RoomName = row.RoomName,
                    SlotLabel = row.SlotLabel,
                    Status = row.Status,
                    StatusCss = QCUCRAM_Status.CssClass(row.Status),
                    NumberOfMembers = row.NumberOfMembers,
                    StartTime = row.StartTime,
                    IsNow = now >= start && now < end
                });
            }

            return vm;
        }

        // ==================================================================
        //  Approval queue
        // ==================================================================

        public QCUCRAMQueueViewModel Queue(int reviewId, int reassignId)
        {
            DateTime now = QCUCRAM_Clock.Now;
            DateTime today = now.Date;

            Dictionary<int, QCUCRAM_Student> map = LoadStudents();

            QCUCRAMQueueViewModel vm = new QCUCRAMQueueViewModel
            {
                ReviewID = reviewId,
                ReassignID = reassignId
            };

            foreach (QCUCRAM_ReservationListItem row in _reservations.GetQueue())
            {
                DateTime start = row.ReservationDate.Date.Add(row.StartTime);

                QCUCRAMQueueRow item = new QCUCRAMQueueRow
                {
                    ReservationID = row.ReservationId,
                    Requester = DisplayName(map, row.StudentId),
                    StudentNumber = row.StudentNumber,
                    Program = ProgramOf(map, row.StudentId),
                    RoomName = row.RoomName,
                    ReservationDate = row.ReservationDate,
                    SlotLabel = row.SlotLabel,
                    NumberOfMembers = row.NumberOfMembers,
                    Status = row.Status,
                    StatusCss = QCUCRAM_Status.CssClass(row.Status),
                    CreatedAt = row.CreatedAt,
                    IsToday = row.ReservationDate.Date == today
                };

                if (row.Status == QCUCRAM_Status.Pending)
                {
                    item.CanApprove = start > now;
                    item.CanDecline = true;
                    item.CanReassign = start > now;
                    vm.Pending.Add(item);
                }
                else
                {
                    item.CanReassign = start > now;
                    vm.Upcoming.Add(item);
                }
            }

            if (reviewId > 0)
            {
                vm.Detail = Detail(reviewId);
            }

            if (reassignId > 0)
            {
                vm.Reassign = BuildReassign(reassignId);
            }

            return vm;
        }

        /// <summary>
        /// The "move to another room and time" panel. Only rooms that fit the group
        /// and slots that are still free are offered, which is what the original
        /// FillTargets() did.
        /// </summary>
        public QCUCRAMReassignViewModel BuildReassign(int reservationId)
        {
            QCUCRAM_ReservationInfo info = _reservations.GetInfo(reservationId);
            if (info == null) { return null; }

            QCUCRAM_ReservationListItem row = _reservations.GetByDate(info.Date)
                .FirstOrDefault(x => x.ReservationId == reservationId);

            int members = row == null ? 1 : row.NumberOfMembers;

            QCUCRAMReassignViewModel vm = new QCUCRAMReassignViewModel
            {
                ReservationID = info.ReservationId,
                CurrentRoomName = info.RoomName,
                CurrentSlotLabel = info.SlotLabel,
                ReservationDate = info.Date
            };

            foreach (QCUCRAM_Room room in _content.GetRooms(true))
            {
                if (room.MaxCapacity < members) { continue; }

                foreach (QCUCRAM_TimeSlot slot in _content.GetTimeSlots(true))
                {
                    bool isCurrent = room.RoomID == info.RoomId && slot.TimeSlotID == info.TimeSlotId;
                    if (isCurrent) { continue; }

                    if (_reservations.IsSlotHeld(room.RoomID, info.Date, slot.TimeSlotID)) { continue; }

                    vm.Targets.Add(new QCUCRAMReassignTarget
                    {
                        Value = room.RoomID.ToString(Inv) + "|" + slot.TimeSlotID.ToString(Inv),
                        Label = room.RoomName + " \u00b7 " +
                                QCUCRAM_ReservationInfo.FormatSlot(slot.StartTime, slot.EndTime)
                    });
                }
            }

            return vm;
        }

        public QCUCRAMOperationResult Approve(int reservationId, int adminId)
        {
            QCUCRAM_ReservationInfo info = _reservations.GetInfo(reservationId);

            if (info == null)
            {
                return QCUCRAMOperationResult.Failure("Reservation #" + reservationId + " was not found.");
            }
            if (info.Status != QCUCRAM_Status.Pending)
            {
                return QCUCRAMOperationResult.Failure(
                    "Reservation #" + reservationId + " is no longer pending (it is " + info.Status + ").");
            }
            if (info.SlotStart <= QCUCRAM_Clock.Now)
            {
                return QCUCRAMOperationResult.Failure(
                    "Reservation #" + reservationId + " cannot be approved because its slot has already " +
                    "started. Decline it instead.");
            }

            try
            {
                _reservations.ChangeStatus(reservationId, QCUCRAM_Status.Approved,
                    QCUCRAM_ReservationRepository.ActorAdmin, adminId);

                return QCUCRAMOperationResult.Success(
                    "Reservation #" + reservationId + " was approved. The requester has been notified.");
            }
            catch (InvalidOperationException ex)
            {
                return QCUCRAMOperationResult.Failure(ex.Message);
            }
            catch (Exception)
            {
                return QCUCRAMOperationResult.Failure("Something went wrong while approving. Please try again.");
            }
        }

        public QCUCRAMOperationResult Decline(int reservationId, string reason, int adminId)
        {
            string why = (reason ?? string.Empty).Trim();

            if (why.Length == 0)
            {
                return QCUCRAMOperationResult.Failure("Please enter a reason before declining.");
            }
            if (why.Length > 200)
            {
                return QCUCRAMOperationResult.Failure("The reason must be 200 characters or fewer.");
            }

            QCUCRAM_ReservationInfo info = _reservations.GetInfo(reservationId);
            if (info == null)
            {
                return QCUCRAMOperationResult.Failure("Reservation #" + reservationId + " was not found.");
            }
            if (!QCUCRAM_ReservationRepository.CanTransition(info.Status, QCUCRAM_Status.Declined))
            {
                return QCUCRAMOperationResult.Failure(
                    "Reservation #" + reservationId + " is " + info.Status + " and cannot be declined.");
            }

            try
            {
                _reservations.ChangeStatus(reservationId, QCUCRAM_Status.Declined,
                    QCUCRAM_ReservationRepository.ActorAdmin, adminId, why);

                return QCUCRAMOperationResult.Success(
                    "Reservation #" + reservationId + " was declined. The requester has been notified.");
            }
            catch (InvalidOperationException ex)
            {
                return QCUCRAMOperationResult.Failure(ex.Message);
            }
            catch (Exception)
            {
                return QCUCRAMOperationResult.Failure("Something went wrong while declining. Please try again.");
            }
        }

        public QCUCRAMOperationResult DoReassign(int reservationId, string target, string reason, int adminId)
        {
            string why = (reason ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(target))
            {
                return QCUCRAMOperationResult.Failure("Please choose the new room and time.");
            }
            if (why.Length == 0)
            {
                return QCUCRAMOperationResult.Failure("Please enter a reason for the reassignment.");
            }

            string[] parts = target.Split('|');
            int roomId, slotId;
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], NumberStyles.Integer, Inv, out roomId) ||
                !int.TryParse(parts[1], NumberStyles.Integer, Inv, out slotId))
            {
                return QCUCRAMOperationResult.Failure("Please choose the new room and time.");
            }

            try
            {
                _reservations.Reassign(reservationId, roomId, slotId, why, adminId);

                return QCUCRAMOperationResult.Success(
                    "Reservation #" + reservationId + " was reassigned. The requester has been notified.");
            }
            catch (InvalidOperationException ex)
            {
                QCUCRAMOperationResult result = QCUCRAMOperationResult.Failure(ex.Message);

                // Keep the panel open with fresh options when the target was taken
                // between rendering the page and submitting it.
                QCUCRAM_ReservationInfo info = _reservations.GetInfo(reservationId);
                result.Retry = info != null &&
                               QCUCRAM_ReservationRepository.CanTransition(info.Status, QCUCRAM_Status.Reassigned) &&
                               info.SlotStart > QCUCRAM_Clock.Now;

                return result;
            }
            catch (Exception)
            {
                return QCUCRAMOperationResult.Failure(
                    "Something went wrong while reassigning. Please try again.");
            }
        }

        // ==================================================================
        //  Check-in desk
        // ==================================================================

        public QCUCRAMCheckInViewModel CheckInBoard(string search)
        {
            DateTime now = QCUCRAM_Clock.Now;
            DateTime today = now.Date;

            int grace = Math.Max(1, _content.GetSettingInt("GraceMinutes", 15));
            string q = (search ?? string.Empty).Trim();

            Dictionary<int, QCUCRAM_Student> map = LoadStudents();

            QCUCRAMCheckInViewModel vm = new QCUCRAMCheckInViewModel
            {
                Now = now,
                GraceMinutes = grace,
                Search = q
            };

            foreach (QCUCRAM_ReservationListItem row in _reservations.GetByDate(today))
            {
                if (row.Status != QCUCRAM_Status.Approved &&
                    row.Status != QCUCRAM_Status.Reassigned &&
                    row.Status != QCUCRAM_Status.CheckedIn)
                {
                    continue;
                }

                string name = DisplayName(map, row.StudentId);

                if (q.Length > 0 && !Matches(q, row, name))
                {
                    continue;
                }

                DateTime start = row.ReservationDate.Date.Add(row.StartTime);
                DateTime closes = start.AddMinutes(grace);

                QCUCRAMCheckInRow item = new QCUCRAMCheckInRow
                {
                    ReservationID = row.ReservationId,
                    Requester = name,
                    StudentNumber = row.StudentNumber,
                    Program = ProgramOf(map, row.StudentId),
                    RoomName = row.RoomName,
                    SlotLabel = row.SlotLabel,
                    ReservationDate = row.ReservationDate,
                    NumberOfMembers = row.NumberOfMembers,
                    Status = row.Status,
                    StatusCss = QCUCRAM_Status.CssClass(row.Status),
                    SlotStart = start,
                    GraceEndsAt = closes,
                    CheckedInAt = row.CheckedInAt
                };

                if (row.Status == QCUCRAM_Status.CheckedIn)
                {
                    vm.CheckedIn.Add(item);
                    continue;
                }

                if (now < start)
                {
                    item.CanCheckIn = false;
                    item.BlockedReason = "Opens at " + start.ToString("h:mm tt", Inv);
                }
                else if (now <= closes)
                {
                    item.CanCheckIn = true;
                    item.MinutesLeft = (int)Math.Ceiling((closes - now).TotalMinutes);
                    item.BlockedReason = string.Empty;
                }
                else
                {
                    item.CanCheckIn = false;
                    item.BlockedReason = "Closed at " + closes.ToString("h:mm tt", Inv) + " - will be forfeited";
                }

                vm.Waiting.Add(item);
            }

            return vm;
        }

        public QCUCRAMOperationResult CheckIn(int reservationId, int adminId)
        {
            QCUCRAM_ReservationInfo info = _reservations.GetInfo(reservationId);

            if (info == null)
            {
                return QCUCRAMOperationResult.Failure("Reservation #" + reservationId + " was not found.");
            }
            if (info.Status != QCUCRAM_Status.Approved && info.Status != QCUCRAM_Status.Reassigned)
            {
                return QCUCRAMOperationResult.Failure(
                    "Reservation #" + reservationId + " is " + info.Status + " and can't be checked in.");
            }

            int grace = Math.Max(1, _content.GetSettingInt("GraceMinutes", 15));
            DateTime now = QCUCRAM_Clock.Now;
            DateTime start = info.SlotStart;
            DateTime closes = start.AddMinutes(grace);

            if (now < start)
            {
                return QCUCRAMOperationResult.Failure(
                    "Too early: check-in for reservation #" + reservationId + " opens at " +
                    start.ToString("h:mm tt", Inv) + ".");
            }
            if (now > closes)
            {
                return QCUCRAMOperationResult.Failure(
                    "The grace period for reservation #" + reservationId + " ended at " +
                    closes.ToString("h:mm tt", Inv) + ". It will be forfeited.");
            }

            try
            {
                _reservations.ChangeStatus(reservationId, QCUCRAM_Status.CheckedIn,
                    QCUCRAM_ReservationRepository.ActorAdmin, adminId);

                return QCUCRAMOperationResult.Success(
                    "Reservation #" + reservationId + " is checked in.");
            }
            catch (InvalidOperationException ex)
            {
                return QCUCRAMOperationResult.Failure(ex.Message);
            }
            catch (Exception)
            {
                return QCUCRAMOperationResult.Failure(
                    "Something went wrong while checking in. Please try again.");
            }
        }

        // ==================================================================
        //  Restrictions (no-show strikes)
        // ==================================================================

        public QCUCRAMRestrictionsViewModel Restrictions()
        {
            DateTime now = QCUCRAM_Clock.Now;

            int threshold = Math.Max(1, _content.GetSettingInt("NoShowThreshold", 3));
            int days = Math.Max(1, _content.GetSettingInt("RestrictionDays", 7));

            QCUCRAMRestrictionsViewModel vm = new QCUCRAMRestrictionsViewModel
            {
                Threshold = threshold,
                RestrictionDays = days
            };

            foreach (QCUCRAM_Student student in _students.Search())
            {
                if (student.IsRestricted)
                {
                    string until;
                    string remaining;

                    if (!student.RestrictionLiftDate.HasValue)
                    {
                        until = "Until lifted manually";
                        remaining = "-";
                    }
                    else
                    {
                        DateTime lift = student.RestrictionLiftDate.Value;
                        until = lift.ToString("MMM d, yyyy h:mm tt", Inv);
                        remaining = lift <= now ? "Ending at the next sweep" : RemainingText(lift - now);
                    }

                    vm.Restricted.Add(new QCUCRAMRestrictedRow
                    {
                        StudentID = student.StudentID,
                        FullName = FullName(student),
                        StudentNumber = student.StudentNumber,
                        Program = student.Program,
                        RequesterType = student.RequesterType,
                        NoShowCount = student.NoShowCount,
                        RestrictionLiftDate = student.RestrictionLiftDate,
                        UntilText = until,
                        RemainingText = remaining
                    });
                }
                else if (student.NoShowCount == threshold - 1 && threshold > 1)
                {
                    vm.Warnings.Add(new QCUCRAMStrikeWarningRow
                    {
                        StudentID = student.StudentID,
                        FullName = FullName(student),
                        StudentNumber = student.StudentNumber,
                        Program = student.Program,
                        NoShowCount = student.NoShowCount,
                        StrikesRemaining = threshold - student.NoShowCount
                    });
                }
            }

            return vm;
        }

        /// <summary>
        /// Clears the restriction, resets the strikes and notifies the requester in
        /// one transaction - the same three writes the original LiftRestriction did.
        /// </summary>
        public QCUCRAMOperationResult LiftRestriction(int studentId, string note, int adminId)
        {
            QCUCRAM_Student student = _students.GetById(studentId);

            if (student == null)
            {
                return QCUCRAMOperationResult.Failure("That requester was not found.");
            }
            if (!student.IsRestricted)
            {
                return QCUCRAMOperationResult.Failure("That requester is no longer restricted.");
            }

            string trimmed = (note ?? string.Empty).Trim();
            if (trimmed.Length > 150) { trimmed = trimmed.Substring(0, 150); }

            try
            {
                int affected = 0;

                _students.Db.RunInTransaction(System.Data.IsolationLevel.ReadCommitted, (con, tx) =>
                {
                    affected = QCUCRAM_Db.Exec(con, tx,
                        @"UPDATE Students
                             SET IsRestricted = 0, NoShowCount = 0, RestrictionLiftDate = NULL
                           WHERE StudentID = @s AND IsRestricted = 1",
                        QCUCRAM_Db.P("@s", studentId));

                    if (affected != 1) { return; }

                    string message = "Your booking restriction was lifted. Your strike count was reset " +
                                     "and you can reserve rooms again." +
                                     (trimmed.Length == 0 ? string.Empty : " Note: " + trimmed);

                    if (message.Length > 400) { message = message.Substring(0, 397) + "..."; }

                    QCUCRAM_NotificationRepository.Insert(con, tx, studentId, null, message, QCUCRAM_Clock.Now);
                });

                if (affected != 1)
                {
                    return QCUCRAMOperationResult.Failure("That requester is no longer restricted.");
                }

                return QCUCRAMOperationResult.Success(
                    "The restriction was lifted, the strikes were reset and the requester was notified.");
            }
            catch (Exception)
            {
                return QCUCRAMOperationResult.Failure(
                    "Something went wrong while lifting the restriction. Please try again.");
            }
        }

        // ==================================================================
        //  Records
        // ==================================================================

        public QCUCRAMRecordsViewModel Records(QCUCRAMRecordsFilter filter)
        {
            if (filter == null) { filter = new QCUCRAMRecordsFilter(); }

            Dictionary<int, QCUCRAM_Student> map = LoadStudents();
            List<QCUCRAMRecordsRow> rows = LoadRecordRows(filter, map);

            QCUCRAMRecordsViewModel vm = new QCUCRAMRecordsViewModel { Filter = filter };

            vm.TotalRows = rows.Count;

            int pageSize = filter.PageSize < 1 ? 25 : filter.PageSize;
            int totalPages = (int)Math.Ceiling(rows.Count / (double)pageSize);
            if (totalPages < 1) { totalPages = 1; }

            int page = filter.Page < 1 ? 1 : (filter.Page > totalPages ? totalPages : filter.Page);
            filter.Page = page;

            vm.Rows = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            vm.Pager = new QCUCRAMPagerViewModel
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = rows.Count,
                UrlFor = p => RecordsUrl(filter, p)
            };

            // ---- filter drop-downs ----
            vm.StatusOptions.Add(new KeyValuePair<string, string>(string.Empty, "All statuses"));
            foreach (string status in QCUCRAM_Status.All)
            {
                vm.StatusOptions.Add(new KeyValuePair<string, string>(status, status));
            }

            vm.RoomOptions.Add(new KeyValuePair<string, string>(string.Empty, "All rooms"));
            foreach (QCUCRAM_Room room in _content.GetRooms())
            {
                vm.RoomOptions.Add(new KeyValuePair<string, string>(
                    room.RoomID.ToString(Inv), room.RoomName));
            }

            foreach (string program in QCUCRAM_Programs.All)
            {
                vm.ProgramOptions.Add(program);
            }

            return vm;
        }

        /// <summary>
        /// The CSV the original Records page streamed. UTF-8 with a BOM is added by
        /// the controller so Excel reads the en-dash and the Filipino names correctly.
        /// </summary>
        public string ExportCsv(QCUCRAMRecordsFilter filter)
        {
            if (filter == null) { filter = new QCUCRAMRecordsFilter(); }

            Dictionary<int, QCUCRAM_Student> map = LoadStudents();
            List<QCUCRAMRecordsRow> rows = LoadRecordRows(filter, map);

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("ReservationID,Date,Start,End,Room,StudentNumber,Requester,RequesterType," +
                          "Program,Email,Members,Purpose,Status,CreatedAt");

            foreach (QCUCRAMRecordsRow row in rows)
            {
                QCUCRAM_Student owner = FindStudent(map, row.StudentNumber);

                // The list DTO carries the combined "9:00 AM - 10:00 AM" label, so the
                // export splits it back into the two columns the original page wrote.
                string slot = row.SlotLabel ?? string.Empty;
                string start = slot;
                string end = string.Empty;
                int dash = slot.IndexOf(" - ", StringComparison.Ordinal);
                if (dash > 0)
                {
                    start = slot.Substring(0, dash).Trim();
                    end = slot.Substring(dash + 3).Trim();
                }

                sb.Append(Csv(row.ReservationID.ToString(Inv))).Append(',')
                  .Append(Csv(row.ReservationDate.ToString("yyyy-MM-dd", Inv))).Append(',')
                  .Append(Csv(start)).Append(',')
                  .Append(Csv(end)).Append(',')
                  .Append(Csv(row.RoomName)).Append(',')
                  .Append(Csv(row.StudentNumber)).Append(',')
                  .Append(Csv(row.Requester)).Append(',')
                  .Append(Csv(owner == null ? string.Empty : owner.RequesterType)).Append(',')
                  .Append(Csv(row.Program)).Append(',')
                  .Append(Csv(owner == null ? string.Empty : owner.Email)).Append(',')
                  .Append(Csv(row.NumberOfMembers.ToString(Inv))).Append(',')
                  .Append(Csv(row.Purpose)).Append(',')
                  .Append(Csv(row.Status)).Append(',')
                  .Append(Csv(row.CreatedAt.ToString("yyyy-MM-dd HH:mm", Inv)))
                  .AppendLine();
            }

            return sb.ToString();
        }

        // ==================================================================
        //  Shared detail panel
        // ==================================================================

        public QCUCRAMReservationDetailViewModel Detail(int reservationId)
        {
            QCUCRAM_ReservationInfo info = _reservations.GetInfo(reservationId);
            if (info == null) { return null; }

            QCUCRAM_Student student = _students.GetById(info.StudentId);

            QCUCRAM_ReservationListItem row = _reservations.GetByDate(info.Date)
                .FirstOrDefault(x => x.ReservationId == reservationId);

            QCUCRAMReservationDetailViewModel vm = new QCUCRAMReservationDetailViewModel
            {
                ReservationID = info.ReservationId,
                Status = info.Status,
                StatusCss = QCUCRAM_Status.CssClass(info.Status),
                RequesterName = student == null ? info.Summary : FullName(student),
                StudentNumber = student == null ? string.Empty : student.StudentNumber,
                Program = student == null ? string.Empty : student.Program,
                RequesterType = student == null ? string.Empty : student.RequesterType,
                Email = student == null ? string.Empty : student.Email,
                RoomName = info.RoomName,
                ReservationDate = info.Date,
                SlotLabel = info.SlotLabel,
                SlotStart = info.SlotStart,
                SlotEnd = info.SlotEnd,
                NumberOfMembers = row == null ? 0 : row.NumberOfMembers,
                Purpose = row == null ? string.Empty : row.Purpose,
                CreatedAt = row == null ? info.Date : row.CreatedAt
            };

            // ---- members ----
            if (student != null)
            {
                vm.Members.Add(new QCUCRAMMemberRow
                {
                    FullName = FullName(student),
                    StudentNumber = student.StudentNumber,
                    Program = student.Program,
                    IsRequester = true
                });
            }

            foreach (QCUCRAM_ReservationMember member in _reservations.GetMembers(reservationId))
            {
                vm.Members.Add(new QCUCRAMMemberRow
                {
                    FullName = JoinName(member.LastName, member.FirstName, member.MI),
                    StudentNumber = string.Empty,
                    Program = string.Empty,
                    IsRequester = false
                });
            }

            // ---- history ----
            foreach (QCUCRAM_HistoryItem item in _reservations.GetHistory(reservationId))
            {
                vm.History.Add(new QCUCRAMHistoryRow
                {
                    ChangedAt = item.ChangedAt,
                    FromStatus = item.OldStatus,
                    ToStatus = item.NewStatus,
                    ActorRole = item.ChangedByRole,
                    ActorName = item.ChangedBy.HasValue ? "#" + item.ChangedBy.Value.ToString(Inv) : string.Empty,
                    Note = item.Notes
                });
            }

            // The newest admin decision, if any, becomes the "Decided by / Note" block.
            QCUCRAM_HistoryItem decision = _reservations.GetHistory(reservationId)
                .LastOrDefault(x => string.Equals(x.ChangedByRole,
                    QCUCRAM_ReservationRepository.ActorAdmin, StringComparison.OrdinalIgnoreCase));

            if (decision != null)
            {
                vm.DecidedByName = "Library Assistant" +
                                   (decision.ChangedBy.HasValue
                                        ? " #" + decision.ChangedBy.Value.ToString(Inv)
                                        : string.Empty);
                vm.DecidedAt = decision.ChangedAt;
                vm.DecisionNote = decision.Notes;
            }

            return vm;
        }

        // ==================================================================
        //  Private helpers
        // ==================================================================

        private List<QCUCRAMRecordsRow> LoadRecordRows(QCUCRAMRecordsFilter filter,
                                                       Dictionary<int, QCUCRAM_Student> map)
        {
            List<QCUCRAM_ReservationListItem> source = _reservations.Search(
                filter.From, filter.To, filter.Status, filter.RoomID, filter.Search);

            IEnumerable<QCUCRAM_ReservationListItem> query = source;

            // The repository has no Program column to filter on, so that one filter
            // is applied here against the requester's profile.
            if (!string.IsNullOrWhiteSpace(filter.Program))
            {
                query = query.Where(x =>
                {
                    QCUCRAM_Student s;
                    return map.TryGetValue(x.StudentId, out s) &&
                           string.Equals(s.Program, filter.Program, StringComparison.OrdinalIgnoreCase);
                });
            }

            List<QCUCRAMRecordsRow> rows = query.Select(x => new QCUCRAMRecordsRow
            {
                ReservationID = x.ReservationId,
                ReservationDate = x.ReservationDate,
                SlotLabel = x.SlotLabel,
                RoomName = x.RoomName,
                Requester = DisplayName(map, x.StudentId),
                StudentNumber = x.StudentNumber,
                Program = ProgramOf(map, x.StudentId),
                NumberOfMembers = x.NumberOfMembers,
                Purpose = x.Purpose,
                Status = x.Status,
                StatusCss = QCUCRAM_Status.CssClass(x.Status),
                CreatedAt = x.CreatedAt
            }).ToList();

            bool asc = string.Equals(filter.Dir, "asc", StringComparison.OrdinalIgnoreCase);
            string sort = QCUCRAMRecordsFilter.IsSortKey(filter.Sort) ? filter.Sort : "ReservationDate";

            switch (sort)
            {
                case "ReservationID":
                    rows = asc ? rows.OrderBy(r => r.ReservationID).ToList()
                               : rows.OrderByDescending(r => r.ReservationID).ToList();
                    break;
                case "StartTime":
                    rows = asc ? rows.OrderBy(r => r.SlotLabel, StringComparer.Ordinal).ToList()
                               : rows.OrderByDescending(r => r.SlotLabel, StringComparer.Ordinal).ToList();
                    break;
                case "RoomName":
                    rows = asc ? rows.OrderBy(r => r.RoomName, StringComparer.OrdinalIgnoreCase).ToList()
                               : rows.OrderByDescending(r => r.RoomName, StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                case "Requester":
                    rows = asc ? rows.OrderBy(r => r.Requester, StringComparer.OrdinalIgnoreCase).ToList()
                               : rows.OrderByDescending(r => r.Requester, StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                case "Program":
                    rows = asc ? rows.OrderBy(r => r.Program, StringComparer.OrdinalIgnoreCase).ToList()
                               : rows.OrderByDescending(r => r.Program, StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                case "NumberOfMembers":
                    rows = asc ? rows.OrderBy(r => r.NumberOfMembers).ToList()
                               : rows.OrderByDescending(r => r.NumberOfMembers).ToList();
                    break;
                case "Status":
                    rows = asc ? rows.OrderBy(r => r.Status, StringComparer.OrdinalIgnoreCase).ToList()
                               : rows.OrderByDescending(r => r.Status, StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                default:
                    rows = asc ? rows.OrderBy(r => r.ReservationDate).ThenBy(r => r.SlotLabel).ToList()
                               : rows.OrderByDescending(r => r.ReservationDate).ThenByDescending(r => r.SlotLabel).ToList();
                    break;
            }

            return rows;
        }

        private static string RecordsUrl(QCUCRAMRecordsFilter filter, int page)
        {
            StringBuilder sb = new StringBuilder("/admin/records?");

            sb.Append("status=").Append(HttpUtility.UrlEncode(filter.Status ?? string.Empty));
            sb.Append("&room=").Append(filter.RoomID.HasValue
                ? filter.RoomID.Value.ToString(Inv) : string.Empty);
            sb.Append("&program=").Append(HttpUtility.UrlEncode(filter.Program ?? string.Empty));
            sb.Append("&from=").Append(filter.From.HasValue
                ? filter.From.Value.ToString("yyyy-MM-dd", Inv) : string.Empty);
            sb.Append("&to=").Append(filter.To.HasValue
                ? filter.To.Value.ToString("yyyy-MM-dd", Inv) : string.Empty);
            sb.Append("&search=").Append(HttpUtility.UrlEncode(filter.Search ?? string.Empty));
            sb.Append("&sort=").Append(HttpUtility.UrlEncode(filter.Sort ?? "ReservationDate"));
            sb.Append("&dir=").Append(HttpUtility.UrlEncode(filter.Dir ?? "desc"));
            sb.Append("&page=").Append(page.ToString(Inv));

            return sb.ToString();
        }

        private Dictionary<int, QCUCRAM_Student> LoadStudents()
        {
            Dictionary<int, QCUCRAM_Student> map = new Dictionary<int, QCUCRAM_Student>();

            foreach (QCUCRAM_Student student in _students.Search())
            {
                map[student.StudentID] = student;
            }

            return map;
        }

        private static QCUCRAM_Student FindStudent(Dictionary<int, QCUCRAM_Student> map, string studentNumber)
        {
            if (string.IsNullOrWhiteSpace(studentNumber)) { return null; }

            foreach (QCUCRAM_Student student in map.Values)
            {
                if (string.Equals(student.StudentNumber, studentNumber, StringComparison.OrdinalIgnoreCase))
                {
                    return student;
                }
            }

            return null;
        }

        private static string DisplayName(Dictionary<int, QCUCRAM_Student> map, int studentId)
        {
            QCUCRAM_Student student;
            return map.TryGetValue(studentId, out student) ? FullName(student) : "Unknown requester";
        }

        private static string ProgramOf(Dictionary<int, QCUCRAM_Student> map, int studentId)
        {
            QCUCRAM_Student student;
            return map.TryGetValue(studentId, out student) ? student.Program : string.Empty;
        }

        private static string FullName(QCUCRAM_Student student)
        {
            return JoinName(student.LastName, student.FirstName, student.MI);
        }

        /// <summary>"Dela Cruz, Juan P." - the same order the original grids used.</summary>
        private static string JoinName(string lastName, string firstName, string mi)
        {
            string name = (lastName ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(firstName))
            {
                name = name.Length == 0 ? firstName.Trim() : name + ", " + firstName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(mi))
            {
                name = name + " " + mi.Trim().TrimEnd('.') + ".";
            }

            return name;
        }

        private static bool Matches(string q, QCUCRAM_ReservationListItem row, string name)
        {
            if (row.ReservationId.ToString(Inv).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            if ((row.StudentNumber ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            if ((name ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            if ((row.RoomName ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }

            return false;
        }

        private static string RemainingText(TimeSpan span)
        {
            if (span.TotalHours < 1) { return Math.Max(1, (int)Math.Ceiling(span.TotalMinutes)) + " min"; }
            if (span.TotalDays < 1) { return (int)span.TotalHours + " hr " + span.Minutes + " min"; }

            return (int)span.TotalDays + " day(s) " + span.Hours + " hr";
        }

        private static string Csv(string value)
        {
            string text = value ?? string.Empty;

            if (text.IndexOf('"') >= 0) { text = text.Replace("\"", "\"\""); }

            if (text.IndexOf(',') >= 0 || text.IndexOf('"') >= 0 ||
                text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0)
            {
                return "\"" + text + "\"";
            }

            return text;
        }
    }
}
