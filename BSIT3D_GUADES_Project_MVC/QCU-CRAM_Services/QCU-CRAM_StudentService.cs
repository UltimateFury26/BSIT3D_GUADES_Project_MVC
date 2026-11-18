using System;
using System.Collections.Generic;
using System.Linq;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Services
{
    /// <summary>
    /// Phase 06 - everything the student pages need, in one place.
    ///
    /// The Web Forms code-behind of each page read its own data with inline SQL. Those
    /// reads are already repositories in this project, so this service is the thin layer
    /// that turns repository rows into the seven view models:
    ///
    ///   Dashboard      - restriction banner, upcoming, recent, glance, calendar
    ///   Reserve        - requester block, rules, drop-downs, five blank member rows
    ///   My Reservations- filter, paging, CanCancel
    ///   Notifications  - filter, paging, unread count
    ///   Profile        - editable fields, standing, reservation-history summary
    ///
    /// No SQL lives here: every method delegates to a repository, so the Phase 02 data
    /// layer stays the only place that talks to the database.
    /// </summary>
    public class QCUCRAMStudentService
    {
        private readonly QCUCRAM_StudentRepository _students;
        private readonly QCUCRAM_ReservationRepository _reservations;
        private readonly QCUCRAM_ContentRepository _content;
        private readonly QCUCRAM_NotificationRepository _notifications;

        public QCUCRAMStudentService()
            : this(new QCUCRAM_StudentRepository(), new QCUCRAM_ReservationRepository(),
                   new QCUCRAM_ContentRepository(), new QCUCRAM_NotificationRepository())
        {
        }

        public QCUCRAMStudentService(QCUCRAM_StudentRepository students,
                                     QCUCRAM_ReservationRepository reservations,
                                     QCUCRAM_ContentRepository content,
                                     QCUCRAM_NotificationRepository notifications)
        {
            if (students == null) { throw new ArgumentNullException("students"); }
            if (reservations == null) { throw new ArgumentNullException("reservations"); }
            if (content == null) { throw new ArgumentNullException("content"); }
            if (notifications == null) { throw new ArgumentNullException("notifications"); }

            _students = students;
            _reservations = reservations;
            _content = content;
            _notifications = notifications;
        }

        // ==================================================================
        //  Dashboard
        // ==================================================================

        /// <param name="studentId">The signed-in student.</param>
        /// <param name="month">
        /// Any date inside the month the calendar should show; null means the current month.
        /// The dashboard's &amp;laquo; / &amp;raquo; links pass this so the calendar can be paged
        /// the way the original &lt;asp:Calendar&gt; allowed.
        /// </param>
        public QCUCRAMStudentDashboardViewModel BuildDashboard(int studentId, DateTime? month = null)
        {
            QCUCRAM_Student student = _students.GetById(studentId);
            DateTime now = QCUCRAM_Clock.Now;
            DateTime calendarMonth = (month ?? QCUCRAM_Clock.Today);
            int ahead = _content.GetSettingInt("AdvanceBookingDays", 1);
            DateTime bookingDay = QCUCRAM_Clock.Today.AddDays(ahead);

            QCUCRAMStudentDashboardViewModel vm = new QCUCRAMStudentDashboardViewModel
            {
                Student = student,
                BookingDay = bookingDay,
                AdvanceBookingDays = ahead,
                NoShowThreshold = Math.Max(1, _content.GetSettingInt("NoShowThreshold", 3)),
                UnreadCount = _notifications.CountUnread(studentId)
            };

            if (student != null)
            {
                // ShowRestrictionBanner() hid the banner when the cooldown had already
                // passed (the sweep was about to lift it), so do the same here.
                bool expired = student.RestrictionLiftDate.HasValue &&
                               student.RestrictionLiftDate.Value <= now;

                vm.IsRestricted = student.IsRestricted && !expired;
                vm.NoShowCount = student.NoShowCount;
                vm.RestrictionLiftDate = student.RestrictionLiftDate;
            }

            List<QCUCRAM_ReservationListItem> all = _reservations.GetForStudent(studentId);
            vm.TotalReservations = all.Count;

            // Upcoming: the original ShowUpcoming() accepted only the four "live" statuses
            // (Pending / Approved / Reassigned / CheckedIn) and required the slot not to
            // have ended yet. Note this is narrower than QCUCRAM_Status.IsActive, which
            // also lets Completed through - a completed booking is not "upcoming".
            vm.Upcoming = all
                .Where(x => IsLive(x.Status) && SlotEnd(x) > now)
                .OrderBy(SlotStart)
                .ToList();

            // Recent: the five newest requests, whatever their status.
            vm.Recent = all
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.ReservationId)
                .Take(5)
                .ToList();

            // Availability glance for the booking day.
            // The original SQL counted only reservations whose TimeSlot is active
            // (JOIN TimeSlots t ... AND t.IsActive = 1), so a booking sitting on a
            // disabled slot must not inflate the "held" number here either.
            List<QCUCRAM_TimeSlot> slots = _content.GetTimeSlots(true);
            HashSet<int> activeSlotIds = new HashSet<int>();
            foreach (QCUCRAM_TimeSlot slot in slots) { activeSlotIds.Add(slot.TimeSlotID); }

            Dictionary<int, int> heldPerRoom = new Dictionary<int, int>();

            foreach (QCUCRAM_AvailabilityCell cell in _content.GetAvailability(bookingDay))
            {
                if (!activeSlotIds.Contains(cell.TimeSlotId)) { continue; }

                int count;
                heldPerRoom.TryGetValue(cell.RoomId, out count);
                heldPerRoom[cell.RoomId] = count + 1;
            }

            foreach (QCUCRAM_Room room in _content.GetRooms(true))
            {
                int held;
                heldPerRoom.TryGetValue(room.RoomID, out held);

                vm.Glance.Add(new QCUCRAMAvailabilityGlanceRow
                {
                    RoomId = room.RoomID,
                    RoomName = room.RoomName,
                    TotalSlots = slots.Count,
                    Held = held
                });
            }

            vm.Calendar = BuildCalendar(all, new DateTime(calendarMonth.Year, calendarMonth.Month, 1), now);
            return vm;
        }

        /// <summary>
        /// The month grid behind Views/Shared/_QCU-CRAM_Calendar.cshtml.
        /// </summary>
        public QCUCRAMCalendarViewModel BuildCalendar(IEnumerable<QCUCRAM_ReservationListItem> items,
                                                      DateTime month, DateTime now)
        {
            DateTime first = new DateTime(month.Year, month.Month, 1);

            QCUCRAMCalendarViewModel vm = new QCUCRAMCalendarViewModel { Month = first };

            // Group the bookings by day once, then rank each day.
            Dictionary<DateTime, List<QCUCRAM_ReservationListItem>> byDay =
                new Dictionary<DateTime, List<QCUCRAM_ReservationListItem>>();

            if (items != null)
            {
                foreach (QCUCRAM_ReservationListItem item in items)
                {
                    DateTime key = item.ReservationDate.Date;
                    List<QCUCRAM_ReservationListItem> bucket;
                    if (!byDay.TryGetValue(key, out bucket))
                    {
                        bucket = new List<QCUCRAM_ReservationListItem>();
                        byDay[key] = bucket;
                    }
                    bucket.Add(item);
                }
            }

            // <asp:Calendar> started the week on Sunday and painted the whole weeks that
            // the month touches, including the greyed days that belong to the neighbouring
            // months. DayRender fired for those days too, so they get a rank as well.
            DateTime gridStart = first.AddDays(-(int)first.DayOfWeek);
            int daysInMonth = DateTime.DaysInMonth(first.Year, first.Month);
            int cells = (((int)first.DayOfWeek + daysInMonth + 6) / 7) * 7;

            for (int i = 0; i < cells; i++)
            {
                DateTime date = gridStart.AddDays(i);

                QCUCRAMCalendarDay cell = new QCUCRAMCalendarDay
                {
                    Date = date,
                    InMonth = date.Month == first.Month && date.Year == first.Year,
                    IsToday = date == now.Date
                };

                List<QCUCRAM_ReservationListItem> bucket;
                if (byDay.TryGetValue(date, out bucket) && bucket.Count > 0)
                {
                    cell.BookingCount = bucket.Count;
                    cell.Rank = Rank(bucket, now);
                    cell.Tip = Tip(cell.Rank, bucket);
                }

                vm.Days.Add(cell);
            }

            return vm;
        }

        /// <summary>
        /// The original BuildCalendarData rank, exactly:
        ///   CheckedIn                                        -> 3 (ongoing)
        ///   Pending / Approved / Reassigned                  -> 2 (reserved)
        ///   Completed                                        -> 1 (done)
        ///   Declined / Cancelled / Forfeited                 -> no colour
        /// The highest rank on a day wins. The Web Forms page did NOT look at the
        /// clock here, so neither does this.
        /// </summary>
        private static int Rank(List<QCUCRAM_ReservationListItem> bucket, DateTime now)
        {
            int rank = 0;

            foreach (QCUCRAM_ReservationListItem item in bucket)
            {
                if (item.Status == QCUCRAM_Status.CheckedIn)
                {
                    return 3;   // ongoing wins outright
                }

                if (item.Status == QCUCRAM_Status.Pending ||
                    item.Status == QCUCRAM_Status.Approved ||
                    item.Status == QCUCRAM_Status.Reassigned)
                {
                    if (rank < 2) { rank = 2; }
                }
                else if (item.Status == QCUCRAM_Status.Completed)
                {
                    if (rank < 1) { rank = 1; }
                }
            }

            return rank;
        }

        /// <summary>
        /// The original cell tooltip: the rank label, then one line per booking -
        ///   "Reserved: #12 Room A, 8:00 AM - 9:00 AM (Approved); #13 ...".
        /// </summary>
        private static string Tip(int rank, List<QCUCRAM_ReservationListItem> bucket)
        {
            string label;
            switch (rank)
            {
                case 3: label = "Ongoing"; break;
                case 2: label = "Reserved"; break;
                case 1: label = "Done"; break;
                default: return null;
            }

            List<string> lines = new List<string>();

            foreach (QCUCRAM_ReservationListItem item in bucket)
            {
                // The original only appended a tip while it was assigning a rank, so a
                // declined / cancelled / forfeited booking never appears in the tooltip.
                if (!IsRanked(item.Status)) { continue; }

                string status = item.Status == QCUCRAM_Status.CheckedIn ? "Checked In" : item.Status;

                lines.Add("#" + item.ReservationId + " " + item.RoomName + ", " +
                          item.SlotLabel + " (" + status + ")");
            }

            if (lines.Count == 0) { return null; }

            return label + ": " + string.Join("; ", lines.ToArray());
        }

        /// <summary>True for the statuses ShowUpcoming() treated as still upcoming.</summary>
        private static bool IsLive(string status)
        {
            return status == QCUCRAM_Status.Pending
                || status == QCUCRAM_Status.Approved
                || status == QCUCRAM_Status.Reassigned
                || status == QCUCRAM_Status.CheckedIn;
        }

        /// <summary>True for the statuses BuildCalendarData gave a colour to.</summary>
        private static bool IsRanked(string status)
        {
            return status == QCUCRAM_Status.CheckedIn
                || status == QCUCRAM_Status.Pending
                || status == QCUCRAM_Status.Approved
                || status == QCUCRAM_Status.Reassigned
                || status == QCUCRAM_Status.Completed;
        }

        private static DateTime SlotStart(QCUCRAM_ReservationListItem item)
        {
            return item.ReservationDate.Date.Add(item.StartTime);
        }

        private static DateTime SlotEnd(QCUCRAM_ReservationListItem item)
        {
            return item.ReservationDate.Date.Add(item.EndTime);
        }

        // ==================================================================
        //  Reserve
        // ==================================================================

        public QCUCRAMReserveViewModel BuildReserve(int studentId, int? roomId, int? slotId, DateTime? date)
        {
            DateTime bookingDay = QCUCRAM_Clock.Today.AddDays(_content.GetSettingInt("AdvanceBookingDays", 1));
            QCUCRAM_Student requester = _students.GetById(studentId);

            QCUCRAMReserveViewModel vm = new QCUCRAMReserveViewModel
            {
                Requester = requester,
                BookingDay = bookingDay,
                Date = bookingDay,
                RoomId = roomId.HasValue ? roomId.Value : 0,
                TimeSlotId = slotId.HasValue ? slotId.Value : 0,
                Rooms = _content.GetRooms(true),
                TimeSlots = _content.GetTimeSlots(true),
                RulesHtml = _content.BuildRulesHtml(),
                NumberOfMembers = 5
            };

            if (requester != null)
            {
                // IsRestrictedNow(): the flag only counts while the cooldown is still running.
                bool expired = requester.RestrictionLiftDate.HasValue &&
                               requester.RestrictionLiftDate.Value <= QCUCRAM_Clock.Now;

                vm.IsRestricted = requester.IsRestricted && !expired;
                vm.NoShowCount = requester.NoShowCount;
                vm.RestrictionLiftDate = requester.RestrictionLiftDate;
            }

            // The original opened with five member rows and copied the requester into row 1.
            for (int i = 0; i < vm.MinMembers; i++)
            {
                vm.Members.Add(new QCUCRAM_MemberInput());
            }

            if (requester != null && vm.Members.Count > 0)
            {
                vm.Members[0].LastName = requester.LastName;
                vm.Members[0].FirstName = requester.FirstName;
                vm.Members[0].MI = requester.MI;
            }

            return vm;
        }

        /// <summary>
        /// Runs every booking rule and saves when they all pass. The repository collects
        /// the errors, so the page can show them together.
        /// </summary>
        public QCUCRAM_CreateResult Create(QCUCRAM_CreateRequest request)
        {
            return _reservations.ValidateAndCreate(request);
        }

        // ==================================================================
        //  My Reservations
        // ==================================================================

        public QCUCRAMMyReservationsViewModel GetMyReservations(int studentId, string status,
                                                                int page, int pageSize,
                                                                Func<int, string> urlFor,
                                                                int? createdId)
        {
            List<QCUCRAM_ReservationListItem> all = _reservations.GetForStudent(studentId);

            IEnumerable<QCUCRAM_ReservationListItem> filtered = all;

            if (!string.IsNullOrWhiteSpace(status) &&
                Array.IndexOf(QCUCRAM_Status.All, status.Trim()) >= 0)
            {
                string wanted = status.Trim();
                filtered = all.Where(x => x.Status == wanted);
            }

            List<QCUCRAM_ReservationListItem> list = filtered.ToList();
            DateTime now = QCUCRAM_Clock.Now;

            int total = list.Count;
            page = QCUCRAMPagerViewModel.ClampPage(page, pageSize, total);

            QCUCRAMMyReservationsViewModel vm = new QCUCRAMMyReservationsViewModel
            {
                StatusFilter = status,
                CreatedId = createdId,
                TotalItems = total,
                Pager = new QCUCRAMPagerViewModel
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = total,
                    UrlFor = urlFor ?? (p => "?page=" + p)
                }
            };

            foreach (QCUCRAM_ReservationListItem item in list.Skip((page - 1) * pageSize).Take(pageSize))
            {
                bool cancellable =
                    (item.Status == QCUCRAM_Status.Pending ||
                     item.Status == QCUCRAM_Status.Approved ||
                     item.Status == QCUCRAM_Status.Reassigned) &&
                    SlotStart(item) > now;

                vm.Items.Add(new QCUCRAMReservationRow { Item = item, CanCancel = cancellable });
            }

            return vm;
        }

        /// <summary>
        /// Cancels one of the student's own bookings. Returns false and fills
        /// <paramref name="error"/> when the booking cannot be cancelled.
        /// </summary>
        public bool Cancel(int reservationId, int studentId, out string error)
        {
            error = null;

            QCUCRAM_ReservationInfo info = _reservations.GetInfo(reservationId);

            if (info == null)
            {
                error = "That reservation no longer exists.";
                return false;
            }

            if (info.StudentId != studentId)
            {
                error = "That reservation does not belong to you.";
                return false;
            }

            if (info.Status != QCUCRAM_Status.Pending &&
                info.Status != QCUCRAM_Status.Approved &&
                info.Status != QCUCRAM_Status.Reassigned)
            {
                error = "Only a pending or approved booking can be cancelled. This one is " + info.Status + ".";
                return false;
            }

            if (info.SlotStart <= QCUCRAM_Clock.Now)
            {
                error = "This slot has already started, so it can no longer be cancelled.";
                return false;
            }

            try
            {
                _reservations.ChangeStatus(
                    reservationId, QCUCRAM_Status.Cancelled,
                    QCUCRAM_ReservationRepository.ActorStudent, studentId,
                    "Cancelled by the requester.");

                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // ==================================================================
        //  Notifications
        // ==================================================================

        public QCUCRAMNotificationsViewModel GetNotifications(int studentId, bool unreadOnly,
                                                              int page, int pageSize,
                                                              Func<int, string> urlFor)
        {
            List<QCUCRAM_NotificationItem> all = _notifications.GetForStudent(studentId, unreadOnly);

            int total = all.Count;
            page = QCUCRAMPagerViewModel.ClampPage(page, pageSize, total);

            QCUCRAMNotificationsViewModel vm = new QCUCRAMNotificationsViewModel
            {
                UnreadOnly = unreadOnly,
                UnreadCount = _notifications.CountUnread(studentId),
                TotalCount = _notifications.CountAll(studentId),
                TotalItems = total,
                Pager = new QCUCRAMPagerViewModel
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = total,
                    UrlFor = urlFor ?? (p => "?page=" + p)
                }
            };

            foreach (QCUCRAM_NotificationItem item in all.Skip((page - 1) * pageSize).Take(pageSize))
            {
                vm.Items.Add(item);
            }

            return vm;
        }

        public bool MarkRead(int notificationId, int studentId)
        {
            return _notifications.MarkRead(notificationId, studentId) > 0;
        }

        public int MarkAllRead(int studentId)
        {
            return _notifications.MarkAllRead(studentId);
        }

        // ==================================================================
        //  Profile
        // ==================================================================

        public QCUCRAMProfileViewModel GetProfile(int studentId)
        {
            QCUCRAM_Student student = _students.GetById(studentId);
            if (student == null) { return null; }

            QCUCRAMProfileViewModel vm = new QCUCRAMProfileViewModel
            {
                StudentID = student.StudentID,
                StudentNumber = student.StudentNumber,
                RequesterType = student.RequesterType,
                LastName = student.LastName,
                FirstName = student.FirstName,
                MI = student.MI,
                Email = student.Email,
                Program = student.Program,
                // ShowStanding() hid a restriction whose cooldown had already passed.
                IsRestricted = student.IsRestricted &&
                               !(student.RestrictionLiftDate.HasValue &&
                                 student.RestrictionLiftDate.Value <= QCUCRAM_Clock.Now),
                NoShowCount = student.NoShowCount,
                RestrictionLiftDate = student.RestrictionLiftDate,
                NoShowThreshold = Math.Max(1, _content.GetSettingInt("NoShowThreshold", 3)),
                Stats = new QCUCRAMReservationStats()
            };

            foreach (QCUCRAM_ReservationListItem item in _reservations.GetForStudent(studentId))
            {
                vm.Stats.Total++;

                switch (item.Status)
                {
                    case QCUCRAM_Status.Pending: vm.Stats.Pending++; break;
                    case QCUCRAM_Status.Approved: vm.Stats.Approved++; break;
                    case QCUCRAM_Status.Reassigned: vm.Stats.Approved++; break;
                    case QCUCRAM_Status.CheckedIn: vm.Stats.CheckedIn++; break;
                    case QCUCRAM_Status.Completed: vm.Stats.Completed++; break;
                    case QCUCRAM_Status.Forfeited: vm.Stats.Forfeited++; break;
                    case QCUCRAM_Status.Cancelled: vm.Stats.Cancelled++; break;
                    case QCUCRAM_Status.Declined: vm.Stats.Declined++; break;
                }
            }

            return vm;
        }

        /// <summary>0 = OK, 1 = e-mail taken, 2 = not found, 3 = invalid input.</summary>
        public int UpdateProfile(int studentId, string lastName, string firstName, string mi,
                                 string email, string program)
        {
            return _students.UpdateProfile(studentId, lastName, firstName, mi, email, program);
        }

        /// <summary>0 = OK, 1 = wrong current password, 2 = not found, 3 = new password too weak.</summary>
        public int ChangePassword(int studentId, string currentPassword, string newPassword)
        {
            return _students.ChangePassword(studentId, currentPassword, newPassword);
        }
    }
}
