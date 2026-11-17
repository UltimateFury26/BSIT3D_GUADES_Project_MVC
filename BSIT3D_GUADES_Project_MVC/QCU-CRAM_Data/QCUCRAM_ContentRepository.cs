using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data
{
    /// <summary>
    /// Rooms, time slots, Settings and the availability grid - everything the admin
    /// maintains and every page reads.
    /// </summary>
    public class QCUCRAM_ContentRepository
    {
        private readonly QCUCRAM_Db _db;

        public QCUCRAM_ContentRepository() : this(new QCUCRAM_Db()) { }

        public QCUCRAM_ContentRepository(QCUCRAM_Db db)
        {
            if (db == null) { throw new ArgumentNullException("db"); }
            _db = db;
        }

        // =================================================================
        //  Rooms
        // =================================================================

        private const string SelectRoom =
            @"SELECT RoomID, RoomName, MinCapacity, MaxCapacity, IsActive FROM Rooms";

        public List<QCUCRAM_Room> GetRooms(bool activeOnly = false)
        {
            string sql = SelectRoom + (activeOnly ? " WHERE IsActive = 1" : "") + " ORDER BY RoomName";
            return _db.Query<QCUCRAM_Room>(sql);
        }

        public QCUCRAM_Room GetRoom(int roomId)
        {
            return _db.QueryFirstOrDefault<QCUCRAM_Room>(
                SelectRoom + " WHERE RoomID = @id", QCUCRAM_Db.P("@id", roomId));
        }

        /// <summary>0 = OK, 1 = name taken, 3 = invalid input. newId is set on success.</summary>
        public int InsertRoom(string roomName, int minCapacity, int maxCapacity, out int newId)
        {
            newId = 0;
            string name = (roomName ?? "").Trim();

            if (name.Length == 0 || name.Length > 50) { return 3; }
            if (minCapacity < 1 || maxCapacity < minCapacity) { return 3; }

            if (_db.Any("SELECT COUNT(*) AS Total FROM Rooms WHERE RoomName = @n",
                        QCUCRAM_Db.P("@n", name)))
            {
                return 1;
            }

            try
            {
                QCUCRAM_NewIdResult row = _db.QuerySingle<QCUCRAM_NewIdResult>(
                    @"INSERT INTO Rooms (RoomName, MinCapacity, MaxCapacity, IsActive)
                      OUTPUT INSERTED.RoomID AS NewId
                      VALUES (@n, @min, @max, 1)",
                    QCUCRAM_Db.P("@n", name),
                    QCUCRAM_Db.P("@min", minCapacity),
                    QCUCRAM_Db.P("@max", maxCapacity));

                newId = row.NewId;
                return 0;
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2601 || ex.Number == 2627) { return 1; }
                throw;
            }
        }

        /// <summary>0 = OK, 1 = name taken, 2 = not found, 3 = invalid.</summary>
        public int UpdateRoom(int roomId, string roomName, int minCapacity, int maxCapacity, bool isActive)
        {
            string name = (roomName ?? "").Trim();

            if (name.Length == 0 || name.Length > 50) { return 3; }
            if (minCapacity < 1 || maxCapacity < minCapacity) { return 3; }
            if (GetRoom(roomId) == null) { return 2; }

            bool taken = _db.Any(
                "SELECT COUNT(*) AS Total FROM Rooms WHERE RoomName = @n AND RoomID <> @id",
                QCUCRAM_Db.P("@n", name), QCUCRAM_Db.P("@id", roomId));
            if (taken) { return 1; }

            _db.Execute(
                @"UPDATE Rooms
                     SET RoomName = @n, MinCapacity = @min, MaxCapacity = @max, IsActive = @a
                   WHERE RoomID = @id",
                QCUCRAM_Db.P("@n", name),
                QCUCRAM_Db.P("@min", minCapacity),
                QCUCRAM_Db.P("@max", maxCapacity),
                QCUCRAM_Db.P("@a", isActive),
                QCUCRAM_Db.P("@id", roomId));

            return 0;
        }

        public int SetRoomActive(int roomId, bool isActive)
        {
            return _db.Execute("UPDATE Rooms SET IsActive = @a WHERE RoomID = @id",
                QCUCRAM_Db.P("@a", isActive),
                QCUCRAM_Db.P("@id", roomId));
        }

        /// <summary>Returns -1 when the room still has reservations (FK), 0 when it was deleted.</summary>
        public int DeleteRoom(int roomId)
        {
            try
            {
                return _db.Execute("DELETE FROM Rooms WHERE RoomID = @id", QCUCRAM_Db.P("@id", roomId));
            }
            catch (SqlException ex)
            {
                if (ex.Number == 547) { return -1; }
                throw;
            }
        }

        // =================================================================
        //  Time slots
        // =================================================================

        private const string SelectSlot =
            @"SELECT TimeSlotID, StartTime, EndTime, IsActive FROM TimeSlots";

        public List<QCUCRAM_TimeSlot> GetTimeSlots(bool activeOnly = false)
        {
            string sql = SelectSlot + (activeOnly ? " WHERE IsActive = 1" : "") + " ORDER BY StartTime";
            return _db.Query<QCUCRAM_TimeSlot>(sql);
        }

        public QCUCRAM_TimeSlot GetTimeSlot(int timeSlotId)
        {
            return _db.QueryFirstOrDefault<QCUCRAM_TimeSlot>(
                SelectSlot + " WHERE TimeSlotID = @id", QCUCRAM_Db.P("@id", timeSlotId));
        }

        /// <summary>"9:00 AM - 10:00 AM" for a slot id; empty when the slot is gone.</summary>
        public string GetSlotLabel(int timeSlotId)
        {
            QCUCRAM_TimeSlot slot = GetTimeSlot(timeSlotId);
            return slot == null ? "" : QCUCRAM_ReservationInfo.FormatSlot(slot.StartTime, slot.EndTime);
        }

        /// <summary>0 = OK, 1 = start time taken, 3 = invalid (end must be after start).</summary>
        public int InsertTimeSlot(TimeSpan startTime, TimeSpan endTime, out int newId)
        {
            newId = 0;
            if (endTime <= startTime) { return 3; }

            if (_db.Any("SELECT COUNT(*) AS Total FROM TimeSlots WHERE StartTime = @s",
                        QCUCRAM_Db.P("@s", SqlDbType.Time, startTime)))
            {
                return 1;
            }

            try
            {
                QCUCRAM_NewIdResult row = _db.QuerySingle<QCUCRAM_NewIdResult>(
                    @"INSERT INTO TimeSlots (StartTime, EndTime, IsActive)
                      OUTPUT INSERTED.TimeSlotID AS NewId
                      VALUES (@s, @e, 1)",
                    QCUCRAM_Db.P("@s", SqlDbType.Time, startTime),
                    QCUCRAM_Db.P("@e", SqlDbType.Time, endTime));

                newId = row.NewId;
                return 0;
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2601 || ex.Number == 2627) { return 1; }
                throw;
            }
        }

        /// <summary>0 = OK, 1 = start time taken, 2 = not found, 3 = invalid.</summary>
        public int UpdateTimeSlot(int timeSlotId, TimeSpan startTime, TimeSpan endTime, bool isActive)
        {
            if (endTime <= startTime) { return 3; }
            if (GetTimeSlot(timeSlotId) == null) { return 2; }

            bool taken = _db.Any(
                "SELECT COUNT(*) AS Total FROM TimeSlots WHERE StartTime = @s AND TimeSlotID <> @id",
                QCUCRAM_Db.P("@s", SqlDbType.Time, startTime),
                QCUCRAM_Db.P("@id", timeSlotId));
            if (taken) { return 1; }

            _db.Execute(
                @"UPDATE TimeSlots SET StartTime = @s, EndTime = @e, IsActive = @a
                   WHERE TimeSlotID = @id",
                QCUCRAM_Db.P("@s", SqlDbType.Time, startTime),
                QCUCRAM_Db.P("@e", SqlDbType.Time, endTime),
                QCUCRAM_Db.P("@a", isActive),
                QCUCRAM_Db.P("@id", timeSlotId));

            return 0;
        }

        public int SetTimeSlotActive(int timeSlotId, bool isActive)
        {
            return _db.Execute("UPDATE TimeSlots SET IsActive = @a WHERE TimeSlotID = @id",
                QCUCRAM_Db.P("@a", isActive),
                QCUCRAM_Db.P("@id", timeSlotId));
        }

        /// <summary>Returns -1 when the slot is still used by a reservation.</summary>
        public int DeleteTimeSlot(int timeSlotId)
        {
            try
            {
                return _db.Execute("DELETE FROM TimeSlots WHERE TimeSlotID = @id",
                    QCUCRAM_Db.P("@id", timeSlotId));
            }
            catch (SqlException ex)
            {
                if (ex.Number == 547) { return -1; }
                throw;
            }
        }

        // =================================================================
        //  Settings
        // =================================================================

        public List<QCUCRAM_Setting> GetAllSettings()
        {
            return _db.Query<QCUCRAM_Setting>(
                "SELECT SettingKey, SettingValue, Description FROM Settings ORDER BY SettingKey");
        }

        public string GetSetting(string key, string defaultValue = null)
        {
            QCUCRAM_Setting row = _db.QueryFirstOrDefault<QCUCRAM_Setting>(
                "SELECT SettingKey, SettingValue, Description FROM Settings WHERE SettingKey = @k",
                QCUCRAM_Db.P("@k", key));

            return (row == null || row.SettingValue == null) ? defaultValue : row.SettingValue;
        }

        public int GetSettingInt(string key, int defaultValue = 0)
        {
            int parsed;
            return int.TryParse(GetSetting(key), out parsed) ? parsed : defaultValue;
        }

        public bool GetSettingBool(string key, bool defaultValue = false)
        {
            bool parsed;
            return bool.TryParse(GetSetting(key), out parsed) ? parsed : defaultValue;
        }

        /// <summary>
        /// UPDATE first, INSERT when the key does not exist yet (port of SettingsHelper.Set).
        /// Clears the cached clock offset so a DevTimeOffsetMinutes change takes effect at once.
        /// </summary>
        public void SetSetting(string key, string value)
        {
            int rows = _db.Execute(
                "UPDATE Settings SET SettingValue = @v WHERE SettingKey = @k",
                QCUCRAM_Db.P("@v", value),
                QCUCRAM_Db.P("@k", key));

            if (rows == 0)
            {
                _db.Execute(
                    "INSERT INTO Settings (SettingKey, SettingValue) VALUES (@k, @v)",
                    QCUCRAM_Db.P("@k", key),
                    QCUCRAM_Db.P("@v", value));
            }

            QCUCRAM_Clock.Invalidate();
        }

        public int DeleteSetting(string key)
        {
            int rows = _db.Execute("DELETE FROM Settings WHERE SettingKey = @k",
                QCUCRAM_Db.P("@k", key));
            QCUCRAM_Clock.Invalidate();
            return rows;
        }

        /// <summary>
        /// The booking-rules HTML, built from Settings and the active time slots.
        /// ONE source of wording for the Availability pop-up and the Reserve card, so the
        /// two can never drift apart. Port of SettingsHelper.BuildRulesHtml.
        /// </summary>
        public string BuildRulesHtml()
        {
            int ahead = GetSettingInt("AdvanceBookingDays", 1);
            int grace = GetSettingInt("GraceMinutes", 15);
            int threshold = Math.Max(1, GetSettingInt("NoShowThreshold", 3));
            int restrictDays = GetSettingInt("RestrictionDays", 7);

            string aheadText = ahead <= 0
                ? "Reserve for <strong>today only</strong>."
                : ahead == 1
                    ? "<strong>Reserve one day in advance only.</strong> Bookings are made for tomorrow; same-day requests are not accepted."
                    : "<strong>Reserve exactly " + ahead + " days in advance.</strong> Same-day requests are not accepted.";

            string slotText = "Time slots are set by the library.";
            try
            {
                List<QCUCRAM_TimeSlot> slots = GetTimeSlots(true);
                if (slots.Count > 0)
                {
                    TimeSpan first = slots[0].StartTime;
                    TimeSpan last = slots[0].EndTime;
                    foreach (QCUCRAM_TimeSlot s in slots)
                    {
                        if (s.StartTime < first) { first = s.StartTime; }
                        if (s.EndTime > last) { last = s.EndTime; }
                    }

                    DateTime d = DateTime.Today;
                    string from = d.Add(first).ToString("h:mm tt", CultureInfo.InvariantCulture);
                    string to = d.Add(last).ToString("h:mm tt", CultureInfo.InvariantCulture);
                    int n = slots.Count;

                    slotText = "<strong>Time slots:</strong> " + n + " slot" + (n == 1 ? "" : "s") +
                               " from " + from + " to " + to +
                               ". Collaboration rooms are at the San Bartolome branch.";
                }
            }
            catch
            {
                // The rules must still render if the slot lookup fails.
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("<ol class=\"qcu-rules-list\">");
            sb.Append("<li>").Append(aheadText).Append("</li>");
            sb.Append("<li><strong>Bring the physical QCID of all members.</strong> The library assistant verifies it when you claim the room.</li>");
            sb.Append("<li><strong>Claim within ").Append(grace).Append(" minutes</strong> of the slot start, or the booking is forfeited.</li>");
            sb.Append("<li><strong>Maximum of 1 hour per day</strong> per requester (one active reservation per date).</li>");
            sb.Append("<li><strong>Group size: 5 to 10 members.</strong> Every member's name must be listed.</li>");
            sb.Append("<li>").Append(slotText).Append("</li>");
            sb.Append("<li><strong>No eating or drinking</strong> inside the collaboration rooms.</li>");
            sb.Append("<li><strong>Pending requests hold the slot</strong> and must be reviewed by the library assistant before the slot starts, or they expire.</li>");
            sb.Append("<li><strong>Cancelling</strong> before the slot starts is not a no-show.</li>");
            sb.Append("<li><strong>No-shows:</strong> after ").Append(threshold).Append(" forfeiture")
              .Append(threshold == 1 ? "" : "s")
              .Append(" you cannot book for ").Append(restrictDays).Append(" day")
              .Append(restrictDays == 1 ? "" : "s")
              .Append(". The restriction lifts automatically afterwards and your strikes reset.</li>");
            sb.Append("</ol>");
            return sb.ToString();
        }

        // =================================================================
        //  Availability grid
        // =================================================================

        /// <summary>
        /// Every held slot on a date, for the grid. Only "active" reservations count,
        /// which is exactly the definition UX_Res_ActiveSlot enforces.
        /// </summary>
        public List<QCUCRAM_AvailabilityCell> GetAvailability(DateTime date)
        {
            return _db.Query<QCUCRAM_AvailabilityCell>(
                @"SELECT RoomID AS RoomId, TimeSlotID AS TimeSlotId,
                         ReservationID AS ReservationId, Status
                    FROM Reservations
                   WHERE ReservationDate = CAST(@d AS DATE)
                     AND Status NOT IN " + QCUCRAM_Status.FreeListSql,
                QCUCRAM_Db.P("@d", SqlDbType.Date, date.Date));
        }

        /// <summary>
        /// True when that room + slot is already held on that date. Pass ignoreReservationId
        /// to exclude one row (used by Reassign so a booking does not block itself).
        /// </summary>
        public bool IsSlotTaken(DateTime date, int roomId, int timeSlotId, int? ignoreReservationId = null)
        {
            string sql =
                @"SELECT COUNT(*) AS Total FROM Reservations
                   WHERE RoomID = @r AND TimeSlotID = @t
                     AND ReservationDate = CAST(@d AS DATE)
                     AND Status NOT IN " + QCUCRAM_Status.FreeListSql;

            List<SqlParameter> p = new List<SqlParameter>
            {
                QCUCRAM_Db.P("@r", roomId),
                QCUCRAM_Db.P("@t", timeSlotId),
                QCUCRAM_Db.P("@d", SqlDbType.Date, date.Date)
            };

            if (ignoreReservationId.HasValue)
            {
                sql += " AND ReservationID <> @ignore";
                p.Add(QCUCRAM_Db.P("@ignore", ignoreReservationId.Value));
            }

            return _db.Count(sql, p.ToArray()) > 0;
        }

        /// <summary>How many slots are held on a date (dashboard "occupancy" card).</summary>
        public int CountTakenSlots(DateTime date)
        {
            return _db.Count(
                @"SELECT COUNT(*) AS Total FROM Reservations
                   WHERE ReservationDate = CAST(@d AS DATE)
                     AND Status NOT IN " + QCUCRAM_Status.FreeListSql,
                QCUCRAM_Db.P("@d", SqlDbType.Date, date.Date));
        }
    }
}
