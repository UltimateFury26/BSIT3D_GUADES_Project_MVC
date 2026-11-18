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
        private readonly QCUCRAM_AdminUserRepository _admins = new QCUCRAM_AdminUserRepository();

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
                ClockText = now.ToString("ddd, MMM d, yyyy h:mm tt", Inv),
                PendingCount = counts.Pending,
                RestrictedCount = counts.RestrictedStudents
            };

            int offset = QCUCRAM_Clock.OffsetMinutes;
            if (offset != 0)
            {
                vm.ClockNote = "Dev Tools clock is shifted by " + offset.ToString(Inv) + " minute(s).";
            }

            string day = today.ToString("yyyy-MM-dd", Inv);

            // Six cards, same order, same wording, same destinations as Dashboard.aspx.cs.
            vm.Cards.Add(new QCUCRAMStatCard
            {
                Label = "Pending requests",
                Value = counts.Pending.ToString(Inv),
                Hint = "Waiting for approval or decline",
                Accent = counts.Pending > 0 ? "warning" : "secondary",
                Url = "/admin/queue"
            });

            vm.Cards.Add(new QCUCRAMStatCard
            {
                Label = "Awaiting check-in today",
                Value = counts.ApprovedToday.ToString(Inv),
                Hint = "Approved or reassigned for today",
                Accent = "primary",
                Url = "/admin/check-in"
            });

            vm.Cards.Add(new QCUCRAMStatCard
            {
                Label = "Checked in now",
                Value = counts.CheckedInToday.ToString(Inv),
                Hint = "Today's groups currently in a room",
                Accent = "success",
                Url = "/admin/check-in"
            });

            vm.Cards.Add(new QCUCRAMStatCard
            {
                Label = "Forfeited today",
                Value = counts.ForfeitedToday.ToString(Inv),
                Hint = "No-shows from today's slots",
                Accent = counts.ForfeitedToday > 0 ? "danger" : "secondary",
                Url = "/admin/records?status=Forfeited&from=" + day + "&to=" + day
            });

            vm.Cards.Add(new QCUCRAMStatCard
            {
                Label = "Currently restricted",
                Value = counts.RestrictedStudents.ToString(Inv),
                Hint = "Students barred from booking",
                Accent = counts.RestrictedStudents > 0 ? "danger" : "secondary",
                Url = "/admin/restrictions"
            });

            vm.Cards.Add(new QCUCRAMStatCard
            {
                Label = "Total reservations",
                Value = counts.TotalReservations.ToString(Inv),
                Hint = "All records, any status",
                Accent = "secondary",
                Url = "/admin/records"
            });

            // Next five bookings that still hold a slot that has not ended yet.
            foreach (QCUCRAM_ReservationListItem row in _reservations.Upcoming(now, 5))
            {
                DateTime date = row.ReservationDate.Date;

                string badge = null;
                string badgeCss = null;
                if (date == today)
                {
                    badge = "Today";
                    badgeCss = "text-bg-primary";
                }
                else if (date == today.AddDays(1))
                {
                    badge = "Tomorrow";
                    badgeCss = "text-bg-secondary";
                }

                string mi = row.MI;
                string name = row.StudentName +
                              (string.IsNullOrWhiteSpace(mi) ? "" : " " + mi.TrimEnd('.') + ".");

                vm.Upcoming.Add(new QCUCRAMUpcomingRow
                {
                    ReservationID = row.ReservationId,
                    ReservationDate = date,
                    DateBadge = badge,
                    DateBadgeCss = badgeCss,
                    Requester = name,
                    StudentNumber = row.StudentNumber,
                    RoomName = row.RoomName,
                    SlotLabel = row.SlotLabel,
                    Status = row.Status == QCUCRAM_Status.CheckedIn ? "Checked In" : row.Status,
                    StatusCss = DashboardBadge(row.Status),
                    NumberOfMembers = row.NumberOfMembers,
                    StartTime = row.StartTime
                });
            }

            return vm;
        }

        /// <summary>Dashboard.aspx.cs StatusBadge - the exact Bootstrap 5 classes it emitted.</summary>
        private static string DashboardBadge(string status)
        {
            switch (status)
            {
                case QCUCRAM_Status.Pending: return "bg-warning text-dark";
                case QCUCRAM_Status.Approved: return "bg-success";
                case QCUCRAM_Status.Reassigned: return "bg-info text-dark";
                case QCUCRAM_Status.Declined: return "bg-danger";
                case QCUCRAM_Status.Cancelled: return "bg-secondary";
                case QCUCRAM_Status.CheckedIn: return "bg-primary";
                case QCUCRAM_Status.Completed: return "bg-dark";
                case QCUCRAM_Status.Forfeited: return "bg-danger";
                default: return "bg-light text-dark";
            }
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
                Now = now,
                ReviewID = reviewId,
                ReassignID = reassignId
            };

            // Pending grid: every Pending row, oldest first (Queue.aspx.cs BindPending).
            foreach (QCUCRAM_ReservationListItem row in _reservations.GetPending())
            {
                vm.Pending.Add(QueueRow(row, map, now));
            }

            // Upcoming grid: Approved/Reassigned that have not started yet.
            foreach (QCUCRAM_ReservationListItem row in _reservations.GetUpcomingApproved(now))
            {
                vm.Upcoming.Add(QueueRow(row, map, now));
            }

            if (reviewId > 0)
            {
                vm.Detail = Detail(reviewId);
                ApplyQueueNames(vm.Detail, reviewId, map);
            }

            if (reassignId > 0)
            {
                vm.Reassign = BuildReassign(reassignId);
            }

            return vm;
        }

        /// <summary>
        /// Queue.aspx.cs ShowDetails wrote the requester and every member as
        /// "Last, First" + " " + MI with the MI exactly as stored (no trim, no added
        /// period). Detail() is shared with the Records page and uses JoinName, so the
        /// Queue review panel re-applies the as-stored wording here.
        /// </summary>
        private void ApplyQueueNames(QCUCRAMReservationDetailViewModel detail, int reservationId,
                                     Dictionary<int, QCUCRAM_Student> map)
        {
            if (detail == null) { return; }

            QCUCRAM_ReservationInfo info = _reservations.GetInfo(reservationId);
            QCUCRAM_Student student;
            if (info != null && map.TryGetValue(info.StudentId, out student))
            {
                detail.RequesterName = StudentName(student);
            }

            List<QCUCRAM_ReservationMember> members = _reservations.GetMembers(reservationId).ToList();
            for (int i = 0; i < members.Count && i < detail.Members.Count; i++)
            {
                QCUCRAM_ReservationMember m = members[i];
                detail.Members[i].FullName = m.LastName + ", " + m.FirstName +
                                             (string.IsNullOrWhiteSpace(m.MI) ? "" : " " + m.MI);
            }
        }

        /// <summary>A rejected submit that leaves the pop-up open (the Web Forms panel stayed visible).</summary>
        private static QCUCRAMOperationResult RetryFailure(string message)
        {
            QCUCRAMOperationResult result = QCUCRAMOperationResult.Failure(message);
            result.Retry = true;
            return result;
        }

        /// <summary>Queue.aspx.cs StatusBadge - the exact Bootstrap classes it emitted.</summary>
        private static string QueueBadge(string status)
        {
            switch (status)
            {
                case QCUCRAM_Status.Pending: return "text-bg-warning";
                case QCUCRAM_Status.Approved: return "text-bg-success";
                case QCUCRAM_Status.Reassigned: return "text-bg-info";
                default: return "text-bg-secondary";
            }
        }

        /// <summary>One grid row, with the same display values Queue.aspx.cs built.</summary>
        private static QCUCRAMQueueRow QueueRow(QCUCRAM_ReservationListItem row,
                                                 Dictionary<int, QCUCRAM_Student> map,
                                                 DateTime now)
        {
            QCUCRAM_Student student;
            map.TryGetValue(row.StudentId, out student);

            string mi = student == null ? null : student.MI;
            string name = row.StudentName +
                          (string.IsNullOrWhiteSpace(mi) ? "" : " " + mi);

            bool overdue = row.ReservationDate.Date.Add(row.StartTime) <= now;

            return new QCUCRAMQueueRow
            {
                ReservationID = row.ReservationId,
                Requester = name,
                StudentNumber = row.StudentNumber,
                RequesterType = student == null ? string.Empty : student.RequesterType,
                Program = student == null ? string.Empty : student.Program,
                NoShowCount = student == null ? 0 : student.NoShowCount,
                Purpose = row.Purpose,
                RoomName = row.RoomName,
                ReservationDate = row.ReservationDate,
                SlotLabel = row.SlotLabel,
                NumberOfMembers = row.NumberOfMembers,
                Status = row.Status,
                StatusCss = QCUCRAM_Status.CssClass(row.Status),
                StatusBadge = QueueBadge(row.Status),
                CreatedAt = row.CreatedAt,
                IsToday = row.ReservationDate.Date == now.Date,
                Overdue = overdue,
                CanApprove = row.Status == QCUCRAM_Status.Pending && !overdue,
                CanDecline = true,
                CanReassign = !overdue
            };
        }

        /// <summary>
        /// The "move to another room and time" panel. Only rooms that fit the group
        /// and slots that are still free are offered, which is what the original
        /// FillTargets() did.
        /// </summary>
        public QCUCRAMReassignViewModel BuildReassign(int reservationId)
        {
            QCUCRAMReassignViewModel vm = new QCUCRAMReassignViewModel
            {
                ReservationID = reservationId
            };

            QCUCRAM_ReservationInfo info = _reservations.GetInfo(reservationId);
            if (info == null)
            {
                vm.Error = "Reservation #" + reservationId + " was not found.";
                return vm;
            }

            if (!QCUCRAM_ReservationRepository.CanTransition(info.Status, QCUCRAM_Status.Reassigned))
            {
                vm.Error = "Reservation #" + reservationId + " is " + info.Status +
                           " and can't be reassigned.";
                return vm;
            }

            if (info.SlotStart <= QCUCRAM_Clock.Now)
            {
                vm.Error = "Reservation #" + reservationId +
                           " has already started and can't be reassigned.";
                return vm;
            }

            vm.CurrentRoomName = info.RoomName;
            vm.CurrentSlotLabel = info.SlotLabel;
            vm.ReservationDate = info.Date;
            vm.Status = info.Status;

            QCUCRAM_ReservationListItem row = _reservations.GetByDate(info.Date)
                .FirstOrDefault(x => x.ReservationId == reservationId);

            int members = row == null ? 1 : row.NumberOfMembers;
            DateTime now = QCUCRAM_Clock.Now;

            // FillTargets(): slots outer, rooms inner, started slots and rooms that
            // do not fit the group skipped, and any held pair (including this
            // booking's own slot) left out.
            // FillTargets(): rooms were read ORDER BY RoomID (not by name).
            List<QCUCRAM_Room> rooms = _content.GetRooms(true).OrderBy(x => x.RoomID).ToList();

            foreach (QCUCRAM_TimeSlot slot in _content.GetTimeSlots(true))
            {
                if (info.Date.Date.Add(slot.StartTime) <= now) { continue; }

                string slotLabel = QCUCRAM_ReservationInfo.FormatSlot(slot.StartTime, slot.EndTime);

                foreach (QCUCRAM_Room room in rooms)
                {
                    if (members < room.MinCapacity || members > room.MaxCapacity) { continue; }

                    if (_reservations.IsSlotHeld(room.RoomID, info.Date, slot.TimeSlotID)) { continue; }

                    vm.Targets.Add(new QCUCRAMReassignTarget
                    {
                        Value = room.RoomID.ToString(Inv) + "|" + slot.TimeSlotID.ToString(Inv),
                        Label = slotLabel + "  |  " + room.RoomName
                    });
                }
            }

            if (vm.Targets.Count == 0)
            {
                vm.Error = "No other open room or time slot fits this booking on " +
                           info.Date.ToString("MMM d, yyyy", Inv) + ", so it can't be reassigned.";
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
                return RetryFailure("Please enter a reason before declining.");
            }
            if (why.Length > 250)
            {
                return RetryFailure("The reason must be 250 characters or fewer.");
            }

            QCUCRAM_ReservationInfo info = _reservations.GetInfo(reservationId);
            if (info == null)
            {
                return QCUCRAMOperationResult.Failure("Reservation #" + reservationId + " was not found.");
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
                return RetryFailure("Something went wrong while declining. Please try again.");
            }
        }

        public QCUCRAMOperationResult DoReassign(int reservationId, string target, string reason, int adminId)
        {
            string why = (reason ?? string.Empty).Trim();

            // btnDoReassign_Click: the room/time check came first, then the reason, and the
            // panel stayed open after either message.
            string[] parts = (target ?? string.Empty).Split('|');
            int roomId = 0, slotId = 0;
            if (string.IsNullOrWhiteSpace(target) || parts.Length != 2 ||
                !int.TryParse(parts[0], NumberStyles.Integer, Inv, out roomId) ||
                !int.TryParse(parts[1], NumberStyles.Integer, Inv, out slotId))
            {
                return RetryFailure("Please choose the new room and time.");
            }
            if (why.Length == 0)
            {
                return RetryFailure("Please enter a reason for the reassignment.");
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
                               info.SlotStart > QCUCRAM_Clock.Now &&
                               BuildReassign(reservationId).Error == null;

                return result;
            }
            catch (Exception)
            {
                return RetryFailure(
                    "Something went wrong while reassigning. Please try again.");
            }
        }

        // ==================================================================
        //  Check-in desk
        // ==================================================================

        /// <summary>Shown as "Grace expiring" when this many minutes (or fewer) are left.</summary>
        private const int ExpiringMinutes = 5;

        public QCUCRAMCheckInViewModel CheckInBoard(string search)
        {
            DateTime now = QCUCRAM_Clock.Now;
            DateTime today = now.Date;

            int grace = Math.Max(1, _content.GetSettingInt("GraceMinutes", 15));
            string q = (search ?? string.Empty).Trim();

            Dictionary<int, QCUCRAM_Student> map = LoadStudents();

            // Member names for today's bookings, one query for all rows (LoadMembers).
            Dictionary<int, List<string>> memberMap = new Dictionary<int, List<string>>();

            foreach (QCUCRAM_ReservationMember m in _reservations.GetMembersForDate(today))
            {
                string mi = m.MI;
                string memberName = m.LastName + ", " + m.FirstName +
                                    (string.IsNullOrWhiteSpace(mi) ? "" : " " + mi);

                List<string> list;
                if (!memberMap.TryGetValue(m.ReservationID, out list))
                {
                    list = new List<string>();
                    memberMap[m.ReservationID] = list;
                }

                list.Add(memberName);
            }

            QCUCRAMCheckInViewModel vm = new QCUCRAMCheckInViewModel
            {
                Now = now,
                GraceMinutes = grace,
                Search = q,
                NowText = "System time: <strong>" +
                          now.ToString("ddd, MMM d, yyyy h:mm:ss tt", Inv) +
                          "</strong> \u00b7 Grace period: <strong>" + grace + " minutes</strong>"
            };

            foreach (QCUCRAM_ReservationListItem row in _reservations.GetByDate(today))
            {
                if (row.Status != QCUCRAM_Status.Approved &&
                    row.Status != QCUCRAM_Status.Reassigned &&
                    row.Status != QCUCRAM_Status.CheckedIn)
                {
                    continue;
                }

                QCUCRAM_Student student;
                map.TryGetValue(row.StudentId, out student);

                // CheckIn.aspx.cs RequesterHtml: MI appended exactly as stored.
                string mi = student == null ? null : student.MI;
                string name = row.StudentName +
                              (string.IsNullOrWhiteSpace(mi) ? "" : " " + mi);

                // CheckIn.aspx.cs applied the search to the waiting grid only; the
                // "Checked in today" grid (BindCheckedIn) was never filtered.
                if (q.Length > 0 && row.Status != QCUCRAM_Status.CheckedIn && !Matches(q, row, name))
                {
                    continue;
                }

                DateTime start = row.ReservationDate.Date.Add(row.StartTime);
                DateTime closes = start.AddMinutes(grace);

                List<string> names;
                memberMap.TryGetValue(row.ReservationId, out names);

                QCUCRAMCheckInRow item = new QCUCRAMCheckInRow
                {
                    ReservationID = row.ReservationId,
                    Requester = name,
                    StudentNumber = row.StudentNumber,
                    RequesterType = student == null ? string.Empty : student.RequesterType,
                    Program = student == null ? string.Empty : student.Program,
                    RoomName = row.RoomName,
                    SlotLabel = row.SlotLabel,
                    ReservationDate = row.ReservationDate,
                    NumberOfMembers = row.NumberOfMembers,
                    Status = row.Status,
                    StatusCss = QCUCRAM_Status.CssClass(row.Status),
                    Members = names ?? new List<string>(),
                    SlotStart = start,
                    GraceEndsAt = closes,
                    CheckedInAt = row.CheckedInAt
                };

                if (row.Status == QCUCRAM_Status.CheckedIn)
                {
                    item.CheckedText = row.CheckedInAt.HasValue
                        ? row.CheckedInAt.Value.ToString("h:mm tt", Inv)
                        : string.Empty;

                    vm.CheckedIn.Add(item);
                    continue;
                }

                // State relative to the check-in window [start, start + grace].
                if (now < start)
                {
                    item.StateBadgeCss = "text-bg-secondary";
                    item.StateText = "Not yet open";
                    item.StateDetail = "Opens at " + start.ToString("h:mm tt", Inv);
                    item.CanCheckIn = false;
                }
                else if (now <= closes)
                {
                    int left = (int)Math.Ceiling((closes - now).TotalMinutes);
                    bool expiring = left <= ExpiringMinutes;

                    item.StateBadgeCss = expiring ? "text-bg-warning" : "text-bg-success";
                    item.StateText = expiring ? "Grace expiring" : "Open for check-in";
                    item.StateDetail = "Closes in " + left + " min (" + closes.ToString("h:mm tt", Inv) + ")";
                    item.MinutesLeft = left;
                    item.CanCheckIn = true;
                }
                else
                {
                    item.StateBadgeCss = "text-bg-danger";
                    item.StateText = "Past grace";
                    item.StateDetail = "Closed at " + closes.ToString("h:mm tt", Inv) + " - will be forfeited";
                    item.CanCheckIn = false;
                }

                item.BlockedReason = item.CanCheckIn ? string.Empty : item.StateDetail;

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

        /// <summary>
        /// The Restrictions screen. When <paramref name="liftId"/> is greater than
        /// zero the "Lift restriction" panel is rendered open for that student,
        /// which is what gvRestricted_RowCommand did before re-binding the grids.
        /// </summary>
        public QCUCRAMRestrictionsViewModel Restrictions(int liftId = 0)
        {
            DateTime now = QCUCRAM_Clock.Now;

            int threshold = Math.Max(1, _content.GetSettingInt("NoShowThreshold", 3));
            int days = _content.GetSettingInt("RestrictionDays", 7);   // Restrictions.aspx.cs BindAll(): no Math.Max

            QCUCRAMRestrictionsViewModel vm = new QCUCRAMRestrictionsViewModel
            {
                Threshold = threshold,
                RestrictionDays = days,
                PrintedAtText = now.ToString("MMMM d, yyyy h:mm tt", Inv)
            };

            vm.RuleText = "Rule: <strong>" + threshold + "</strong> forfeited booking(s) restrict a student " +
                          "from booking for <strong>" + days + "</strong> day(s). Restrictions end " +
                          "automatically; you can also lift one early.";

            // ---- currently restricted, soonest lift first ------------------
            // Restrictions.aspx.cs: ORDER BY CASE WHEN RestrictionLiftDate IS NULL
            // THEN 1 ELSE 0 END, RestrictionLiftDate
            List<QCUCRAM_Student> students = _students.Search();

            foreach (QCUCRAM_Student student in students
                .Where(s => s.IsRestricted)
                .OrderBy(s => s.RestrictionLiftDate.HasValue ? 0 : 1)
                .ThenBy(s => s.RestrictionLiftDate ?? DateTime.MaxValue)
                .ThenBy(s => s.LastName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.FirstName, StringComparer.OrdinalIgnoreCase))
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
                    StudentHtml = StudentHtml(student),
                    UntilText = until,
                    RemainingText = remaining
                });
            }

            // ---- one strike away -------------------------------------------
            // "One strike away" only makes sense when the threshold is 2 or more;
            // target -1 matches nobody, exactly as BindWarning did.
            int oneAway = threshold - 1;
            int target = oneAway >= 1 ? oneAway : -1;

            Dictionary<int, DateTime> lastForfeited = _reservations.GetLastForfeitedDates();

            foreach (QCUCRAM_Student student in students
                .Where(s => !s.IsRestricted && s.NoShowCount == target)
                .OrderBy(s => s.LastName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.FirstName, StringComparer.OrdinalIgnoreCase))
            {
                DateTime forfeited;

                vm.Warnings.Add(new QCUCRAMStrikeWarningRow
                {
                    StudentID = student.StudentID,
                    FullName = FullName(student),
                    StudentNumber = student.StudentNumber,
                    Program = student.Program,
                    RequesterType = student.RequesterType,
                    NoShowCount = student.NoShowCount,
                    StrikesRemaining = threshold - student.NoShowCount,
                    StudentHtml = StudentHtml(student),
                    StrikesText = student.NoShowCount + " of " + threshold,
                    LastText = lastForfeited.TryGetValue(student.StudentID, out forfeited)
                        ? forfeited.ToString("MMM d, yyyy", Inv)
                        : "-"
                });
            }

            // ---- the lift panel --------------------------------------------
            if (liftId > 0)
            {
                QCUCRAM_Student student = _students.GetById(liftId);

                if (student == null || !student.IsRestricted)
                {
                    // gvRestricted_RowCommand: stale row, close the panel and say so.
                    vm.Message = "That student is no longer restricted. The list has been refreshed.";
                    vm.MessageIsError = true;
                }
                else
                {
                    string until = student.RestrictionLiftDate.HasValue
                        ? "until " + student.RestrictionLiftDate.Value.ToString("MMM d, yyyy h:mm tt", Inv)
                        : "until lifted manually";

                    vm.LiftId = liftId;
                    vm.LiftOpen = true;
                    vm.LiftInfo = "<p class=\"mb-0\">You are about to lift the restriction of <strong>" +
                                  HttpUtility.HtmlEncode(StudentName(student)) + "</strong> (" +
                                  HttpUtility.HtmlEncode(student.StudentNumber ?? string.Empty) + "), who has " +
                                  student.NoShowCount + " strike(s) and is restricted " +
                                  HttpUtility.HtmlEncode(until) + ".</p>";
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
                return QCUCRAMOperationResult.Failure("That student was no longer restricted.");
            }
            if (!student.IsRestricted)
            {
                return QCUCRAMOperationResult.Failure("That student was no longer restricted.");
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

                    // Restrictions.aspx.cs named the assistant who lifted it.
                    QCUCRAM_AdminUser admin = _admins.GetById(adminId);
                    string who = admin == null || string.IsNullOrWhiteSpace(admin.FullName)
                        ? "a library assistant"
                        : admin.FullName.Trim();

                    string message = "Your booking restriction was lifted by " + who +
                                     ". Your strike count was reset and you can reserve rooms again." +
                                     (trimmed.Length == 0 ? string.Empty : " Note: " + trimmed);

                    if (message.Length > 400) { message = message.Substring(0, 397) + "..."; }

                    QCUCRAM_NotificationRepository.Insert(con, tx, studentId, null, message, QCUCRAM_Clock.Now);
                });

                if (affected != 1)
                {
                    return QCUCRAMOperationResult.Failure("That student was no longer restricted.");
                }

                return QCUCRAMOperationResult.Success(
                    "Restriction lifted. The student has been notified and their strikes were reset.");
            }
            catch (Exception)
            {
                // The original kept the panel open on an exception.
                QCUCRAMOperationResult failure =
                    QCUCRAMOperationResult.Failure("Something went wrong while lifting the restriction. Please try again.");
                failure.Retry = true;
                return failure;
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

            int pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;
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
                // Records.aspx.cs labelled the CheckedIn row "Checked In".
                string label = status == QCUCRAM_Status.CheckedIn ? "Checked In" : status;
                vm.StatusOptions.Add(new KeyValuePair<string, string>(status, label));
            }

            vm.RoomOptions.Add(new KeyValuePair<string, string>(string.Empty, "All rooms"));
            foreach (QCUCRAM_Room room in _content.GetRooms())
            {
                // Records.aspx.cs marked retired rooms so they are not mistaken for open ones.
                string label = room.RoomName + (room.IsActive ? string.Empty : " (inactive)");
                vm.RoomOptions.Add(new KeyValuePair<string, string>(
                    room.RoomID.ToString(Inv), label));
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

            // Phase 16A: btnExportCsv_Click wrote no file when nothing matched. Null tells
            // the controller to show "There are no reservations to export for these filters."
            if (rows.Count == 0) { return null; }

            StringBuilder sb = new StringBuilder();

            // Same heading, same order, same CRLF line ending as Records.aspx.cs.
            sb.Append("Reservation No.,Date,Start Time,End Time,Room,ID / Student No.,Requester,Requester Type,")
              .Append("Program,Email,Members,Purpose,Status,Submitted,Checked In\r\n");

            foreach (QCUCRAMRecordsRow row in rows)
            {
                QCUCRAM_Student owner = FindStudent(map, row.StudentNumber);

                // Phase 31: the two time columns come from the real slot boundaries.
                //
                // This used to split SlotLabel on " - ", which is a display string: it
                // would quietly produce a blank End Time if the separator ever changed,
                // and it re-parsed text the database had already given us as TimeSpans.
                string start = FormatTimeOfDay(row.StartTime);
                string end = FormatTimeOfDay(row.EndTime);

                string status = row.Status == QCUCRAM_Status.CheckedIn ? "Checked In" : row.Status;

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
                  .Append(Csv(status)).Append(',')
                  .Append(Csv(row.CreatedAt.ToString("yyyy-MM-dd HH:mm", Inv))).Append(',')
                  .Append(Csv(row.CheckedInAt.HasValue
                                  ? row.CheckedInAt.Value.ToString("yyyy-MM-dd HH:mm", Inv)
                                  : string.Empty))
                  .Append("\r\n");
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
                NoShowCount = student == null ? 0 : student.NoShowCount,
                RoomName = info.RoomName,
                ReservationDate = info.Date,
                SlotLabel = info.SlotLabel,
                SlotStart = info.SlotStart,
                SlotEnd = info.SlotEnd,
                NumberOfMembers = row == null ? 0 : row.NumberOfMembers,
                Purpose = row == null ? string.Empty : row.Purpose,
                CreatedAt = row == null ? info.Date : row.CreatedAt,
                CheckedInAt = row == null ? (DateTime?)null : row.CheckedInAt
            };

            // ---- members ----
            // ReservationMembers already contains the requester (row 1 is prefilled on
            // the Reserve page), so adding the student again would duplicate them.
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
                    ActorName = HistoryActor(item),
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

        /// <summary>
        /// Records.aspx.cs ActorText: "Admin: FullName" / "Student: Last, First" /
        /// "System", with "(account removed)" when the id no longer resolves.
        /// </summary>
        private string HistoryActor(QCUCRAM_HistoryItem item)
        {
            bool isAdmin = string.Equals(item.ChangedByRole, QCUCRAM_ReservationRepository.ActorAdmin,
                                         StringComparison.OrdinalIgnoreCase);
            bool isStudent = string.Equals(item.ChangedByRole, QCUCRAM_ReservationRepository.ActorStudent,
                                           StringComparison.OrdinalIgnoreCase);

            // Anything else is shown as "System" by the view.
            if (!isAdmin && !isStudent) { return string.Empty; }

            // Phase 16A: the source CASE looked the id up even when it was NULL, which
            // gave "(account removed)". Student names are "Last, First" with no MI.
            if (!item.ChangedBy.HasValue) { return "(account removed)"; }

            int id = item.ChangedBy.Value;

            if (isAdmin)
            {
                QCUCRAM_AdminUser admin = _admins.GetById(id);
                return admin == null ? "(account removed)" : admin.FullName;
            }

            QCUCRAM_Student student = _students.GetById(id);
            return student == null
                ? "(account removed)"
                : (student.LastName ?? string.Empty) + ", " + (student.FirstName ?? string.Empty);
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
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                SlotLabel = x.SlotLabel,
                RoomName = x.RoomName,
                Requester = DisplayName(map, x.StudentId),
                SortLastName = SortLast(map, x.StudentId),
                SortFirstName = SortFirst(map, x.StudentId),
                StudentNumber = x.StudentNumber,
                Program = ProgramOf(map, x.StudentId),
                NumberOfMembers = x.NumberOfMembers,
                Purpose = x.Purpose,
                Status = x.Status,
                StatusCss = QCUCRAM_Status.CssClass(x.Status),
                CreatedAt = x.CreatedAt,
                CheckedInAt = x.CheckedInAt
            }).ToList();

            bool asc = string.Equals(filter.Dir, "asc", StringComparison.OrdinalIgnoreCase);
            string sort = QCUCRAMRecordsFilter.IsSortKey(filter.Sort) ? filter.Sort : "ReservationDate";

            // Phase 31: sort the way Records.aspx.cs sorted.
            //
            // Two things were wrong before:
            //   * "StartTime" compared SlotLabel, a 12-hour string, so "10:00 AM" sorted
            //     before "9:00 AM". The source ordered on ts.StartTime, a TimeSpan.
            //   * every order the source built ended with ", r.ReservationDate DESC,
            //     r.ReservationID DESC" so equal keys had a stable, total order. Without
            //     that tail the paged grid could repeat or skip rows between pages.
            //
            // The tail is always descending, exactly as the source had it, regardless of
            // the direction of the leading column.
            switch (sort)
            {
                case "ReservationID":
                    rows = asc ? rows.OrderBy(r => r.ReservationID).ToList()
                               : rows.OrderByDescending(r => r.ReservationID).ToList();
                    break;
                case "StartTime":
                    rows = asc
                        ? rows.OrderBy(r => r.StartTime)
                              .ThenByDescending(r => r.ReservationDate)
                              .ThenByDescending(r => r.ReservationID).ToList()
                        : rows.OrderByDescending(r => r.StartTime)
                              .ThenByDescending(r => r.ReservationDate)
                              .ThenByDescending(r => r.ReservationID).ToList();
                    break;
                case "RoomName":
                    rows = asc
                        ? rows.OrderBy(r => r.RoomName, StringComparer.OrdinalIgnoreCase)
                              .ThenByDescending(r => r.ReservationDate)
                              .ThenByDescending(r => r.ReservationID).ToList()
                        : rows.OrderByDescending(r => r.RoomName, StringComparer.OrdinalIgnoreCase)
                              .ThenByDescending(r => r.ReservationDate)
                              .ThenByDescending(r => r.ReservationID).ToList();
                    break;
                case "Requester":
                    // Phase 16A: s.LastName d, s.FirstName d, then the DESC tail.
                    rows = asc
                        ? rows.OrderBy(r => r.SortLastName, StringComparer.OrdinalIgnoreCase)
                              .ThenBy(r => r.SortFirstName, StringComparer.OrdinalIgnoreCase)
                              .ThenByDescending(r => r.ReservationDate)
                              .ThenByDescending(r => r.ReservationID).ToList()
                        : rows.OrderByDescending(r => r.SortLastName, StringComparer.OrdinalIgnoreCase)
                              .ThenByDescending(r => r.SortFirstName, StringComparer.OrdinalIgnoreCase)
                              .ThenByDescending(r => r.ReservationDate)
                              .ThenByDescending(r => r.ReservationID).ToList();
                    break;
                case "Program":
                    rows = asc
                        ? rows.OrderBy(r => r.Program, StringComparer.OrdinalIgnoreCase)
                              .ThenByDescending(r => r.ReservationDate)
                              .ThenByDescending(r => r.ReservationID).ToList()
                        : rows.OrderByDescending(r => r.Program, StringComparer.OrdinalIgnoreCase)
                              .ThenByDescending(r => r.ReservationDate)
                              .ThenByDescending(r => r.ReservationID).ToList();
                    break;
                case "NumberOfMembers":
                    rows = asc
                        ? rows.OrderBy(r => r.NumberOfMembers)
                              .ThenByDescending(r => r.ReservationDate)
                              .ThenByDescending(r => r.ReservationID).ToList()
                        : rows.OrderByDescending(r => r.NumberOfMembers)
                              .ThenByDescending(r => r.ReservationDate)
                              .ThenByDescending(r => r.ReservationID).ToList();
                    break;
                case "Status":
                    rows = asc
                        ? rows.OrderBy(r => r.Status, StringComparer.OrdinalIgnoreCase)
                              .ThenByDescending(r => r.ReservationDate)
                              .ThenByDescending(r => r.ReservationID).ToList()
                        : rows.OrderByDescending(r => r.Status, StringComparer.OrdinalIgnoreCase)
                              .ThenByDescending(r => r.ReservationDate)
                              .ThenByDescending(r => r.ReservationID).ToList();
                    break;
                default:
                    // Source default: r.ReservationDate, ts.StartTime, r.ReservationID.
                    rows = asc
                        ? rows.OrderBy(r => r.ReservationDate)
                              .ThenBy(r => r.StartTime)
                              .ThenBy(r => r.ReservationID).ToList()
                        : rows.OrderByDescending(r => r.ReservationDate)
                              .ThenByDescending(r => r.StartTime)
                              .ThenByDescending(r => r.ReservationID).ToList();
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
            sb.Append("&q=").Append(HttpUtility.UrlEncode(filter.Search ?? string.Empty));
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

        private static string SortLast(Dictionary<int, QCUCRAM_Student> map, int studentId)
        {
            QCUCRAM_Student student;
            return map.TryGetValue(studentId, out student) ? (student.LastName ?? string.Empty) : "Unknown requester";
        }

        private static string SortFirst(Dictionary<int, QCUCRAM_Student> map, int studentId)
        {
            QCUCRAM_Student student;
            return map.TryGetValue(studentId, out student) ? (student.FirstName ?? string.Empty) : string.Empty;
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

        /// <summary>
        /// Restrictions.aspx.cs: LastName + ", " + FirstName + (MI ? " " + mi : "").
        /// The MI is appended exactly as stored - no trim, no trailing period.
        /// </summary>
        private static string StudentName(QCUCRAM_Student student)
        {
            string name = (student.LastName ?? string.Empty) + ", " + (student.FirstName ?? string.Empty);

            return string.IsNullOrWhiteSpace(student.MI) ? name : name + " " + student.MI;
        }

        /// <summary>
        /// Restrictions.aspx.cs StudentHtml: the bold name plus a small line with
        /// "Number | RequesterType | Program".
        /// </summary>
        private static string StudentHtml(QCUCRAM_Student student)
        {
            return "<strong>" + HttpUtility.HtmlEncode(StudentName(student)) + "</strong><br /><small>" +
                   HttpUtility.HtmlEncode((student.StudentNumber ?? string.Empty) + " | " +
                                          (student.RequesterType ?? string.Empty) + " | " +
                                          (student.Program ?? string.Empty)) + "</small>";
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

        /// <summary>
        /// CheckIn.aspx.cs search: reservation # matched exactly, student number and
        /// last name matched with LIKE '%q%'. Nothing else was searched.
        /// </summary>
        private static bool Matches(string q, QCUCRAM_ReservationListItem row, string name)
        {
            if (string.Equals(row.ReservationId.ToString(Inv), q, StringComparison.OrdinalIgnoreCase)) { return true; }
            if ((row.StudentNumber ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }

            string lastName = (row.StudentName ?? string.Empty).Split(',')[0];
            if (lastName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }

            return false;
        }

        private static string RemainingText(TimeSpan span)
        {
            if (span.TotalHours < 1) { return Math.Max(1, (int)Math.Ceiling(span.TotalMinutes)) + " min"; }
            if (span.TotalDays < 1) { return (int)span.TotalHours + " hr " + span.Minutes + " min"; }

            return (int)span.TotalDays + " day(s) " + span.Hours + " hr";
        }

        /// <summary>
        /// One TimeSpan as "9:00 AM" - the same text QCUCRAM_ReservationInfo.FormatSlot
        /// puts on either side of its " - ". Invariant culture so the CSV reads the same
        /// on a machine set to a 24-hour locale.
        /// </summary>
        private static string FormatTimeOfDay(TimeSpan value)
        {
            return DateTime.MinValue.Add(value).ToString("h:mm tt", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Quotes one CSV cell exactly like Records.aspx.cs: newlines become spaces,
        /// a leading = + - @ is prefixed with an apostrophe so Excel does not run it
        /// as a formula, and every cell is wrapped in double quotes.
        /// </summary>
        private static string Csv(string value)
        {
            string text = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ");

            if (text.Length > 0 && "=+-@".IndexOf(text[0]) >= 0) { text = "'" + text; }

            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}
