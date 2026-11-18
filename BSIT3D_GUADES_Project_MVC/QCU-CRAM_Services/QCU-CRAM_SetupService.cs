using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.RegularExpressions;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Services
{
    /// <summary>
    /// Phase 08 - the maintenance screens: rooms, time slots, student accounts,
    /// administrator accounts, settings and developer tools.
    ///
    /// This is the MVC replacement for the code-behind of Rooms.aspx, TimeSlots.aspx,
    /// Students.aspx, AdminUsers.aspx, Settings.aspx and DevTools.aspx. It is a static
    /// helper (unlike QCUCRAMAdminService) because every screen is a self-contained
    /// CRUD form with no shared state; all SQL still goes through the Phase 02
    /// repositories so the data layer remains the only place that talks to the database.
    ///
    /// The result type is the existing <see cref="QCUCRAMOperationResult"/> from
    /// QCUCRAMAdminService - <c>Retry</c> plays the role the guide called
    /// <c>KeepModalOpen</c>.
    /// </summary>
    public static class QCUCRAMSetupService
    {
        private static readonly Regex StudentNumberPattern =
            new Regex(@"^\d{2}-\d{4}$", RegexOptions.Compiled);

        private static readonly Regex LooseNumberPattern =
            new Regex(@"^[A-Za-z0-9\-]{3,20}$", RegexOptions.Compiled);

        // R04: the exact expression Students.aspx / Students.aspx.cs use for the e-mail box.
        private static readonly Regex EmailPattern =
            new Regex(@"^\w+([-+.']\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$", RegexOptions.Compiled);

        private static readonly Regex UsernamePattern =
            new Regex(@"^[A-Za-z0-9._\-]{3,50}$", RegexOptions.Compiled);

        // =================================================================
        //  Rooms
        // =================================================================
        public static QCUCRAMRoomsViewModel Rooms(int? editId)
        {
            QCUCRAMRoomsViewModel vm = new QCUCRAMRoomsViewModel();

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_ContentRepository content = new QCUCRAM_ContentRepository(db);

                Dictionary<int, int> counts = ReservationCountsBy(db, "RoomID");

                // Rooms.aspx.cs BindGrid: ORDER BY r.RoomID (GetRooms sorts by name for other pages)
                List<QCUCRAM_Room> allRooms = content.GetRooms(false);
                allRooms.Sort((x, y) => x.RoomID.CompareTo(y.RoomID));

                foreach (QCUCRAM_Room room in allRooms)
                {
                    int used;
                    counts.TryGetValue(room.RoomID, out used);

                    vm.Rows.Add(new QCUCRAMRoomRow
                    {
                        RoomID = room.RoomID,
                        RoomName = room.RoomName,
                        MinCapacity = room.MinCapacity,
                        MaxCapacity = room.MaxCapacity,
                        IsActive = room.IsActive,
                        ReservationCount = used
                    });
                }

                // edit absent  -> plain list
                // edit = 0    -> "Add room"  (blank form)
                // edit > 0    -> "Edit room" (filled form)
                if (editId.HasValue)
                {
                    if (editId.Value > 0)
                    {
                        QCUCRAM_Room room = content.GetRoom(editId.Value);
                        if (room != null)
                        {
                            vm.Edit = new QCUCRAMRoomEditModel
                            {
                                RoomID = room.RoomID,
                                RoomName = room.RoomName,
                                MinCapacity = room.MinCapacity.ToString(),
                                MaxCapacity = room.MaxCapacity.ToString(),
                                IsActive = room.IsActive
                            };
                            vm.ModalOpen = true;
                        }
                    }
                    else
                    {
                        vm.Edit = new QCUCRAMRoomEditModel();
                        vm.ModalOpen = true;
                    }
                }
            }

            return vm;
        }

        public static QCUCRAMOperationResult SaveRoom(QCUCRAMRoomEditModel model)
        {
            if (model == null) { return Retry("Room details are missing."); }

            model.NameErrors.Clear();
            model.MinErrors.Clear();
            model.MaxErrors.Clear();

            string name = (model.RoomName ?? "").Trim();
            string minText = (model.MinCapacity ?? "").Trim();
            string maxText = (model.MaxCapacity ?? "").Trim();
            int min, max;
            bool minIsInt = int.TryParse(minText, System.Globalization.NumberStyles.Integer,
                                         System.Globalization.CultureInfo.InvariantCulture, out min);
            bool maxIsInt = int.TryParse(maxText, System.Globalization.NumberStyles.Integer,
                                         System.Globalization.CultureInfo.InvariantCulture, out max);

            // Rooms.aspx validators (ValidationGroup="Room"), in markup order. Each one that
            // fails shows its own message under the box and keeps the pop-up open.
            if (name.Length == 0) { model.NameErrors.Add("Room name is required."); }

            if (minText.Length == 0)
            {
                model.MinErrors.Add("Min capacity is required.");
            }
            else if (!minIsInt || min < 1 || min > 100)
            {
                model.MinErrors.Add("Min capacity must be a whole number from 1 to 100.");
            }

            if (maxText.Length == 0)
            {
                model.MaxErrors.Add("Max capacity is required.");
            }
            else
            {
                if (!maxIsInt || max < 1 || max > 100)
                {
                    model.MaxErrors.Add("Max capacity must be a whole number from 1 to 100.");
                }
                // CompareValidator (GreaterThanEqual, Integer): fails for a non-number; passes
                // when the min box cannot be read as a number.
                if (!maxIsInt || (minIsInt && max < min))
                {
                    model.MaxErrors.Add("Max capacity must be at least the min capacity.");
                }
            }

            if (model.NameErrors.Count > 0 || model.MinErrors.Count > 0 || model.MaxErrors.Count > 0)
            {
                return Retry(string.Empty);     // the validators show their own messages
            }

            // btnSave_Click server-side re-checks (browser checks can be bypassed)
            if (name.Length == 0 || name.Length > 50)
            {
                return Retry("Room name is required (maximum 50 characters).");
            }

            if (!minIsInt || !maxIsInt || min < 1 || max < 1 || min > 100 || max > 100)
            {
                return Retry("Capacities must be whole numbers from 1 to 100.");
            }

            if (min > max)
            {
                return Retry("Min capacity cannot be greater than max capacity.");
            }

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_ContentRepository content = new QCUCRAM_ContentRepository(db);

                if (model.RoomID > 0)
                {
                    int code = content.UpdateRoom(model.RoomID, name, min, max, model.IsActive);

                    if (code == 0) { return QCUCRAMOperationResult.Success("Room updated."); }
                    if (code == 1) { return Retry("A room named \"" + name + "\" already exists."); }
                    if (code == 2) { return QCUCRAMOperationResult.Failure("That room no longer exists."); }

                    return Retry("Sorry, the room could not be saved. Please try again.");
                }

                int newId;
                int created = content.InsertRoom(name, min, max, out newId);

                if (created == 0)
                {
                    if (!model.IsActive) { content.SetRoomActive(newId, false); }
                    return QCUCRAMOperationResult.Success("Room added.");
                }
                if (created == 1) { return Retry("A room named \"" + name + "\" already exists."); }

                return Retry("Sorry, the room could not be saved. Please try again.");
            }
        }

        public static QCUCRAMOperationResult ToggleRoom(int roomId)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_ContentRepository content = new QCUCRAM_ContentRepository(db);

                QCUCRAM_Room room = content.GetRoom(roomId);
                if (room == null)
                {
                    return QCUCRAMOperationResult.Failure("That room no longer exists.");
                }

                content.SetRoomActive(roomId, !room.IsActive);

                // Rooms.aspx.cs ToggleRoom wording
                if (!room.IsActive) { return QCUCRAMOperationResult.Success("Room enabled."); }

                int upcoming = db.Count(
                    @"SELECT COUNT(*) AS Total FROM Reservations
                       WHERE RoomID = @id AND ReservationDate >= @today
                         AND Status NOT IN ('Declined','Cancelled','Forfeited')",
                    QCUCRAM_Db.P("@id", roomId),
                    QCUCRAM_Db.P("@today", System.Data.SqlDbType.Date, QCUCRAM_Clock.Today));

                string msg = "Room disabled. It won't be offered for new bookings.";
                if (upcoming > 0) { msg += " " + upcoming + " upcoming reservation(s) still use it."; }
                return QCUCRAMOperationResult.Success(msg);
            }
        }

        public static QCUCRAMOperationResult DeleteRoom(int roomId)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_ContentRepository content = new QCUCRAM_ContentRepository(db);

                int resCount = db.Count("SELECT COUNT(*) AS Total FROM Reservations WHERE RoomID = @id",
                    QCUCRAM_Db.P("@id", roomId));
                if (resCount > 0)
                {
                    return QCUCRAMOperationResult.Failure(
                        "This room has reservations and cannot be deleted. Disable it instead.");
                }

                if (content.DeleteRoom(roomId) == -1)
                {
                    return QCUCRAMOperationResult.Failure(
                        "This room is still in use and cannot be deleted. Disable it instead.");
                }

                return QCUCRAMOperationResult.Success("Room deleted.");
            }
        }

        // =================================================================
        //  Time slots
        // =================================================================
        public static QCUCRAMTimeSlotsViewModel TimeSlots(int? editId)
        {
            QCUCRAMTimeSlotsViewModel vm = new QCUCRAMTimeSlotsViewModel();

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_ContentRepository content = new QCUCRAM_ContentRepository(db);

                Dictionary<int, int> counts = ReservationCountsBy(db, "TimeSlotID");

                foreach (QCUCRAM_TimeSlot slot in content.GetTimeSlots(false))
                {
                    int used;
                    counts.TryGetValue(slot.TimeSlotID, out used);

                    vm.Rows.Add(new QCUCRAMSetupSlotRow
                    {
                        TimeSlotID = slot.TimeSlotID,
                        StartTime = slot.StartTime,
                        EndTime = slot.EndTime,
                        Label = QCUCRAM_ReservationInfo.FormatSlot(slot.StartTime, slot.EndTime),
                        IsActive = slot.IsActive,
                        ReservationCount = used
                    });
                }

                // edit absent  -> plain list
                // edit = 0    -> "Add slot"  (blank form)
                // edit > 0    -> "Edit slot" (filled form)
                if (editId.HasValue)
                {
                    if (editId.Value > 0)
                    {
                        QCUCRAM_TimeSlot slot = content.GetTimeSlot(editId.Value);
                        if (slot != null)
                        {
                            vm.Edit = new QCUCRAMSlotEditModel
                            {
                                TimeSlotID = slot.TimeSlotID,
                                Start = slot.StartTime.ToString(@"hh\:mm"),
                                End = slot.EndTime.ToString(@"hh\:mm"),
                                IsActive = slot.IsActive
                            };
                            vm.ModalOpen = true;
                        }
                    }
                    else
                    {
                        vm.Edit = new QCUCRAMSlotEditModel();
                        vm.ModalOpen = true;
                    }
                }
            }

            return vm;
        }

        public static QCUCRAMOperationResult SaveSlot(QCUCRAMSlotEditModel model)
        {
            if (model == null) { return Retry("Time slot details are missing."); }

            model.StartErrors.Clear();
            model.EndErrors.Clear();

            // TimeSlots.aspx validators (ValidationGroup="Slot"): shown under the boxes, pop-up stays open
            if (string.IsNullOrWhiteSpace(model.Start)) { model.StartErrors.Add("Start time is required."); }
            if (string.IsNullOrWhiteSpace(model.End)) { model.EndErrors.Add("End time is required."); }
            if (model.StartErrors.Count > 0 || model.EndErrors.Count > 0)
            {
                return Retry(string.Empty);
            }

            TimeSpan start;
            TimeSpan end;

            // TimeSlots.aspx.cs TryParseTime + the two server-side checks
            if (!TryParseTime(model.Start, out start) || !TryParseTime(model.End, out end))
            {
                return Retry("Enter valid start and end times (for example 08:00 and 09:00).");
            }
            if (end <= start)
            {
                return Retry("End time must be later than start time.");
            }

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_ContentRepository content = new QCUCRAM_ContentRepository(db);
                bool isEdit = model.TimeSlotID > 0;
                int idForChecks = isEdit ? model.TimeSlotID : 0;

                // 1) Duplicate start time (ignoring the slot being edited)
                int dup = db.Count(
                    "SELECT COUNT(*) AS Total FROM TimeSlots WHERE StartTime = @s AND TimeSlotID <> @id",
                    QCUCRAM_Db.P("@s", start), QCUCRAM_Db.P("@id", idForChecks));
                if (dup > 0)
                {
                    return Retry("A time slot starting at " + FormatTime(start) + " already exists.");
                }

                // 2) Overlap with any other slot (touching edges are allowed)
                QCUCRAM_TimeSlot overlap = db.QueryFirstOrDefault<QCUCRAM_TimeSlot>(
                    @"SELECT TOP 1 TimeSlotID, StartTime, EndTime, IsActive FROM TimeSlots
                       WHERE StartTime < @e AND EndTime > @s AND TimeSlotID <> @id
                       ORDER BY StartTime",
                    QCUCRAM_Db.P("@s", start), QCUCRAM_Db.P("@e", end), QCUCRAM_Db.P("@id", idForChecks));
                if (overlap != null)
                {
                    return Retry("This time overlaps with the existing slot " +
                                 FormatTime(overlap.StartTime) + " \u2013 " + FormatTime(overlap.EndTime) + ".");
                }

                if (isEdit)
                {
                    QCUCRAM_TimeSlot current = content.GetTimeSlot(model.TimeSlotID);
                    if (current == null)
                    {
                        return QCUCRAMOperationResult.Failure("That time slot no longer exists.");
                    }

                    // Changing the times of a slot that already has bookings would silently move them.
                    if (current.StartTime != start || current.EndTime != end)
                    {
                        int resCount = db.Count(
                            "SELECT COUNT(*) AS Total FROM Reservations WHERE TimeSlotID = @id",
                            QCUCRAM_Db.P("@id", model.TimeSlotID));
                        if (resCount > 0)
                        {
                            return Retry("This slot has reservations, so its times cannot be changed. " +
                                         "Disable it and add a new slot instead.");
                        }
                    }

                    int code = content.UpdateTimeSlot(model.TimeSlotID, start, end, model.IsActive);
                    if (code == 0) { return QCUCRAMOperationResult.Success("Time slot updated."); }
                    if (code == 1) { return Retry("A time slot with that start time already exists."); }
                    if (code == 2) { return QCUCRAMOperationResult.Failure("That time slot no longer exists."); }

                    return Retry("Sorry, the time slot could not be saved. Please try again.");
                }

                int newId;
                int created = content.InsertTimeSlot(start, end, out newId);

                if (created == 0)
                {
                    if (!model.IsActive) { content.SetTimeSlotActive(newId, false); }
                    return QCUCRAMOperationResult.Success("Time slot added.");
                }
                if (created == 1) { return Retry("A time slot with that start time already exists."); }

                return Retry("Sorry, the time slot could not be saved. Please try again.");
            }
        }

        // TimeSlots.aspx.cs FormatTime / TryParseTime (accepts 08:00, 8:00, 08:00:00 and 8:00 AM)
        private static string FormatTime(TimeSpan t)
        {
            return DateTime.Today.Add(t).ToString("h:mm tt", CultureInfo.InvariantCulture);
        }

        private static bool TryParseTime(string text, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            DateTime dt;
            if (DateTime.TryParseExact((text ?? "").Trim(),
                    new[] { "H:mm", "H:mm:ss", "h:mm tt" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                result = new TimeSpan(dt.Hour, dt.Minute, 0);
                return true;
            }
            return false;
        }

        public static QCUCRAMOperationResult ToggleSlot(int timeSlotId)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_ContentRepository content = new QCUCRAM_ContentRepository(db);

                QCUCRAM_TimeSlot slot = content.GetTimeSlot(timeSlotId);
                if (slot == null)
                {
                    return QCUCRAMOperationResult.Failure("That time slot no longer exists.");
                }

                content.SetTimeSlotActive(timeSlotId, !slot.IsActive);

                // TimeSlots.aspx.cs ToggleSlot wording
                if (!slot.IsActive) { return QCUCRAMOperationResult.Success("Time slot enabled."); }

                int upcoming = db.Count(
                    @"SELECT COUNT(*) AS Total FROM Reservations
                       WHERE TimeSlotID = @id AND ReservationDate >= @today
                         AND Status NOT IN ('Declined','Cancelled','Forfeited')",
                    QCUCRAM_Db.P("@id", timeSlotId),
                    QCUCRAM_Db.P("@today", System.Data.SqlDbType.Date, QCUCRAM_Clock.Today));

                string msg = "Time slot disabled. It won't be offered for new bookings.";
                if (upcoming > 0) { msg += " " + upcoming + " upcoming reservation(s) still use it."; }
                return QCUCRAMOperationResult.Success(msg);
            }
        }

        public static QCUCRAMOperationResult DeleteSlot(int timeSlotId)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_ContentRepository content = new QCUCRAM_ContentRepository(db);

                if (content.GetTimeSlot(timeSlotId) == null)
                {
                    return QCUCRAMOperationResult.Failure("That time slot no longer exists.");
                }

                int resCount = db.Count("SELECT COUNT(*) AS Total FROM Reservations WHERE TimeSlotID = @id",
                    QCUCRAM_Db.P("@id", timeSlotId));
                if (resCount > 0)
                {
                    return QCUCRAMOperationResult.Failure(
                        "This time slot has reservations and cannot be deleted. Disable it instead.");
                }

                if (content.DeleteTimeSlot(timeSlotId) == -1)
                {
                    return QCUCRAMOperationResult.Failure(
                        "This time slot is still in use and cannot be deleted. Disable it instead.");
                }

                return QCUCRAMOperationResult.Success("Time slot deleted.");
            }
        }

        // =================================================================
        //  Student accounts
        // =================================================================
        public static QCUCRAMStudentsViewModel Students(string search, string program,
                                                        int page, int pageSize)
        {
            QCUCRAMStudentsViewModel vm = new QCUCRAMStudentsViewModel();
            vm.Search = (search ?? "").Trim();
            vm.Program = (program ?? "").Trim();
            vm.PageSize = pageSize < 1 ? 10 : pageSize;   // GridView PageSize="10"

            foreach (string p in QCUCRAM_Programs.All) { vm.ProgramOptions.Add(p); }
            foreach (string t in QCUCRAM_Programs.RequesterTypes) { vm.RequesterTypes.Add(t); }

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_StudentRepository students = new QCUCRAM_StudentRepository(db);

                // SearchAdmin is the port of Students.aspx.cs BindGrid (LIKE ... ESCAPE across number,
                // names, e-mail, program and the "First Last" / "Last, First" forms). The program
                // dropdown filter is applied below, in memory, so the list order is kept.
                List<QCUCRAM_Student> all = students.SearchAdmin(
                    vm.Search.Length == 0 ? null : vm.Search);

                if (vm.Program.Length > 0)
                {
                    List<QCUCRAM_Student> filtered = new List<QCUCRAM_Student>();
                    foreach (QCUCRAM_Student candidate in all)
                    {
                        if (string.Equals(candidate.Program, vm.Program,
                                          StringComparison.OrdinalIgnoreCase))
                        {
                            filtered.Add(candidate);
                        }
                    }
                    all = filtered;
                }

                vm.TotalRows = all.Count;

                int pages = vm.TotalPages;
                int current = page < 1 ? 1 : page;
                if (current > pages) { current = pages; }
                vm.Page = current;

                Dictionary<int, int> counts = ReservationCountsBy(db, "StudentID");

                int skip = (current - 1) * vm.PageSize;
                int take = Math.Min(vm.PageSize, Math.Max(0, all.Count - skip));

                for (int i = skip; i < skip + take; i++)
                {
                    QCUCRAM_Student s = all[i];
                    int used;
                    counts.TryGetValue(s.StudentID, out used);

                    vm.Rows.Add(new QCUCRAMStudentRow
                    {
                        StudentID = s.StudentID,
                        StudentNumber = s.StudentNumber,
                        LastName = s.LastName,
                        FirstName = s.FirstName,
                        MI = s.MI,
                        Email = s.Email,
                        Program = s.Program,
                        RequesterType = s.RequesterType,
                        NoShowCount = s.NoShowCount,
                        IsRestricted = s.IsRestricted,
                        RestrictionLiftDate = s.RestrictionLiftDate,
                        ReservationCount = used
                    });
                }
            }

            return vm;
        }

        public static QCUCRAMStudentEditModel LoadStudent(int studentId)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_Student s = new QCUCRAM_StudentRepository(db).GetById(studentId);
                if (s == null) { return null; }

                return new QCUCRAMStudentEditModel
                {
                    StudentID = s.StudentID,
                    StudentNumber = s.StudentNumber,
                    LastName = s.LastName,
                    FirstName = s.FirstName,
                    MI = s.MI,
                    Email = s.Email,
                    Program = s.Program,
                    RequesterType = s.RequesterType
                };
            }
        }

        // ----------------------------------------------------------------
        //  R04: Save / Reset / Delete are re-derived from Guades_QCUCram_Students.aspx.cs
        //  (SaveNew, SaveEdit, ResetPassword, DeleteStudent): same checks, same order,
        //  same wording. Problems are collected and joined with a space, like the source.
        // ----------------------------------------------------------------
        private const string StudentUniqueMessage =
            "That ID / Student Number or email is already used by another account.";
        private const string StudentSaveFailedMessage =
            "Sorry, the student could not be saved. Please try again.";
        private const string StudentGoneMessage = "That student no longer exists.";
        private const string StudentHasReservationsMessage =
            "This student has reservations and cannot be deleted. Their history must be kept.";

        public static QCUCRAMOperationResult SaveStudent(QCUCRAMStudentEditModel model)
        {
            if (model == null) { return Retry("Account details are missing."); }

            return model.StudentID > 0 ? SaveStudentEdit(model) : SaveStudentNew(model);
        }

        // SaveNew(): registration rules + an automatic temporary password.
        private static QCUCRAMOperationResult SaveStudentNew(QCUCRAMStudentEditModel model)
        {
            string type = (model.RequesterType ?? "").Trim();
            string number = (model.StudentNumber ?? "").Trim();
            string last = (model.LastName ?? "").Trim();
            string first = (model.FirstName ?? "").Trim();
            string mi = (model.MI ?? "").Trim().TrimEnd('.').ToUpperInvariant();
            if (mi.Length > 2) { mi = mi.Substring(0, 2); }
            string email = (model.Email ?? "").Trim();
            string program = (model.Program ?? "").Trim();

            // A student typed as 230001 means 23-0001
            if (type == "Student") { number = QCUCRAM_Programs.NormalizeStudentNumber(number); }

            List<string> errors = new List<string>();
            if (!QCUCRAM_Programs.IsValidRequesterType(type))
            {
                errors.Add("Please select a valid requester type.");
            }
            if (!QCUCRAM_Programs.IsValidProgram(program))
            {
                errors.Add("Please select a valid program.");
            }
            if (type == "Student")
            {
                if (!StudentNumberPattern.IsMatch(number))
                {
                    errors.Add("Student number must look like 23-0001.");
                }
            }
            else if (!LooseNumberPattern.IsMatch(number))
            {
                errors.Add("ID number must be 3-20 letters, numbers, or dashes only.");
            }
            if (last.Length == 0 || first.Length == 0 || last.Length > 60 || first.Length > 60)
            {
                errors.Add("Last name and first name are required (maximum 60 characters each).");
            }
            if (email.Length == 0 || email.Length > 120 || !EmailPattern.IsMatch(email))
            {
                errors.Add("Please enter a valid email address.");
            }

            if (errors.Count > 0) { return Retry(string.Join(" ", errors)); }

            try
            {
                using (QCUCRAM_Db db = new QCUCRAM_Db())
                {
                    QCUCRAM_StudentRepository students = new QCUCRAM_StudentRepository(db);

                    if (students.GetByStudentNumber(number) != null)
                    {
                        errors.Add("Another account already uses that ID / Student Number.");
                    }
                    if (students.GetByEmail(email) != null)
                    {
                        errors.Add("Another account already uses that email address.");
                    }
                    if (errors.Count > 0) { return Retry(string.Join(" ", errors)); }

                    // "student" + the last 4 digits of the ID. The password strength rules are for
                    // passwords people choose, so they are not applied here.
                    string temporary = QCUCRAM_PasswordHash.StudentTemporaryPassword(number);

                    QCUCRAM_RegisterResult result = students.Register(
                        number, last, first, mi, email, program, type, temporary, false);

                    if (result.Success)
                    {
                        return QCUCRAMOperationResult.Success(
                            "Account " + number + " added. Temporary password: " + temporary + ".");
                    }

                    // 1 / 2 = the unique index was hit by a simultaneous save
                    if (result.ResultCode == 1 || result.ResultCode == 2)
                    {
                        return Retry(StudentUniqueMessage);
                    }

                    return Retry(StudentSaveFailedMessage);
                }
            }
            catch (SqlException)
            {
                return Retry(StudentSaveFailedMessage);
            }
        }

        // SaveEdit(): the ID / student number and the requester type are saved too.
        private static QCUCRAMOperationResult SaveStudentEdit(QCUCRAMStudentEditModel model)
        {
            int id = model.StudentID;
            string type = (model.RequesterType ?? "").Trim();
            string number = (model.StudentNumber ?? "").Trim();
            string last = (model.LastName ?? "").Trim();
            string first = (model.FirstName ?? "").Trim();
            string mi = (model.MI ?? "").Trim().TrimEnd('.');
            string email = (model.Email ?? "").Trim();
            string program = (model.Program ?? "").Trim();

            try
            {
                using (QCUCRAM_Db db = new QCUCRAM_Db())
                {
                    QCUCRAM_StudentRepository students = new QCUCRAM_StudentRepository(db);

                    QCUCRAM_Student current = students.GetById(id);
                    if (current == null)
                    {
                        return QCUCRAMOperationResult.Failure(StudentGoneMessage);
                    }

                    List<string> errors = new List<string>();
                    if (!QCUCRAM_Programs.IsValidRequesterType(type))
                    {
                        errors.Add("Please select a valid requester type.");
                    }
                    if (!QCUCRAM_Programs.IsValidProgram(program) && program != current.Program)
                    {
                        errors.Add("Please select a valid program.");
                    }
                    if (!LooseNumberPattern.IsMatch(number))
                    {
                        errors.Add("ID / Student Number must be 3-20 letters, numbers, or dashes.");
                    }
                    if (last.Length == 0 || first.Length == 0 || last.Length > 60 || first.Length > 60)
                    {
                        errors.Add("Last name and first name are required (maximum 60 characters each).");
                    }
                    if (mi.Length > 5)
                    {
                        errors.Add("Middle initial is too long.");
                    }
                    if (email.Length == 0 || email.Length > 120 || !EmailPattern.IsMatch(email))
                    {
                        errors.Add("Please enter a valid email address.");
                    }

                    if (errors.Count > 0) { return Retry(string.Join(" ", errors)); }

                    // Unique checks (ignoring this student)
                    QCUCRAM_Student numberOwner = students.GetByStudentNumber(number);
                    if (numberOwner != null && numberOwner.StudentID != id)
                    {
                        errors.Add("Another account already uses that ID / Student Number.");
                    }
                    QCUCRAM_Student emailOwner = students.GetByEmail(email);
                    if (emailOwner != null && emailOwner.StudentID != id)
                    {
                        errors.Add("Another account already uses that email address.");
                    }
                    if (errors.Count > 0) { return Retry(string.Join(" ", errors)); }

                    int code = students.UpdateAdminProfile(id, number, last, first, mi, email, program, type);

                    if (code == 0) { return QCUCRAMOperationResult.Success("Student updated."); }
                    if (code == 2) { return QCUCRAMOperationResult.Failure(StudentGoneMessage); }
                    if (code == 1 || code == 4) { return Retry(StudentUniqueMessage); }

                    return Retry(StudentSaveFailedMessage);
                }
            }
            catch (SqlException ex)
            {
                // 2601 / 2627 = unique constraint hit (two people saving at the same moment)
                if (ex.Number == 2601 || ex.Number == 2627) { return Retry(StudentUniqueMessage); }
                return Retry(StudentSaveFailedMessage);
            }
        }

        // ResetPassword(): "student" + the last digits of the ID, no strength policy.
        public static QCUCRAMOperationResult ResetStudentPassword(int studentId)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_StudentRepository students = new QCUCRAM_StudentRepository(db);

                QCUCRAM_Student student = students.GetById(studentId);
                if (student == null)
                {
                    return QCUCRAMOperationResult.Failure(StudentGoneMessage);
                }

                string temporary = QCUCRAM_PasswordHash.StudentTemporaryPassword(student.StudentNumber);

                int code = students.ResetPassword(studentId, temporary);
                if (code == 2) { return QCUCRAMOperationResult.Failure(StudentGoneMessage); }
                if (code != 0)
                {
                    return QCUCRAMOperationResult.Failure(
                        "Sorry, that action could not be completed. Please try again.");
                }

                return QCUCRAMOperationResult.Success(
                    "Password reset for " + student.StudentNumber +
                    ". Temporary password: " + temporary + ".");
            }
        }

        // DeleteStudent(): reservations block the delete; notifications go with the account.
        public static QCUCRAMOperationResult DeleteStudent(int studentId)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_StudentRepository students = new QCUCRAM_StudentRepository(db);

                int reservations = db.Count(
                    "SELECT COUNT(*) AS Total FROM Reservations WHERE StudentID = @id",
                    QCUCRAM_Db.P("@id", studentId));
                if (reservations > 0)
                {
                    return QCUCRAMOperationResult.Failure(StudentHasReservationsMessage);
                }

                // -1 = FK constraint (SqlException 547); anything else bubbles up to the controller,
                // which shows "Sorry, that action could not be completed. Please try again."
                if (students.Delete(studentId) == -1)
                {
                    return QCUCRAMOperationResult.Failure(
                        "This student is still in use and cannot be deleted.");
                }

                if (students.GetById(studentId) != null)
                {
                    return QCUCRAMOperationResult.Failure(StudentHasReservationsMessage);
                }

                return QCUCRAMOperationResult.Success("Student account deleted.");
            }
        }

        // =================================================================
        //  Administrator accounts
        // =================================================================
        public static QCUCRAMAdminUsersViewModel AdminUsers()
        {
            QCUCRAMAdminUsersViewModel vm = new QCUCRAMAdminUsersViewModel();
            int me = QCUCRAM_Session.UserId;

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                foreach (QCUCRAM_AdminUser admin in new QCUCRAM_AdminUserRepository(db).GetAll())
                {
                    vm.Rows.Add(new QCUCRAMAdminRow
                    {
                        AdminID = admin.AdminID,
                        Username = admin.Username,
                        FullName = admin.FullName,
                        IsActive = admin.IsActive,
                        IsSelf = admin.AdminID == me
                    });

                    if (admin.IsActive) { vm.ActiveCount++; }
                }

                // Phase 24 parity: AdminUsers.aspx.cs set TempPasswordPreview from
                // NextAdminNumber() (COUNT(*) + 1) so the Add pop-up could show the
                // password before the account existed. SaveAdmin() uses the same
                // formula, so the preview always matches what is actually issued.
                vm.NextTemporaryPassword = QCUCRAM_PasswordHash.AdminTemporaryPassword(
                    new QCUCRAM_AdminUserRepository(db).Count() + 1);
            }

            return vm;
        }

        public static QCUCRAMOperationResult SaveAdmin(QCUCRAMAdminEditModel model)
        {
            if (model == null) { return Retry("Account details are missing."); }

            string username = (model.Username ?? "").Trim();
            string fullName = (model.FullName ?? "").Trim();

            if (model.AdminID == 0 && !UsernamePattern.IsMatch(username))
            {
                return Retry("Username must be 3-50 letters, numbers, dots, dashes or underscores.");
            }

            if (fullName.Length == 0 || fullName.Length > 120)
            {
                return Retry("Full name is required (maximum 120 characters).");
            }

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_AdminUserRepository admins = new QCUCRAM_AdminUserRepository(db);

                if (model.AdminID == 0)
                {
                    int next = admins.Count() + 1;
                    string temporary = QCUCRAM_PasswordHash.AdminTemporaryPassword(next);

                    int newId;
                    int code = admins.Insert(username, fullName, temporary, out newId);

                    if (code == 0)
                    {
                        return QCUCRAMOperationResult.Success(
                            "Admin account '" + username +
                            "' added. Temporary password: " + temporary + ".");
                    }
                    if (code == 1) { return Retry("That username is already taken."); }

                    return Retry("Sorry, the account could not be saved. Please try again.");
                }

                int updated = admins.Update(model.AdminID, username, fullName);

                if (updated == 0)
                {
                    if (model.AdminID == QCUCRAM_Session.UserId)
                    {
                        QCUCRAM_Session.RefreshName(fullName);
                    }
                    return QCUCRAMOperationResult.Success("Admin account updated.");
                }
                if (updated == 1) { return Retry("That username is already taken."); }
                if (updated == 2) { return QCUCRAMOperationResult.Failure("That account no longer exists."); }

                return Retry("Sorry, the account could not be saved. Please try again.");
            }
        }

        public static QCUCRAMOperationResult ToggleAdmin(int adminId)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_AdminUserRepository admins = new QCUCRAM_AdminUserRepository(db);

                QCUCRAM_AdminUser admin = admins.GetById(adminId);
                if (admin == null)
                {
                    return QCUCRAMOperationResult.Failure("That account no longer exists.");
                }

                if (admin.IsActive)
                {
                    if (adminId == QCUCRAM_Session.UserId)
                    {
                        return QCUCRAMOperationResult.Failure("You cannot disable your own account.");
                    }

                    int code = admins.SetActive(adminId, false);
                    if (code == 4)
                    {
                        return QCUCRAMOperationResult.Failure(
                            "At least one active admin account must remain, so '" +
                            admin.Username + "' was not disabled.");
                    }

                    return QCUCRAMOperationResult.Success(
                        "Account '" + admin.Username + "' disabled. They can no longer log in.");
                }

                admins.SetActive(adminId, true);
                return QCUCRAMOperationResult.Success("Account '" + admin.Username + "' enabled.");
            }
        }

        public static QCUCRAMOperationResult ResetAdminPassword(int adminId)
        {
            if (adminId == QCUCRAM_Session.UserId)
            {
                return QCUCRAMOperationResult.Failure(
                    "To change your own password, use Change Password.");
            }

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_AdminUserRepository admins = new QCUCRAM_AdminUserRepository(db);

                QCUCRAM_AdminUser admin = admins.GetById(adminId);
                if (admin == null)
                {
                    return QCUCRAMOperationResult.Failure("That account no longer exists.");
                }

                int number = db.Count(
                    "SELECT COUNT(*) AS Total FROM AdminUsers WHERE AdminID <= @id",
                    QCUCRAM_Db.P("@id", adminId));

                string temporary = QCUCRAM_PasswordHash.AdminTemporaryPassword(number);
                admins.ResetPassword(adminId, temporary);

                return QCUCRAMOperationResult.Success(
                    "Password reset for " + admin.Username +
                    ". Temporary password: " + temporary + ".");
            }
        }

        // =================================================================
        //  Settings
        // =================================================================
        private static readonly string[] SettingOrder =
        {
            "GraceMinutes", "NoShowThreshold", "RestrictionDays",
            "AdvanceBookingDays", "DevTimeOffsetMinutes"
        };

        private static readonly Dictionary<string, int[]> SettingRules =
            new Dictionary<string, int[]>
            {
                { "GraceMinutes",         new[] { 1, 240 } },
                { "NoShowThreshold",      new[] { 1, 20 } },
                { "RestrictionDays",      new[] { 0, 365 } },
                { "AdvanceBookingDays",   new[] { 0, 30 } },
                { "DevTimeOffsetMinutes", new[] { 0, 1000000 } }
            };

        private static readonly Dictionary<string, string> SettingDescriptions =
            new Dictionary<string, string>
            {
                { "GraceMinutes",         "Minutes after a slot starts before an unclaimed booking is forfeited." },
                { "NoShowThreshold",      "Number of forfeitures before a requester is restricted." },
                { "RestrictionDays",      "How many days an automatic restriction lasts." },
                { "AdvanceBookingDays",   "How many days ahead a booking may be filed." },
                { "DevTimeOffsetMinutes", "Developer clock offset in minutes. 0 means real time." }
            };

        public static QCUCRAMSettingsViewModel Settings()
        {
            QCUCRAMSettingsViewModel vm = new QCUCRAMSettingsViewModel();

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                Dictionary<string, QCUCRAM_Setting> stored =
                    new Dictionary<string, QCUCRAM_Setting>(StringComparer.OrdinalIgnoreCase);

                foreach (QCUCRAM_Setting row in new QCUCRAM_ContentRepository(db).GetAllSettings())
                {
                    if (row.SettingKey != null) { stored[row.SettingKey] = row; }
                }

                foreach (string key in SettingOrder)
                {
                    QCUCRAM_Setting row;
                    stored.TryGetValue(key, out row);

                    int[] range = SettingRules[key];

                    vm.Rows.Add(new QCUCRAMSettingRow
                    {
                        Key = key,
                        Value = row == null ? "" : row.SettingValue,
                        Description = (row != null && !string.IsNullOrEmpty(row.Description))
                            ? row.Description
                            : SettingDescriptions[key],
                        Min = range[0],
                        Max = range[1]
                    });
                }
            }

            return vm;
        }

        public static QCUCRAMOperationResult SaveSettings(IDictionary<string, string> values)
        {
            if (values == null)
            {
                return QCUCRAMOperationResult.Failure("No settings were submitted.");
            }

            List<string> errors = new List<string>();
            Dictionary<string, int> parsed = new Dictionary<string, int>();

            foreach (string key in SettingOrder)
            {
                string raw;
                if (!values.TryGetValue(key, out raw)) { continue; }

                int value;
                int[] range = SettingRules[key];

                if (!int.TryParse((raw ?? "").Trim(), NumberStyles.Integer,
                                  CultureInfo.InvariantCulture, out value))
                {
                    errors.Add(key + " must be a whole number.");
                    continue;
                }

                if (value < range[0] || value > range[1])
                {
                    errors.Add(key + " must be between " + range[0] + " and " + range[1] + ".");
                    continue;
                }

                parsed[key] = value;
            }

            if (errors.Count > 0)
            {
                return QCUCRAMOperationResult.Failure(string.Join(" ", errors.ToArray()));
            }

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_ContentRepository content = new QCUCRAM_ContentRepository(db);

                foreach (KeyValuePair<string, int> pair in parsed)
                {
                    content.SetSetting(pair.Key,
                        pair.Value.ToString(CultureInfo.InvariantCulture));
                }
            }

            // SetSetting already drops the clock cache; do it again so the very next
            // request is guaranteed to see the new offset.
            QCUCRAM_Clock.Invalidate();

            return QCUCRAMOperationResult.Success("Settings saved.");
        }

        // =================================================================
        //  Shared helpers
        // =================================================================
        private static QCUCRAMOperationResult Retry(string message)
        {
            return new QCUCRAMOperationResult { Ok = false, Message = message, Retry = true };
        }

        /// <summary>
        /// "SELECT &lt;column&gt;, COUNT(*) ... GROUP BY &lt;column&gt;" as a dictionary.
        /// Used to show how many reservations each room, slot or student holds.
        ///
        /// Phase 31: the column name is a whitelisted identifier, not a string spliced
        /// into SQL. Every caller passes a literal today, but a column name is the one
        /// thing a parameter cannot carry - so it is checked against a fixed set instead.
        /// Anything else throws rather than reaching the database.
        /// </summary>
        private static Dictionary<int, int> ReservationCountsBy(QCUCRAM_Db db, string column)
        {
            Dictionary<int, int> map = new Dictionary<int, int>();

            string safeColumn = SafeCountColumn(column);

            string sql = "SELECT " + safeColumn + " AS KeyId, COUNT(*) AS Total " +
                         "FROM Reservations GROUP BY " + safeColumn;

            foreach (CountRow row in db.Query<CountRow>(sql))
            {
                map[row.KeyId] = row.Total;
            }

            return map;
        }

        /// <summary>
        /// The only columns ReservationCountsBy may group on. Kept in one place so adding
        /// a fourth caller is a deliberate edit here, not an accidental injection point.
        /// </summary>
        private static string SafeCountColumn(string column)
        {
            switch (column)
            {
                case "RoomID":
                case "TimeSlotID":
                case "StudentID":
                    return column;
                default:
                    throw new ArgumentException(
                        "ReservationCountsBy does not support the column '" + column + "'.",
                        "column");
            }
        }

        /// <summary>
        /// Shaped row for the GROUP BY above. Public because EF's SqlQuery&lt;T&gt; builds it
        /// by reflection - a private nested type is not reliably materialised.
        /// </summary>
        public class CountRow
        {
            public int KeyId { get; set; }
            public int Total { get; set; }
        }
    }
}
