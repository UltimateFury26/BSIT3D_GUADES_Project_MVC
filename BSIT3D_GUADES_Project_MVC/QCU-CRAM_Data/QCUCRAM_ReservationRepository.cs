using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data
{
    /// <summary>
    /// The ONE place where reservation statuses change.
    ///
    /// Every change writes three things inside a single transaction:
    ///   1. the new status on dbo.Reservations
    ///   2. a dbo.ReservationStatusHistory audit row
    ///   3. a dbo.Notifications row for the requester
    ///
    /// Port of Guades_QCUCram_ReservationService with the same transition table,
    /// the same messages and the same locking (UPDLOCK / HOLDLOCK / Serializable).
    /// </summary>
    public class QCUCRAM_ReservationRepository
    {
        public const string ActorAdmin = "Admin";
        public const string ActorStudent = "Student";
        public const string ActorSystem = "System";

        /// <summary>Thrown inside a transaction to roll back after a rule has already been reported.</summary>
        private sealed class TransactionAbort : Exception { }

        private static readonly Dictionary<string, string[]> Allowed =
            new Dictionary<string, string[]>
            {
                { QCUCRAM_Status.Pending,    new[] { QCUCRAM_Status.Approved, QCUCRAM_Status.Declined, QCUCRAM_Status.Reassigned, QCUCRAM_Status.Cancelled } },
                { QCUCRAM_Status.Approved,   new[] { QCUCRAM_Status.CheckedIn, QCUCRAM_Status.Forfeited, QCUCRAM_Status.Cancelled, QCUCRAM_Status.Reassigned } },
                { QCUCRAM_Status.Reassigned, new[] { QCUCRAM_Status.CheckedIn, QCUCRAM_Status.Forfeited, QCUCRAM_Status.Cancelled, QCUCRAM_Status.Reassigned } },
                { QCUCRAM_Status.CheckedIn,  new[] { QCUCRAM_Status.Completed } }
                // Declined, Cancelled, Forfeited, Completed are terminal (no entry)
            };

        private readonly QCUCRAM_Db _db;

        public QCUCRAM_ReservationRepository() : this(new QCUCRAM_Db()) { }

        public QCUCRAM_ReservationRepository(QCUCRAM_Db db)
        {
            if (db == null) { throw new ArgumentNullException("db"); }
            _db = db;
        }

        public QCUCRAM_Db Db { get { return _db; } }

        // =================================================================
        //  Transition table
        // =================================================================

        public static bool CanTransition(string from, string to)
        {
            if (from == null || to == null) { return false; }
            string[] targets;
            if (!Allowed.TryGetValue(from, out targets)) { return false; }
            return Array.IndexOf(targets, to) >= 0;
        }

        /// <summary>The statuses this one is allowed to move to (empty for terminal statuses).</summary>
        public static IEnumerable<string> NextStatuses(string from)
        {
            string[] targets;
            if (from != null && Allowed.TryGetValue(from, out targets)) { return targets; }
            return new string[0];
        }

        // =================================================================
        //  Reading one reservation
        // =================================================================

        /// <summary>Returns null when the reservation does not exist.</summary>
        public QCUCRAM_ReservationInfo GetInfo(int reservationId)
        {
            using (SqlConnection con = _db.OpenConnection())
            {
                return ReadInfo(con, null, reservationId, false);
            }
        }

        /// <summary>"Collab Room 3 on Oct 7, 2026 (9:00 AM - 10:00 AM)"; empty when not found.</summary>
        public string GetReservationSummary(int reservationId)
        {
            QCUCRAM_ReservationInfo info = GetInfo(reservationId);
            return info == null ? "" : info.Summary;
        }

        private static QCUCRAM_ReservationInfo ReadInfo(SqlConnection con, SqlTransaction tx,
                                                        int reservationId, bool lockRow)
        {
            string sql =
                @"SELECT r.ReservationID, r.StudentID, r.Status, r.RoomID, r.TimeSlotID, r.ReservationDate,
                         rm.RoomName, ts.StartTime, ts.EndTime
                    FROM Reservations r " + (lockRow ? "WITH (UPDLOCK, ROWLOCK) " : "") + @"
                    JOIN Rooms rm     ON rm.RoomID = r.RoomID
                    JOIN TimeSlots ts ON ts.TimeSlotID = r.TimeSlotID
                   WHERE r.ReservationID = @id";

            using (SqlCommand cmd = new SqlCommand(sql, con, tx))
            {
                cmd.Parameters.Add(QCUCRAM_Db.P("@id", reservationId));
                using (SqlDataReader rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) { return null; }

                    return new QCUCRAM_ReservationInfo
                    {
                        ReservationId = Convert.ToInt32(rd["ReservationID"]),
                        StudentId = Convert.ToInt32(rd["StudentID"]),
                        Status = Convert.ToString(rd["Status"]),
                        RoomId = Convert.ToInt32(rd["RoomID"]),
                        TimeSlotId = Convert.ToInt32(rd["TimeSlotID"]),
                        RoomName = Convert.ToString(rd["RoomName"]),
                        Date = Convert.ToDateTime(rd["ReservationDate"]),
                        StartTime = (TimeSpan)rd["StartTime"],
                        EndTime = (TimeSpan)rd["EndTime"]
                    };
                }
            }
        }

        // =================================================================
        //  ChangeStatus
        // =================================================================

        /// <summary>
        /// Opens its own connection and transaction. Throws InvalidOperationException with a
        /// user-friendly message when the change is not allowed.
        /// </summary>
        public void ChangeStatus(int reservationId, string newStatus, string actorRole,
                                 int? actorId, string notes = null, string extraMessage = null)
        {
            // Web Forms used BeginTransaction() (READ COMMITTED); the UPDLOCK read plus the
            // Status = @old compare-and-swap already protect the row, so no SERIALIZABLE here.
            _db.RunInTransaction(IsolationLevel.ReadCommitted, (con, tx) =>
                ChangeStatus(con, tx, reservationId, newStatus, actorRole, actorId, notes, extraMessage));
        }

        /// <summary>
        /// Use inside YOUR transaction. Throws InvalidOperationException so the caller can roll back.
        /// </summary>
        public void ChangeStatus(SqlConnection con, SqlTransaction tx, int reservationId,
                                 string newStatus, string actorRole, int? actorId,
                                 string notes = null, string extraMessage = null)
        {
            if (actorRole != ActorAdmin && actorRole != ActorStudent && actorRole != ActorSystem)
            {
                throw new ArgumentException("Unknown actor role.", "actorRole");
            }
            if (actorRole != ActorSystem && !actorId.HasValue)
            {
                throw new ArgumentException("actorId is required for Admin and Student changes.", "actorId");
            }

            // Read + lock the row so two people cannot change it at the same moment.
            QCUCRAM_ReservationInfo info = ReadInfo(con, tx, reservationId, true);
            if (info == null)
            {
                throw new InvalidOperationException("Reservation #" + reservationId + " was not found.");
            }

            string oldStatus = info.Status;
            if (!CanTransition(oldStatus, newStatus))
            {
                throw new InvalidOperationException(
                    "Reservation #" + reservationId + " is " + oldStatus +
                    " and can't be changed to " + newStatus + ".");
            }

            // 1) Update the reservation (the Status = @old guard makes it a compare-and-swap).
            DateTime now = QCUCRAM_Clock.Now;
            int rows;
            if (newStatus == QCUCRAM_Status.CheckedIn)
            {
                rows = QCUCRAM_Db.Exec(con, tx,
                    "UPDATE Reservations SET Status = @s, CheckedInAt = @now WHERE ReservationID = @id AND Status = @old",
                    QCUCRAM_Db.P("@s", newStatus),
                    QCUCRAM_Db.P("@now", now),
                    QCUCRAM_Db.P("@id", reservationId),
                    QCUCRAM_Db.P("@old", oldStatus));
            }
            else
            {
                rows = QCUCRAM_Db.Exec(con, tx,
                    "UPDATE Reservations SET Status = @s WHERE ReservationID = @id AND Status = @old",
                    QCUCRAM_Db.P("@s", newStatus),
                    QCUCRAM_Db.P("@id", reservationId),
                    QCUCRAM_Db.P("@old", oldStatus));
            }

            if (rows != 1)
            {
                throw new InvalidOperationException(
                    "Reservation #" + reservationId +
                    " was just changed by someone else. Please refresh and try again.");
            }

            info.Status = newStatus;

            // 2) Audit trail
            InsertHistory(con, tx, reservationId, oldStatus, newStatus, actorRole, actorId, notes);

            // 3) Notification for the requester
            string message = BuildMessage(newStatus, info, actorRole, notes, extraMessage);
            InsertNotification(con, tx, info.StudentId, reservationId, message);
        }

        /// <summary>
        /// First history row (NULL -> Pending) plus the "request received" notification.
        /// Call right after inserting a new Pending reservation, inside the same transaction.
        /// </summary>
        public void RecordCreation(SqlConnection con, SqlTransaction tx, int reservationId)
        {
            QCUCRAM_ReservationInfo info = ReadInfo(con, tx, reservationId, false);
            if (info == null)
            {
                throw new InvalidOperationException("Reservation #" + reservationId + " was not found.");
            }

            InsertHistory(con, tx, reservationId, null, QCUCRAM_Status.Pending,
                          ActorStudent, info.StudentId, "Request submitted");

            string message = "Your request #" + reservationId + " (" + info.Summary +
                             ") was received and is awaiting review by the library assistant.";
            InsertNotification(con, tx, info.StudentId, reservationId, message);
        }

        // =================================================================
        //  Notification wording (one place per status)
        // =================================================================

        private string BuildMessage(string newStatus, QCUCRAM_ReservationInfo info,
                                    string actorRole, string notes, string extra)
        {
            string head = "Reservation #" + info.ReservationId + " (" + info.Summary + ")";
            string note = string.IsNullOrWhiteSpace(notes) ? "" : notes.Trim();
            string msg;

            switch (newStatus)
            {
                case QCUCRAM_Status.Approved:
                    msg = head + " was approved. Bring the physical QCID of all members and arrive on time; " +
                          "unclaimed rooms are forfeited after " + GraceMinutes() + " minutes.";
                    break;

                case QCUCRAM_Status.Reassigned:
                    msg = head + " was reassigned to this new room/time." +
                          (note == "" ? "" : " Details: " + note);
                    break;

                case QCUCRAM_Status.Declined:
                    msg = head + " was declined." + (note == "" ? "" : " Reason: " + note);
                    break;

                case QCUCRAM_Status.Cancelled:
                    msg = (actorRole == ActorAdmin)
                        ? head + " was cancelled by the library." + (note == "" ? "" : " Reason: " + note)
                        : "You cancelled " + head + ".";
                    break;

                case QCUCRAM_Status.CheckedIn:
                    msg = "Check-in confirmed for " + head + ". Please keep the room tidy, no eating or drinking.";
                    break;

                case QCUCRAM_Status.Completed:
                    msg = head + " is completed. Thank you for using the collaboration rooms.";
                    break;

                case QCUCRAM_Status.Forfeited:
                    msg = head + " was forfeited because it was not claimed within " +
                          GraceMinutes() + " minutes of the start time.";
                    break;

                default:
                    msg = head + " is now " + newStatus + ".";
                    break;
            }

            if (!string.IsNullOrWhiteSpace(extra)) { msg += " " + extra.Trim(); }

            // Notifications.Message is NVARCHAR(400)
            if (msg.Length > 400) { msg = msg.Substring(0, 397) + "..."; }
            return msg;
        }

        private int GraceMinutes()
        {
            return new QCUCRAM_ContentRepository(_db).GetSettingInt("GraceMinutes", 15);
        }

        // =================================================================
        //  ValidateAndCreate - the Reserve form
        // =================================================================

        /// <summary>
        /// Checks ALL the booking rules, collects every error, and only saves when there are
        /// none. The save is one Serializable transaction with a locking re-check, so a
        /// double-click or two browsers cannot both win the same slot.
        /// </summary>
        public QCUCRAM_CreateResult ValidateAndCreate(QCUCRAM_CreateRequest req)
        {
            QCUCRAM_CreateResult result = new QCUCRAM_CreateResult();
            List<string> errors = result.Errors;
            DateTime now = QCUCRAM_Clock.Now;
            CultureInfo inv = CultureInfo.InvariantCulture;

            if (req == null)
            {
                errors.Add("The reservation request was empty. Please try again.");
                return result;
            }

            // ---- Rule 1: requester exists and is not restricted ----
            QCUCRAM_Student student = _db.QueryFirstOrDefault<QCUCRAM_Student>(
                @"SELECT StudentID, StudentNumber, LastName, FirstName, MI, Email, Program,
                         RequesterType, PasswordHash, NoShowCount, IsRestricted,
                         RestrictionLiftDate, CreatedAt
                    FROM Students WHERE StudentID = @id",
                QCUCRAM_Db.P("@id", req.StudentId));

            if (student == null)
            {
                errors.Add("Your account could not be found. Please log in again.");
                return result;
            }
            if (student.IsRestricted)
            {
                bool stillRestricted = !student.RestrictionLiftDate.HasValue ||
                                       student.RestrictionLiftDate.Value > now;
                if (stillRestricted)
                {
                    string until = !student.RestrictionLiftDate.HasValue
                        ? "further notice"
                        : student.RestrictionLiftDate.Value.ToString("MMM d, yyyy h:mm tt", inv);
                    errors.Add("You are restricted from booking until " + until + ".");
                    return result;
                }
            }

            // ---- Rule 2: the date must be exactly today + AdvanceBookingDays ----
            bool dateOk = req.Date != DateTime.MinValue;
            DateTime date = req.Date.Date;

            if (!dateOk)
            {
                errors.Add("Please choose a valid date.");
            }
            else
            {
                int ahead = new QCUCRAM_ContentRepository(_db).GetSettingInt("AdvanceBookingDays", 1);
                DateTime required = QCUCRAM_Clock.Today.AddDays(ahead);
                if (date != required)
                {
                    string aheadText = ahead == 0 ? "for today only"
                                     : ahead == 1 ? "exactly one day in advance"
                                     : "exactly " + ahead + " days in advance";
                    errors.Add("Reservations must be made " + aheadText + ". Please choose " +
                               required.ToString("dddd, MMM d, yyyy", inv) + ".");
                }
            }

            // ---- Rule 3: room and time slot exist and are active ----
            string roomName = null;
            int minCap = 5, maxCap = 10;

            if (req.RoomId <= 0)
            {
                errors.Add("Please select a room.");
            }
            else
            {
                QCUCRAM_Room room = _db.QueryFirstOrDefault<QCUCRAM_Room>(
                    "SELECT RoomID, RoomName, MinCapacity, MaxCapacity, IsActive FROM Rooms WHERE RoomID = @id",
                    QCUCRAM_Db.P("@id", req.RoomId));

                if (room == null || !room.IsActive)
                {
                    errors.Add("The selected room is not available for booking.");
                }
                else
                {
                    roomName = room.RoomName;
                    minCap = room.MinCapacity;
                    maxCap = room.MaxCapacity;
                }
            }

            bool slotOk = false;
            TimeSpan slotStart = TimeSpan.Zero;

            if (req.TimeSlotId <= 0)
            {
                errors.Add("Please select a time slot.");
            }
            else
            {
                QCUCRAM_TimeSlot slot = _db.QueryFirstOrDefault<QCUCRAM_TimeSlot>(
                    "SELECT TimeSlotID, StartTime, EndTime, IsActive FROM TimeSlots WHERE TimeSlotID = @id",
                    QCUCRAM_Db.P("@id", req.TimeSlotId));

                if (slot == null || !slot.IsActive)
                {
                    errors.Add("The selected time slot is not available for booking.");
                }
                else
                {
                    slotOk = true;
                    slotStart = slot.StartTime;
                }
            }

            if (dateOk && slotOk && date.Add(slotStart) <= now)
            {
                errors.Add("That time slot has already started. Please choose a later slot.");
            }

            // ---- Rule 4: group size, member rows, room capacity ----
            bool sizeOk = req.NumberOfMembers >= 5 && req.NumberOfMembers <= 10;
            if (!sizeOk)
            {
                errors.Add("Group size must be between 5 and 10 members.");
            }
            else if (roomName != null && (req.NumberOfMembers < minCap || req.NumberOfMembers > maxCap))
            {
                errors.Add(roomName + " accommodates " + minCap + " to " + maxCap + " persons only.");
            }

            List<QCUCRAM_MemberInput> members = req.Members ?? new List<QCUCRAM_MemberInput>();
            if (members.Count != req.NumberOfMembers)
            {
                errors.Add("Please fill in exactly " + req.NumberOfMembers + " member rows.");
            }
            else
            {
                for (int i = 0; i < members.Count; i++)
                {
                    QCUCRAM_MemberInput m = members[i];
                    string last = (m.LastName ?? "").Trim();
                    string first = (m.FirstName ?? "").Trim();
                    string mi = (m.MI ?? "").Trim();

                    if (last == "" || first == "")
                    {
                        errors.Add("Member #" + (i + 1) + ": last name and first name are required.");
                    }
                    else if (last.Length > 60 || first.Length > 60 || mi.Length > 5)
                    {
                        errors.Add("Member #" + (i + 1) + ": a name is too long.");
                    }
                }
            }

            // ---- Rule 7: required fields ----
            string purpose = (req.Purpose ?? "").Trim();
            if (purpose == "")
            {
                errors.Add("Please enter the purpose of your reservation.");
            }
            else if (purpose.Length > 300)
            {
                errors.Add("Purpose must be 300 characters or fewer.");
            }

            // ---- Rules 5 + 6: quick, non-locking checks so ALL errors show together ----
            if (dateOk)
            {
                if (roomName != null && slotOk && SlotIsHeld(null, null, req.RoomId, date, req.TimeSlotId, false))
                {
                    errors.Add("That room and time slot is already taken or has a pending request. " +
                               "Please choose another slot (see the availability grid).");
                }

                if (ActiveCountForDay(null, null, req.StudentId, date, false) > 0)
                {
                    errors.Add("You already have an active reservation on " +
                               date.ToString("MMM d, yyyy", inv) +
                               " (maximum 1 hour per day). Cancel it first if you want a different slot.");
                }
            }

            if (errors.Count > 0) { return result; }

            // ---- All rules passed: save everything in ONE transaction ----
            try
            {
                _db.InTransaction<bool>(IsolationLevel.Serializable, (con, tx) =>
                {
                    // Authoritative re-check WITH locks (protects against double-click / two browsers)
                    if (SlotIsHeld(con, tx, req.RoomId, date, req.TimeSlotId, true))
                    {
                        errors.Add("That room and time slot was just taken by someone else. Please choose another slot.");
                        throw new TransactionAbort();
                    }
                    if (ActiveCountForDay(con, tx, req.StudentId, date, true) > 0)
                    {
                        errors.Add("You already have an active reservation on " +
                                   date.ToString("MMM d, yyyy", inv) + " (maximum 1 hour per day).");
                        throw new TransactionAbort();
                    }

                    object idObj = QCUCRAM_Db.Scalar(con, tx,
                        @"INSERT INTO Reservations
                              (StudentID, RoomID, TimeSlotID, ReservationDate, Purpose, NumberOfMembers, Status, CreatedAt)
                          OUTPUT INSERTED.ReservationID
                          VALUES (@s, @r, @t, CAST(@d AS DATE), @p, @n, @st, @at)",
                        QCUCRAM_Db.P("@s", req.StudentId),
                        QCUCRAM_Db.P("@r", req.RoomId),
                        QCUCRAM_Db.P("@t", req.TimeSlotId),
                        QCUCRAM_Db.P("@d", SqlDbType.Date, date),
                        QCUCRAM_Db.P("@p", purpose),
                        QCUCRAM_Db.P("@n", req.NumberOfMembers),
                        QCUCRAM_Db.P("@st", QCUCRAM_Status.Pending),
                        QCUCRAM_Db.P("@at", now));

                    int id = Convert.ToInt32(idObj);

                    foreach (QCUCRAM_MemberInput m in members)
                    {
                        QCUCRAM_Db.Exec(con, tx,
                            @"INSERT INTO ReservationMembers (ReservationID, LastName, FirstName, MI)
                              VALUES (@id, @l, @f, @mi)",
                            QCUCRAM_Db.P("@id", id),
                            QCUCRAM_Db.P("@l", m.LastName.Trim()),
                            QCUCRAM_Db.P("@f", m.FirstName.Trim()),
                            QCUCRAM_Db.P("@mi", string.IsNullOrWhiteSpace(m.MI) ? null : m.MI.Trim()));
                    }

                    // First history row (NULL -> Pending) + "received" notification
                    RecordCreation(con, tx, id);

                    result.ReservationId = id;
                    return true;
                });
            }
            catch (TransactionAbort)
            {
                // errors already collected; the transaction rolled back
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2601 || ex.Number == 2627)
                {
                    errors.Add("That slot was just taken - please choose another.");
                }
                else if (ex.Number == 1205)
                {
                    errors.Add("The system was busy. Please press Submit again.");
                }
                else
                {
                    throw;
                }
            }

            return result;
        }

        // =================================================================
        //  Reassign - move a booking to another room and/or slot, same date
        // =================================================================

        /// <summary>
        /// Throws InvalidOperationException with a friendly message when it cannot be done.
        /// </summary>
        public void Reassign(int reservationId, int newRoomId, int newSlotId, string reason, int adminId)
        {
            string why = (reason ?? "").Trim();
            if (why == "")
            {
                throw new InvalidOperationException("Please enter a reason for the reassignment.");
            }
            if (why.Length > 200)
            {
                throw new InvalidOperationException("The reason must be 200 characters or fewer.");
            }

            try
            {
                _db.RunInTransaction(IsolationLevel.Serializable, (con, tx) =>
                {
                    QCUCRAM_ReservationInfo info = ReadInfo(con, tx, reservationId, true);
                    if (info == null)
                    {
                        throw new InvalidOperationException("Reservation #" + reservationId + " was not found.");
                    }
                    if (!CanTransition(info.Status, QCUCRAM_Status.Reassigned))
                    {
                        throw new InvalidOperationException("Reservation #" + reservationId + " is " +
                            info.Status + " and can't be reassigned.");
                    }

                    DateTime now = QCUCRAM_Clock.Now;
                    if (info.SlotStart <= now)
                    {
                        throw new InvalidOperationException("Reservation #" + reservationId +
                            " has already started and can't be reassigned.");
                    }
                    if (newRoomId == info.RoomId && newSlotId == info.TimeSlotId)
                    {
                        throw new InvalidOperationException("Please choose a different room or time slot.");
                    }

                    // New room: must exist, be active, and fit the group
                    DataTable rm = QCUCRAM_Db.Table(con, tx,
                        "SELECT RoomName, MinCapacity, MaxCapacity, IsActive FROM Rooms WHERE RoomID = @id",
                        QCUCRAM_Db.P("@id", newRoomId));

                    if (rm.Rows.Count == 0 || !Convert.ToBoolean(rm.Rows[0]["IsActive"]))
                    {
                        throw new InvalidOperationException("The selected room is not available.");
                    }

                    string newRoomName = Convert.ToString(rm.Rows[0]["RoomName"]);

                    int memberCount = QCUCRAM_Db.ScalarInt(con, tx,
                        "SELECT NumberOfMembers FROM Reservations WHERE ReservationID = @id",
                        QCUCRAM_Db.P("@id", reservationId));

                    int minCap = Convert.ToInt32(rm.Rows[0]["MinCapacity"]);
                    int maxCap = Convert.ToInt32(rm.Rows[0]["MaxCapacity"]);

                    if (memberCount < minCap || memberCount > maxCap)
                    {
                        throw new InvalidOperationException(newRoomName + " accommodates " + minCap + " to " +
                            maxCap + " persons, but this group has " + memberCount + ".");
                    }

                    // New slot: must exist, be active, and still be in the future
                    DataTable ts = QCUCRAM_Db.Table(con, tx,
                        "SELECT StartTime, EndTime, IsActive FROM TimeSlots WHERE TimeSlotID = @id",
                        QCUCRAM_Db.P("@id", newSlotId));

                    if (ts.Rows.Count == 0 || !Convert.ToBoolean(ts.Rows[0]["IsActive"]))
                    {
                        throw new InvalidOperationException("The selected time slot is not available.");
                    }

                    TimeSpan newStart = (TimeSpan)ts.Rows[0]["StartTime"];
                    TimeSpan newEnd = (TimeSpan)ts.Rows[0]["EndTime"];

                    if (info.Date.Date.Add(newStart) <= now)
                    {
                        throw new InvalidOperationException("That time slot has already started.");
                    }

                    // Is the target free? (locks the range so nobody can grab it meanwhile)
                    if (SlotIsHeld(con, tx, newRoomId, info.Date.Date, newSlotId, true))
                    {
                        throw new InvalidOperationException(
                            "That room and time slot is already taken or has a pending request.");
                    }

                    string note = "Moved from " + info.RoomName + ", " + info.SlotLabel +
                                  " to " + newRoomName + ", " +
                                  QCUCRAM_ReservationInfo.FormatSlot(newStart, newEnd) +
                                  ". Reason: " + why;

                    QCUCRAM_Db.Exec(con, tx,
                        "UPDATE Reservations SET RoomID = @r, TimeSlotID = @t WHERE ReservationID = @id",
                        QCUCRAM_Db.P("@r", newRoomId),
                        QCUCRAM_Db.P("@t", newSlotId),
                        QCUCRAM_Db.P("@id", reservationId));

                    // Status -> Reassigned (+ history row + notification showing the NEW room/time)
                    ChangeStatus(con, tx, reservationId, QCUCRAM_Status.Reassigned,
                                 ActorAdmin, adminId, note);
                });
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2601 || ex.Number == 2627)
                {
                    throw new InvalidOperationException("That room and time slot was just taken by someone else.");
                }
                if (ex.Number == 1205)
                {
                    throw new InvalidOperationException("The system was busy. Please try again.");
                }
                throw;
            }
        }

        // =================================================================
        //  Private locking helpers
        // =================================================================

        /// <summary>
        /// Is the room/date/slot held by an active (not declined/cancelled/forfeited) reservation?
        /// con/tx = null runs a quick check on its own connection.
        /// </summary>
        private static bool SlotIsHeld(SqlConnection con, SqlTransaction tx, int roomId,
                                       DateTime date, int slotId, bool lockRows)
        {
            object n = ScalarAny(con, tx,
                "SELECT COUNT(*) FROM Reservations" + (lockRows ? " WITH (UPDLOCK, HOLDLOCK)" : "") +
                " WHERE RoomID = @r AND ReservationDate = CAST(@d AS DATE) AND TimeSlotID = @t" +
                " AND Status NOT IN " + QCUCRAM_Status.FreeListSql,
                QCUCRAM_Db.P("@r", roomId),
                QCUCRAM_Db.P("@d", SqlDbType.Date, date),
                QCUCRAM_Db.P("@t", slotId));

            return Convert.ToInt32(n) > 0;
        }

        /// <summary>How many active reservations this person already has on that date?</summary>
        private static int ActiveCountForDay(SqlConnection con, SqlTransaction tx, int studentId,
                                             DateTime date, bool lockRows)
        {
            object n = ScalarAny(con, tx,
                "SELECT COUNT(*) FROM Reservations" + (lockRows ? " WITH (UPDLOCK, HOLDLOCK)" : "") +
                " WHERE StudentID = @s AND ReservationDate = CAST(@d AS DATE)" +
                " AND Status NOT IN " + QCUCRAM_Status.FreeListSql,
                QCUCRAM_Db.P("@s", studentId),
                QCUCRAM_Db.P("@d", SqlDbType.Date, date));

            return Convert.ToInt32(n);
        }

        private static object ScalarAny(SqlConnection con, SqlTransaction tx, string sql,
                                        params SqlParameter[] parameters)
        {
            // No connection in scope: let QCUCRAM_Db open and dispose one itself rather
            // than constructing a second SqlConnection here.
            if (con == null)
            {
                return QCUCRAM_Db.Scalar(sql, parameters);
            }
            return QCUCRAM_Db.Scalar(con, tx, sql, parameters);
        }

        private static void InsertHistory(SqlConnection con, SqlTransaction tx, int reservationId,
                                          string oldStatus, string newStatus, string actorRole,
                                          int? actorId, string notes)
        {
            if (notes != null && notes.Length > 300) { notes = notes.Substring(0, 300); }

            QCUCRAM_Db.Exec(con, tx,
                @"INSERT INTO ReservationStatusHistory
                      (ReservationID, OldStatus, NewStatus, ChangedByRole, ChangedBy, ChangedAt, Notes)
                  VALUES (@rid, @old, @new, @role, @by, @at, @notes)",
                QCUCRAM_Db.P("@rid", reservationId),
                QCUCRAM_Db.P("@old", oldStatus),
                QCUCRAM_Db.P("@new", newStatus),
                QCUCRAM_Db.P("@role", actorRole),
                QCUCRAM_Db.P("@by", actorId),
                QCUCRAM_Db.P("@at", QCUCRAM_Clock.Now),
                QCUCRAM_Db.P("@notes", string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()));
        }

        private static void InsertNotification(SqlConnection con, SqlTransaction tx, int studentId,
                                               int reservationId, string message)
        {
            QCUCRAM_Db.Exec(con, tx,
                @"INSERT INTO Notifications (StudentID, ReservationID, Message, IsRead, CreatedAt)
                  VALUES (@sid, @rid, @msg, 0, @at)",
                QCUCRAM_Db.P("@sid", studentId),
                QCUCRAM_Db.P("@rid", reservationId),
                QCUCRAM_Db.P("@msg", message),
                QCUCRAM_Db.P("@at", QCUCRAM_Clock.Now));
        }

        // =================================================================
        //  Lists and lookups
        // =================================================================

        private const string SelectList =
            @"SELECT r.ReservationID, r.StudentID, s.StudentNumber,
                     s.LastName + ', ' + s.FirstName AS StudentName, s.MI,
                     rm.RoomName, r.TimeSlotID, ts.StartTime, ts.EndTime,
                     r.ReservationDate, r.Purpose, r.NumberOfMembers, r.Status,
                     r.CreatedAt, r.CheckedInAt
                FROM Reservations r
                JOIN Students  s  ON s.StudentID  = r.StudentID
                JOIN Rooms     rm ON rm.RoomID    = r.RoomID
                JOIN TimeSlots ts ON ts.TimeSlotID = r.TimeSlotID";

        /// <summary>A student's own bookings, newest date first.</summary>
        public List<QCUCRAM_ReservationListItem> GetForStudent(int studentId, bool includePast = true)
        {
            string sql = SelectList + " WHERE r.StudentID = @id";
            List<SqlParameter> p = new List<SqlParameter> { QCUCRAM_Db.P("@id", studentId) };

            if (!includePast)
            {
                sql += " AND r.ReservationDate >= CAST(@today AS DATE)";
                p.Add(QCUCRAM_Db.P("@today", SqlDbType.Date, QCUCRAM_Clock.Today));
            }

            // Phase 31: total ordering. Two bookings in the same slot on the same day used
            // to come back in whatever order the plan produced, so the paged "My Reservations"
            // list could show the same row on two pages. ReservationID is the tiebreaker the
            // Web Forms page used.
            sql += " ORDER BY r.ReservationDate DESC, ts.StartTime DESC, r.ReservationID DESC";
            return _db.Query<QCUCRAM_ReservationListItem>(sql, p.ToArray());
        }

        /// <summary>The admin Queue page: everything still waiting for a decision.</summary>
        public List<QCUCRAM_ReservationListItem> GetQueue()
        {
            return _db.Query<QCUCRAM_ReservationListItem>(
                SelectList + @" WHERE r.Status IN ('Pending','Approved','Reassigned')
                     AND r.ReservationDate >= CAST(@today AS DATE)
                   ORDER BY r.ReservationDate, ts.StartTime, r.CreatedAt",
                QCUCRAM_Db.P("@today", SqlDbType.Date, QCUCRAM_Clock.Today));
        }

        /// <summary>
        /// Review Queue, first grid: every Pending request, oldest first. The Web
        /// Forms page used ORDER BY CreatedAt, ReservationID and did not filter by
        /// date, so a request whose slot has already started still appears (it can
        /// only be declined).
        /// </summary>
        public List<QCUCRAM_ReservationListItem> GetPending()
        {
            return _db.Query<QCUCRAM_ReservationListItem>(
                SelectList + @" WHERE r.Status = 'Pending'
                   ORDER BY r.CreatedAt, r.ReservationID");
        }

        /// <summary>
        /// Review Queue, second grid: Approved/Reassigned bookings that have not
        /// started yet. Same predicate and order as Queue.aspx.cs BindUpcoming().
        /// </summary>
        public List<QCUCRAM_ReservationListItem> GetUpcomingApproved(DateTime now)
        {
            return _db.Query<QCUCRAM_ReservationListItem>(
                SelectList + @" WHERE r.Status IN ('Approved','Reassigned')
                     AND (r.ReservationDate > CAST(@today AS DATE)
                          OR (r.ReservationDate = CAST(@today AS DATE) AND ts.StartTime > @nowTime))
                   ORDER BY r.ReservationDate, ts.StartTime, rm.RoomName",
                QCUCRAM_Db.P("@today", SqlDbType.Date, now.Date),
                QCUCRAM_Db.P("@nowTime", SqlDbType.Time, now.TimeOfDay));
        }

        /// <summary>Everything booked on one date (Daily Schedule and Print Booking).</summary>
        public List<QCUCRAM_ReservationListItem> GetByDate(DateTime date)
        {
            return _db.Query<QCUCRAM_ReservationListItem>(
                SelectList + @" WHERE r.ReservationDate = CAST(@d AS DATE)
                   ORDER BY ts.StartTime, rm.RoomName",
                QCUCRAM_Db.P("@d", SqlDbType.Date, date.Date));
        }

        /// <summary>
        /// Admin Dashboard: the next five bookings that still hold a slot whose end
        /// time has not passed. Pending requests count too because they hold the
        /// slot; the status badge tells them apart. Same query as Dashboard.aspx.cs.
        /// </summary>
        public List<QCUCRAM_ReservationListItem> Upcoming(DateTime now, int top)
        {
            if (top <= 0) { top = 5; }

            return _db.Query<QCUCRAM_ReservationListItem>(
                @"SELECT TOP (@top) r.ReservationID, r.StudentID, s.StudentNumber,
                         s.LastName + ', ' + s.FirstName AS StudentName, s.MI,
                         rm.RoomName, r.TimeSlotID, ts.StartTime, ts.EndTime,
                         r.ReservationDate, r.Purpose, r.NumberOfMembers, r.Status,
                         r.CreatedAt, r.CheckedInAt
                    FROM Reservations r
                    JOIN Students  s  ON s.StudentID  = r.StudentID
                    JOIN Rooms     rm ON rm.RoomID    = r.RoomID
                    JOIN TimeSlots ts ON ts.TimeSlotID = r.TimeSlotID
                   WHERE r.Status IN ('Pending','Approved','Reassigned')
                     AND CAST(r.ReservationDate AS DATETIME) + CAST(ts.EndTime AS DATETIME) > @now
                   ORDER BY r.ReservationDate, ts.StartTime, r.ReservationID",
                QCUCRAM_Db.P("@top", top),
                QCUCRAM_Db.P("@now", now));
        }

        /// <summary>Admin Records page: filter by date range, status, room and requester.</summary>
        public List<QCUCRAM_ReservationListItem> Search(DateTime? from = null, DateTime? to = null,
                                                        string status = null, int? roomId = null,
                                                        string search = null)
        {
            string sql = SelectList + " WHERE 1 = 1";
            List<SqlParameter> p = new List<SqlParameter>();

            if (from.HasValue)
            {
                sql += " AND r.ReservationDate >= CAST(@from AS DATE)";
                p.Add(QCUCRAM_Db.P("@from", SqlDbType.Date, from.Value.Date));
            }
            if (to.HasValue)
            {
                sql += " AND r.ReservationDate <= CAST(@to AS DATE)";
                p.Add(QCUCRAM_Db.P("@to", SqlDbType.Date, to.Value.Date));
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                sql += " AND r.Status = @status";
                p.Add(QCUCRAM_Db.P("@status", status.Trim()));
            }
            if (roomId.HasValue && roomId.Value > 0)
            {
                sql += " AND r.RoomID = @room";
                p.Add(QCUCRAM_Db.P("@room", roomId.Value));
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                // Records.aspx.cs: wildcards typed by the admin are searched literally.
                string term = search.Trim()
                    .Replace("\\", "\\\\").Replace("%", "\\%")
                    .Replace("_", "\\_").Replace("[", "\\[");

                sql += @" AND (s.StudentNumber LIKE @q ESCAPE '\'
                          OR s.LastName LIKE @q ESCAPE '\'
                          OR s.FirstName LIKE @q ESCAPE '\'
                          OR (s.FirstName + ' ' + s.LastName) LIKE @q ESCAPE '\'
                          OR (s.LastName + ', ' + s.FirstName) LIKE @q ESCAPE '\')";
                p.Add(QCUCRAM_Db.P("@q", "%" + term + "%"));
            }

            // Phase 31: the ORDER BY must be total, not just "mostly right".
            //
            // Records.aspx.cs appended ", r.ReservationDate DESC, r.ReservationID DESC" to
            // every order it built, so two bookings in the same slot always came back in the
            // same order. Without the ReservationID tiebreaker SQL Server is free to return
            // equal-keyed rows in any order, which made the paged Records grid shuffle rows
            // between pages (a row could appear twice, or not at all, while paging).
            sql += " ORDER BY r.ReservationDate DESC, ts.StartTime DESC, r.ReservationID DESC";
            return _db.Query<QCUCRAM_ReservationListItem>(sql, p.ToArray());
        }

        /// <summary>The audit trail for one reservation, oldest first.</summary>
        public List<QCUCRAM_HistoryItem> GetHistory(int reservationId)
        {
            return _db.Query<QCUCRAM_HistoryItem>(
                @"SELECT HistoryID, OldStatus, NewStatus, ChangedByRole, ChangedBy, ChangedAt, Notes
                    FROM ReservationStatusHistory
                   WHERE ReservationID = @id
                   ORDER BY HistoryID",
                QCUCRAM_Db.P("@id", reservationId));
        }

        /// <summary>The group members listed on a reservation.</summary>
        public List<QCUCRAM_ReservationMember> GetMembers(int reservationId)
        {
            return _db.Query<QCUCRAM_ReservationMember>(
                @"SELECT MemberID, ReservationID, LastName, FirstName, MI
                    FROM ReservationMembers
                   WHERE ReservationID = @id
                   ORDER BY MemberID",
                QCUCRAM_Db.P("@id", reservationId));
        }

        /// <summary>
        /// Member names for every booking waiting for check-in on a date, in one query
        /// (CheckIn.aspx.cs LoadMembers). Grouped by ReservationID, MemberID order.
        /// </summary>
        public List<QCUCRAM_ReservationMember> GetMembersForDate(DateTime date)
        {
            return _db.Query<QCUCRAM_ReservationMember>(
                @"SELECT m.MemberID, m.ReservationID, m.LastName, m.FirstName, m.MI
                    FROM ReservationMembers m
                    JOIN Reservations r ON r.ReservationID = m.ReservationID
                   WHERE r.ReservationDate = CAST(@today AS DATE)
                     AND r.Status IN ('Approved','Reassigned')
                   ORDER BY m.ReservationID, m.MemberID",
                QCUCRAM_Db.P("@today", SqlDbType.Date, date));
        }

        /// <summary>
        /// The most recent Forfeited reservation date per student, for the
        /// "One strike away" grid (Restrictions.aspx.cs BindWarning ran this as a
        /// correlated sub-select). Students with no forfeit are absent from the map.
        /// </summary>
        public Dictionary<int, DateTime> GetLastForfeitedDates()
        {
            Dictionary<int, DateTime> map = new Dictionary<int, DateTime>();

            foreach (QCUCRAM_ReservationListItem row in _db.Query<QCUCRAM_ReservationListItem>(
                @"SELECT r.StudentID, MAX(r.ReservationDate) AS ReservationDate
                    FROM Reservations r
                   WHERE r.Status = 'Forfeited'
                   GROUP BY r.StudentID"))
            {
                map[row.StudentId] = row.ReservationDate;
            }

            return map;
        }

        /// <summary>Public non-locking check, for the availability grid and the Reserve page.</summary>
        public bool IsSlotHeld(int roomId, DateTime date, int timeSlotId)
        {
            return SlotIsHeld(null, null, roomId, date, timeSlotId, false);
        }

        /// <summary>Public non-locking count of the student's active bookings on a date.</summary>
        public int CountActiveForDay(int studentId, DateTime date)
        {
            return ActiveCountForDay(null, null, studentId, date, false);
        }

        /// <summary>Reservations still Pending whose slot has already started (the sweep expires these).</summary>
        public List<QCUCRAM_ReservationInfo> GetPendingPastStart(DateTime now)
        {
            return _db.Query<QCUCRAM_ReservationInfo>(
                @"SELECT r.ReservationID, r.StudentID, r.Status, r.RoomID, r.TimeSlotID,
                         r.ReservationDate AS [Date], rm.RoomName, ts.StartTime, ts.EndTime
                    FROM Reservations r
                    JOIN Rooms     rm ON rm.RoomID    = r.RoomID
                    JOIN TimeSlots ts ON ts.TimeSlotID = r.TimeSlotID
                   WHERE r.Status = 'Pending'
                     AND DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00:00' AS TIME), ts.StartTime),
                                 CAST(r.ReservationDate AS DATETIME)) <= @now
                   ORDER BY r.ReservationDate, ts.StartTime",
                QCUCRAM_Db.P("@now", now));
        }

        /// <summary>Approved/Reassigned bookings past their grace window (the sweep forfeits these).</summary>
        public List<QCUCRAM_ReservationInfo> GetUnclaimedPastGrace(DateTime now, int graceMinutes)
        {
            return _db.Query<QCUCRAM_ReservationInfo>(
                @"SELECT r.ReservationID, r.StudentID, r.Status, r.RoomID, r.TimeSlotID,
                         r.ReservationDate AS [Date], rm.RoomName, ts.StartTime, ts.EndTime
                    FROM Reservations r
                    JOIN Rooms     rm ON rm.RoomID    = r.RoomID
                    JOIN TimeSlots ts ON ts.TimeSlotID = r.TimeSlotID
                   WHERE r.Status IN ('Approved','Reassigned')
                     AND r.CheckedInAt IS NULL
                     AND DATEADD(MINUTE, @grace,
                                 DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00:00' AS TIME), ts.StartTime),
                                         CAST(r.ReservationDate AS DATETIME))) < @now
                   ORDER BY r.ReservationDate, ts.StartTime",
                QCUCRAM_Db.P("@grace", graceMinutes),
                QCUCRAM_Db.P("@now", now));
        }

        /// <summary>Checked-in bookings whose slot has ended (the sweep completes these).</summary>
        public List<QCUCRAM_ReservationInfo> GetCheckedInPastEnd(DateTime now)
        {
            return _db.Query<QCUCRAM_ReservationInfo>(
                @"SELECT r.ReservationID, r.StudentID, r.Status, r.RoomID, r.TimeSlotID,
                         r.ReservationDate AS [Date], rm.RoomName, ts.StartTime, ts.EndTime
                    FROM Reservations r
                    JOIN Rooms     rm ON rm.RoomID    = r.RoomID
                    JOIN TimeSlots ts ON ts.TimeSlotID = r.TimeSlotID
                   WHERE r.Status = 'CheckedIn'
                     AND DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00:00' AS TIME), ts.EndTime),
                                 CAST(r.ReservationDate AS DATETIME)) <= @now
                   ORDER BY r.ReservationDate, ts.StartTime",
                QCUCRAM_Db.P("@now", now));
        }
    }
}
