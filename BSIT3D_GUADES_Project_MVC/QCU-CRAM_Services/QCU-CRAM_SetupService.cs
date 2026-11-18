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

        private static readonly Regex EmailPattern =
            new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

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

                foreach (QCUCRAM_Room room in content.GetRooms(false))
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
                                MinCapacity = room.MinCapacity,
                                MaxCapacity = room.MaxCapacity,
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

            string name = (model.RoomName ?? "").Trim();

            if (name.Length == 0 || name.Length > 50)
            {
                return Retry("Room name is required (maximum 50 characters).");
            }

            if (model.MinCapacity < 1 || model.MaxCapacity < 1 ||
                model.MinCapacity > 100 || model.MaxCapacity > 100)
            {
                return Retry("Capacities must be whole numbers from 1 to 100.");
            }

            if (model.MinCapacity > model.MaxCapacity)
            {
                return Retry("Min capacity cannot be greater than max capacity.");
            }

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_ContentRepository content = new QCUCRAM_ContentRepository(db);

                if (model.RoomID > 0)
                {
                    int code = content.UpdateRoom(model.RoomID, name,
                        model.MinCapacity, model.MaxCapacity, model.IsActive);

                    if (code == 0) { return QCUCRAMOperationResult.Success("Room updated."); }
                    if (code == 1) { return Retry("A room named \"" + name + "\" already exists."); }
                    if (code == 2) { return QCUCRAMOperationResult.Failure("That room no longer exists."); }

                    return Retry("Sorry, the room could not be saved. Please try again.");
                }

                int newId;
                int created = content.InsertRoom(name, model.MinCapacity, model.MaxCapacity, out newId);

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

                return QCUCRAMOperationResult.Success(
                    room.RoomName + (room.IsActive ? " disabled." : " enabled."));
            }
        }

        public static QCUCRAMOperationResult DeleteRoom(int roomId)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_ContentRepository content = new QCUCRAM_ContentRepository(db);

                QCUCRAM_Room room = content.GetRoom(roomId);
                if (room == null)
                {
                    return QCUCRAMOperationResult.Failure("That room no longer exists.");
                }

                int code = content.DeleteRoom(roomId);
                if (code == -1)
                {
                    return QCUCRAMOperationResult.Failure(
                        "This room has reservations and cannot be deleted. Disable it instead.");
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

            TimeSpan start;
            TimeSpan end;

            if (!TimeSpan.TryParseExact((model.Start ?? "").Trim(), @"hh\:mm",
                                        CultureInfo.InvariantCulture, out start) &&
                !TimeSpan.TryParse((model.Start ?? "").Trim(), out start))
            {
                return Retry("The start time is not a valid time.");
            }

            if (!TimeSpan.TryParseExact((model.End ?? "").Trim(), @"hh\:mm",
                                        CultureInfo.InvariantCulture, out end) &&
                !TimeSpan.TryParse((model.End ?? "").Trim(), out end))
            {
                return Retry("The end time is not a valid time.");
            }

            if (end <= start)
            {
                return Retry("The end time must be after the start time.");
            }

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_ContentRepository content = new QCUCRAM_ContentRepository(db);

                if (model.TimeSlotID > 0)
                {
                    // A slot that already holds a reservation has frozen times.
                    int used;
                    ReservationCountsBy(db, "TimeSlotID")
                        .TryGetValue(model.TimeSlotID, out used);

                    QCUCRAM_TimeSlot existing = content.GetTimeSlot(model.TimeSlotID);
                    if (existing == null)
                    {
                        return QCUCRAMOperationResult.Failure("That time slot no longer exists.");
                    }

                    if (used > 0 &&
                        (existing.StartTime != start || existing.EndTime != end))
                    {
                        return Retry(
                            "This time slot already has reservations, so its times can no longer be changed. " +
                            "Only its active flag may be switched.");
                    }

                    if (used > 0)
                    {
                        content.SetTimeSlotActive(model.TimeSlotID, model.IsActive);
                        return QCUCRAMOperationResult.Success("Time slot updated.");
                    }

                    int code = content.UpdateTimeSlot(model.TimeSlotID, start, end, model.IsActive);
                    if (code == 0) { return QCUCRAMOperationResult.Success("Time slot updated."); }
                    if (code == 1) { return Retry("Another slot already starts at that time."); }
                    if (code == 2) { return QCUCRAMOperationResult.Failure("That time slot no longer exists."); }

                    return Retry("Sorry, the time slot could not be saved. Please try again.");
                }

                // New slot: it may not overlap any existing slot.
                foreach (QCUCRAM_TimeSlot other in content.GetTimeSlots(false))
                {
                    if (start < other.EndTime && end > other.StartTime)
                    {
                        return Retry("That time range overlaps the existing slot " +
                                     QCUCRAM_ReservationInfo.FormatSlot(other.StartTime, other.EndTime) + ".");
                    }
                }

                int newId;
                int created = content.InsertTimeSlot(start, end, out newId);

                if (created == 0)
                {
                    if (!model.IsActive) { content.SetTimeSlotActive(newId, false); }
                    return QCUCRAMOperationResult.Success("Time slot added.");
                }
                if (created == 1) { return Retry("Another slot already starts at that time."); }

                return Retry("Sorry, the time slot could not be saved. Please try again.");
            }
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

                string label = QCUCRAM_ReservationInfo.FormatSlot(slot.StartTime, slot.EndTime);
                return QCUCRAMOperationResult.Success(
                    label + (slot.IsActive ? " disabled." : " enabled."));
            }
        }

        public static QCUCRAMOperationResult DeleteSlot(int timeSlotId)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_ContentRepository content = new QCUCRAM_ContentRepository(db);

                QCUCRAM_TimeSlot slot = content.GetTimeSlot(timeSlotId);
                if (slot == null)
                {
                    return QCUCRAMOperationResult.Failure("That time slot no longer exists.");
                }

                int code = content.DeleteTimeSlot(timeSlotId);
                if (code == -1)
                {
                    return QCUCRAMOperationResult.Failure(
                        "This time slot is used by a reservation and cannot be deleted. Disable it instead.");
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
            vm.PageSize = pageSize < 1 ? 20 : pageSize;

            foreach (string p in QCUCRAM_Programs.All) { vm.ProgramOptions.Add(p); }
            foreach (string t in QCUCRAM_Programs.RequesterTypes) { vm.RequesterTypes.Add(t); }

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_StudentRepository students = new QCUCRAM_StudentRepository(db);

                // QCUCRAM_StudentRepository.Search filters on text and requester type only,
                // so the program filter is applied here. The list is already ordered by
                // last name, so filtering in memory keeps that order.
                List<QCUCRAM_Student> all = students.Search(
                    vm.Search.Length == 0 ? null : vm.Search,
                    null,
                    null);

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

        public static QCUCRAMOperationResult SaveStudent(QCUCRAMStudentEditModel model)
        {
            if (model == null) { return Retry("Account details are missing."); }

            string number = (model.StudentNumber ?? "").Trim();
            string last = (model.LastName ?? "").Trim();
            string first = (model.FirstName ?? "").Trim();
            string mi = (model.MI ?? "").Trim().TrimEnd('.').ToUpperInvariant();
            string email = (model.Email ?? "").Trim();
            string program = (model.Program ?? "").Trim();
            string type = (model.RequesterType ?? "").Trim();

            if (!QCUCRAM_Programs.IsValidRequesterType(type))
            {
                return Retry("Choose a requester type of Student, Faculty or Visitor.");
            }

            if (!QCUCRAM_Programs.IsValidProgram(program))
            {
                return Retry("Choose a program from the list.");
            }

            if (last.Length == 0 || first.Length == 0 || last.Length > 60 || first.Length > 60)
            {
                return Retry("Last name and first name are required (maximum 60 characters each).");
            }

            if (!EmailPattern.IsMatch(email) || email.Length > 120)
            {
                return Retry("Enter a valid e-mail address (maximum 120 characters).");
            }

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_StudentRepository students = new QCUCRAM_StudentRepository(db);

                if (model.StudentID > 0)
                {
                    int code = students.UpdateProfile(model.StudentID, last, first, mi, email, program);

                    if (code == 0) { return QCUCRAMOperationResult.Success("Account updated."); }
                    if (code == 1) { return Retry("That e-mail address is already registered."); }
                    if (code == 2) { return QCUCRAMOperationResult.Failure("That account no longer exists."); }

                    return Retry("Sorry, the account could not be saved. Please try again.");
                }

                if (type == "Student")
                {
                    if (!StudentNumberPattern.IsMatch(number))
                    {
                        return Retry("A student number must look like 23-0001.");
                    }
                }
                else if (!LooseNumberPattern.IsMatch(number))
                {
                    return Retry("An ID must be 3-20 letters, numbers or dashes.");
                }

                string temporary = QCUCRAM_PasswordHash.StudentTemporaryPassword(number);

                QCUCRAM_RegisterResult result = students.Register(
                    number, last, first, mi, email, program, type, temporary);

                if (result.Success)
                {
                    return QCUCRAMOperationResult.Success(
                        "Account " + number + " added. Temporary password: " + temporary + ".");
                }

                if (result.ResultCode == 1)
                {
                    return Retry("That ID / student number is already registered.");
                }
                if (result.ResultCode == 2)
                {
                    return Retry("That e-mail address is already registered.");
                }

                return Retry("Sorry, the account could not be saved. Please check the details and try again.");
            }
        }

        public static QCUCRAMOperationResult ResetStudentPassword(int studentId)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_StudentRepository students = new QCUCRAM_StudentRepository(db);

                QCUCRAM_Student student = students.GetById(studentId);
                if (student == null)
                {
                    return QCUCRAMOperationResult.Failure("That account no longer exists.");
                }

                string temporary = QCUCRAM_PasswordHash.StudentTemporaryPassword(student.StudentNumber);
                students.ResetPassword(studentId, temporary);

                return QCUCRAMOperationResult.Success(
                    "Password reset for " + student.StudentNumber +
                    ". Temporary password: " + temporary + ".");
            }
        }

        public static QCUCRAMOperationResult DeleteStudent(int studentId)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_StudentRepository students = new QCUCRAM_StudentRepository(db);

                QCUCRAM_Student student = students.GetById(studentId);
                if (student == null)
                {
                    return QCUCRAMOperationResult.Failure("That account no longer exists.");
                }

                int code = students.Delete(studentId);
                if (code == -1)
                {
                    return QCUCRAMOperationResult.Failure(
                        "This account has reservations and cannot be deleted.");
                }

                return QCUCRAMOperationResult.Success("Account " + student.StudentNumber + " deleted.");
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
