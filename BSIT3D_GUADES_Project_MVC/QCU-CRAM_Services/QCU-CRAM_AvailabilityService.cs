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
    /// Phase 06 - builds the availability grid.
    ///
    /// This is the MVC replacement for the code-behind of
    /// Guades_QCUCram_Availability.aspx, which ran three queries (rooms, slots, the
    /// reservations on the chosen date) and then walked every room x slot pair filling
    /// a byCell dictionary. The same reads happen here through the repositories:
    ///
    ///   QCUCRAM_ContentRepository.GetRooms()          - every room (filtered here to active + held)
    ///   QCUCRAM_ContentRepository.GetTimeSlots()      - every slot (filtered here to active + held)
    ///   QCUCRAM_ContentRepository.GetAvailability()   - the held cells (room + slot + status)
    ///   QCUCRAM_ReservationRepository.GetByDate()     - who holds each cell (admin names; a student's own cells)
    ///
    /// A disabled room or slot still shows a booking if one already exists in it, exactly
    /// like the original page, because the booking must not vanish from the grid. A
    /// disabled room or slot with NO booking on the date is left out, as in the original.
    /// </summary>
    public class QCUCRAMAvailabilityService : IDisposable
    {
        private readonly QCUCRAM_ContentRepository _content;
        private readonly QCUCRAM_ReservationRepository _reservations;

        public QCUCRAMAvailabilityService()
            : this(new QCUCRAM_ContentRepository(), new QCUCRAM_ReservationRepository())
        {
        }

        public QCUCRAMAvailabilityService(QCUCRAM_ContentRepository content,
                                          QCUCRAM_ReservationRepository reservations)
        {
            if (content == null) { throw new ArgumentNullException("content"); }
            if (reservations == null) { throw new ArgumentNullException("reservations"); }
            _content = content;
            _reservations = reservations;
        }

        /// <summary>
        /// Releases every repository this service created. Each repository owns a
        /// QCUCRAM_Db (and therefore an EF context + SqlConnection), so without this
        /// a request would leave those connections to the finaliser instead of the pool.
        /// </summary>
        public void Dispose()
        {
            if (_content != null) { _content.Dispose(); }
            if (_reservations != null) { _reservations.Dispose(); }
        }

        /// <summary>Today + AdvanceBookingDays - the only date a request may be filed for.</summary>
        public DateTime BookingDay
        {
            get { return QCUCRAM_Clock.Today.AddDays(_content.GetSettingInt("AdvanceBookingDays", 1)); }
        }

        /// <summary>
        /// Builds the whole grid for one date.
        /// </summary>
        /// <param name="date">The date to show; normalised to midnight.</param>
        /// <param name="studentId">The signed-in student, or null for an admin.</param>
        /// <param name="isAdmin">True when an admin is looking (adds requester names to the cells).</param>
        public QCUCRAMAvailabilityViewModel Build(DateTime date, int? studentId, bool isAdmin)
        {
            DateTime bookingDay = BookingDay;
            DateTime day = date.Date;
            bool isStudent = studentId.HasValue && studentId.Value > 0;

            QCUCRAMAvailabilityViewModel vm = new QCUCRAMAvailabilityViewModel
            {
                Date = day,
                BookingDay = bookingDay,
                IsAdmin = isAdmin,
                IsStudent = isStudent,
                CanBook = day == bookingDay,
                IsClosed = day < bookingDay,
                IsFuture = day > bookingDay,
                RulesHtml = _content.BuildRulesHtml()
            };

            // ---- the held cells, keyed room|slot ----
            Dictionary<string, QCUCRAM_AvailabilityCell> held =
                new Dictionary<string, QCUCRAM_AvailabilityCell>(StringComparer.Ordinal);
            HashSet<int> heldRooms = new HashSet<int>();
            HashSet<int> heldSlots = new HashSet<int>();

            foreach (QCUCRAM_AvailabilityCell cell in _content.GetAvailability(day))
            {
                held[QCUCRAMAvailabilityViewModel.Key(cell.RoomId, cell.TimeSlotId)] = cell;
                heldRooms.Add(cell.RoomId);
                heldSlots.Add(cell.TimeSlotId);
            }

            // ---- columns: ACTIVE rooms, plus disabled ones that still hold a booking on this
            //      date (source query), ordered by RoomID like the source ----
            foreach (QCUCRAM_Room room in _content.GetRooms().OrderBy(r => r.RoomID))
            {
                if (!room.IsActive && !heldRooms.Contains(room.RoomID)) { continue; }

                vm.Rooms.Add(new QCUCRAMRoomColumn
                {
                    RoomId = room.RoomID,
                    RoomName = room.RoomName,
                    MinCapacity = room.MinCapacity,
                    MaxCapacity = room.MaxCapacity,
                    IsActive = room.IsActive
                });
            }

            // ---- rows: ACTIVE slots, plus disabled ones that still hold a booking, by StartTime ----
            foreach (QCUCRAM_TimeSlot slot in _content.GetTimeSlots().OrderBy(t => t.StartTime))
            {
                if (!slot.IsActive && !heldSlots.Contains(slot.TimeSlotID)) { continue; }

                vm.Slots.Add(new QCUCRAMSlotRow
                {
                    TimeSlotId = slot.TimeSlotID,
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    IsActive = slot.IsActive
                });
            }

            vm.TotalCells = vm.Rooms.Count * vm.Slots.Count;

            // ---- who holds each cell, keyed by reservation id. A student needs it to know which
            //      cells are theirs ("mine"); an admin needs it for the requester name. ----
            Dictionary<int, QCUCRAM_ReservationListItem> details =
                new Dictionary<int, QCUCRAM_ReservationListItem>();

            if (isAdmin || isStudent)
            {
                foreach (QCUCRAM_ReservationListItem item in _reservations.GetByDate(day))
                {
                    details[item.ReservationId] = item;
                }
            }

            // ---- paint every intersection ----
            foreach (QCUCRAMRoomColumn room in vm.Rooms)
            {
                foreach (QCUCRAMSlotRow slot in vm.Slots)
                {
                    QCUCRAMCell cell = BuildCell(vm, room, slot, held, details, studentId, isAdmin);
                    vm.Cells[QCUCRAMAvailabilityViewModel.Key(room.RoomId, slot.TimeSlotId)] = cell;

                    switch (cell.State)
                    {
                        case "open": vm.OpenCount++; break;
                        case "pending": vm.PendingCount++; break;
                        case "taken": vm.TakenCount++; break;
                        case "closed": vm.ClosedCount++; break;
                        case "mine":
                            // Source: every held cell counts as Pending or Taken, the student's own included.
                            vm.MineCount++;
                            if (string.Equals(cell.Status, QCUCRAM_Status.Pending, StringComparison.Ordinal)) { vm.PendingCount++; }
                            else { vm.TakenCount++; }
                            break;
                    }
                }
            }

            return vm;
        }

        // ------------------------------------------------------------------
        //  One cell
        // ------------------------------------------------------------------

        private static QCUCRAMCell BuildCell(QCUCRAMAvailabilityViewModel vm,
                                             QCUCRAMRoomColumn room,
                                             QCUCRAMSlotRow slot,
                                             Dictionary<string, QCUCRAM_AvailabilityCell> held,
                                             Dictionary<int, QCUCRAM_ReservationListItem> details,
                                             int? studentId,
                                             bool isAdmin)
        {
            QCUCRAMCell cell = new QCUCRAMCell
            {
                RoomId = room.RoomId,
                TimeSlotId = slot.TimeSlotId
            };

            QCUCRAM_AvailabilityCell taken;
            held.TryGetValue(QCUCRAMAvailabilityViewModel.Key(room.RoomId, slot.TimeSlotId), out taken);

            // ---- 1. something is booked here ----
            if (taken != null)
            {
                cell.ReservationId = taken.ReservationId;
                cell.Status = taken.Status;

                QCUCRAM_ReservationListItem detail = null;
                if (taken.ReservationId.HasValue) { details.TryGetValue(taken.ReservationId.Value, out detail); }

                bool isMine = studentId.HasValue && detail != null && detail.StudentId == studentId.Value;
                bool pending = string.Equals(taken.Status, QCUCRAM_Status.Pending, StringComparison.Ordinal);

                cell.IsMine = isMine;

                if (isMine)
                {
                    cell.State = "mine";
                    cell.CssClass = "table-primary";
                    cell.StateClass = "qcu-cell--mine";
                    // Original: "Your request #12" with the status underneath.
                    cell.Text = (pending ? "Your request" : "Your booking") + " #" + taken.ReservationId;
                    cell.Detail = taken.Status;
                }
                else if (pending)
                {
                    cell.State = "pending";
                    cell.CssClass = "table-warning";
                    cell.StateClass = "qcu-cell--pending";
                    cell.Text = isAdmin
                        ? "Pending #" + taken.ReservationId
                        : "Pending";
                    cell.Detail = isAdmin ? AdminDetail(detail) : string.Empty;
                }
                else
                {
                    cell.State = "taken";
                    cell.CssClass = "table-danger";
                    cell.StateClass = "qcu-cell--taken";
                    cell.Text = isAdmin
                        ? "Taken #" + taken.ReservationId
                        : "Taken";
                    cell.Detail = isAdmin ? AdminDetail(detail) : string.Empty;
                }

                return cell;
            }

            // ---- 2. a disabled room or slot (the original tested this BEFORE closed) ----
            if (!room.IsActive || !slot.IsActive)
            {
                cell.State = "off";
                cell.CssClass = "table-secondary text-muted";
                cell.StateClass = "qcu-cell--unavailable";
                cell.Text = "Unavailable";
                return cell;
            }

            // ---- 3. closed: an empty slot on today or an earlier day ----
            if (vm.IsClosed)
            {
                cell.State = "closed";
                cell.CssClass = "qcu-closed";
                cell.StateClass = "qcu-cell--closed";
                cell.Text = "Closed";
                return cell;
            }

            // ---- 4. open ----
            cell.State = "open";
            cell.CssClass = "table-success";
            cell.StateClass = "qcu-cell--open";
            // Source: canBook = isStudent && date == bookingDay. An admin only ever sees "Open".
            cell.IsBookable = vm.IsStudent && vm.CanBook;
            cell.Text = cell.IsBookable ? "Open - Book" : "Open";

            return cell;
        }

        /// <summary>
        /// The admin tooltip under a held cell: "Last, First (23-0001)", exactly the
        /// string the original built from LastName + ", " + FirstName + " (" + StudentNumber + ")".
        /// </summary>
        private static string AdminDetail(QCUCRAM_ReservationListItem detail)
        {
            if (detail == null) { return string.Empty; }

            return detail.StudentName + " (" + detail.StudentNumber + ")";
        }
    }
}
