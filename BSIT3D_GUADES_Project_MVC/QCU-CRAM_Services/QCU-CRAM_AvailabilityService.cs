using System;
using System.Collections.Generic;
using System.Globalization;
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
    ///   QCUCRAM_ContentRepository.GetRooms()          - every room, active or not
    ///   QCUCRAM_ContentRepository.GetTimeSlots()      - every slot, active or not
    ///   QCUCRAM_ContentRepository.GetAvailability()   - the held cells (room + slot + status)
    ///   QCUCRAM_ReservationRepository.GetByDate()     - the requester names an admin sees
    ///
    /// A disabled room or slot still shows a booking if one already exists in it, exactly
    /// like the original page, because the booking must not vanish from the grid.
    /// </summary>
    public class QCUCRAMAvailabilityService
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

        /// <summary>Today + AdvanceBookingDays - the only date a request may be filed for.</summary>
        public DateTime BookingDay
        {
            get { return QCUCRAM_Clock.Today.AddDays(_content.GetSettingInt("AdvanceBookingDays", 1)); }
        }

        /// <summary>
        /// Builds the whole grid for one date.
        /// </summary>
        /// <param name="date">The date to show; normalised to midnight.</param>
        /// <param name="studentId">The signed-in student, or null for a guest / admin.</param>
        /// <param name="isAdmin">True when an admin is looking (adds requester names to the cells).</param>
        /// <param name="highlightRoomId">?room= from the query string.</param>
        /// <param name="highlightSlotId">?slot= from the query string.</param>
        public QCUCRAMAvailabilityViewModel Build(DateTime date, int? studentId, bool isAdmin,
                                                  int? highlightRoomId, int? highlightSlotId)
        {
            DateTime bookingDay = BookingDay;
            DateTime day = date.Date;

            QCUCRAMAvailabilityViewModel vm = new QCUCRAMAvailabilityViewModel
            {
                Date = day,
                BookingDay = bookingDay,
                IsAdmin = isAdmin,
                IsStudent = studentId.HasValue && studentId.Value > 0,
                CanBook = day == bookingDay,
                IsClosed = day < bookingDay,
                IsFuture = day > bookingDay,
                HighlightRoomId = highlightRoomId,
                HighlightSlotId = highlightSlotId,
                RulesHtml = _content.BuildRulesHtml()
            };

            // ---- columns: every room, in the order the admin arranged them ----
            foreach (QCUCRAM_Room room in _content.GetRooms())
            {
                vm.Rooms.Add(new QCUCRAMRoomColumn
                {
                    RoomId = room.RoomID,
                    RoomName = room.RoomName,
                    MinCapacity = room.MinCapacity,
                    MaxCapacity = room.MaxCapacity,
                    IsActive = room.IsActive
                });
            }

            // ---- rows: every time slot ----
            foreach (QCUCRAM_TimeSlot slot in _content.GetTimeSlots())
            {
                vm.Slots.Add(new QCUCRAMSlotRow
                {
                    TimeSlotId = slot.TimeSlotID,
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    IsActive = slot.IsActive
                });
            }

            vm.TotalCells = vm.Rooms.Count * vm.Slots.Count;

            // ---- the held cells, keyed room|slot ----
            Dictionary<string, QCUCRAM_AvailabilityCell> held =
                new Dictionary<string, QCUCRAM_AvailabilityCell>(StringComparer.Ordinal);

            foreach (QCUCRAM_AvailabilityCell cell in _content.GetAvailability(day))
            {
                held[QCUCRAMAvailabilityViewModel.Key(cell.RoomId, cell.TimeSlotId)] = cell;
            }

            // ---- requester names for the admin view, keyed by reservation id ----
            Dictionary<int, QCUCRAM_ReservationListItem> details =
                new Dictionary<int, QCUCRAM_ReservationListItem>();

            if (isAdmin)
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
                        case "mine": vm.MineCount++; break;
                        case "closed": vm.ClosedCount++; break;
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
                TimeSlotId = slot.TimeSlotId,
                IsHighlighted = vm.HighlightRoomId.HasValue && vm.HighlightSlotId.HasValue
                                && vm.HighlightRoomId.Value == room.RoomId
                                && vm.HighlightSlotId.Value == slot.TimeSlotId
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
                    cell.Text = pending ? "Your request" : "Your booking";
                    cell.Detail = "#" + taken.ReservationId;
                }
                else if (pending)
                {
                    cell.State = "pending";
                    cell.CssClass = "table-warning";
                    cell.StateClass = "qcu-cell--pending";
                    cell.Text = "Pending";
                    cell.Detail = isAdmin ? AdminDetail(taken.ReservationId, detail) : string.Empty;
                }
                else
                {
                    cell.State = "taken";
                    cell.CssClass = "table-danger";
                    cell.StateClass = "qcu-cell--taken";
                    cell.Text = "Taken";
                    cell.Detail = isAdmin ? AdminDetail(taken.ReservationId, detail) : string.Empty;
                }

                return cell;
            }

            // ---- 2. nothing booked: a closed date beats a disabled room ----
            if (vm.IsClosed)
            {
                cell.State = "closed";
                cell.CssClass = "qcu-closed";
                cell.StateClass = "qcu-cell--closed";
                cell.Text = "Closed";
                return cell;
            }

            // ---- 3. a disabled room or slot ----
            if (!room.IsActive || !slot.IsActive)
            {
                cell.State = "off";
                cell.CssClass = "table-secondary";
                cell.StateClass = "qcu-cell--unavailable";
                cell.Text = "\u2014";
                return cell;
            }

            // ---- 4. open ----
            cell.State = "open";
            cell.CssClass = "table-success";
            cell.StateClass = "qcu-cell--open";
            cell.Text = "Open";
            cell.IsBookable = vm.CanBook;

            return cell;
        }

        private static string AdminDetail(int? reservationId, QCUCRAM_ReservationListItem detail)
        {
            string head = "#" + (reservationId.HasValue
                ? reservationId.Value.ToString(CultureInfo.InvariantCulture)
                : "?");

            if (detail == null) { return head; }

            return head + " " + detail.StudentName + " (" + detail.StudentNumber + ")";
        }
    }
}
