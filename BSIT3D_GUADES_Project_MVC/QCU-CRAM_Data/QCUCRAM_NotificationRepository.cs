using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data
{
    /// <summary>
    /// Port of the Notifications page data access (Guades_QCUCram_Notifications.aspx.cs)
    /// plus the insert helper the reservation service needs.
    ///
    /// Every read/write is scoped by StudentID, so a student can never touch
    /// somebody else's row even if they tamper with the id in the URL.
    /// </summary>
    public class QCUCRAM_NotificationRepository : IDisposable
    {
        private readonly QCUCRAM_Db _db;

        public QCUCRAM_NotificationRepository() : this(new QCUCRAM_Db()) { }

        public QCUCRAM_NotificationRepository(QCUCRAM_Db db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        /// <summary>
        /// Releases the EF context this repository opened. The default constructor
        /// creates its own QCUCRAM_Db, so without this the SqlConnection behind it
        /// would only come back to the pool on finalisation - a slow leak under load.
        /// DbContext.Dispose is idempotent, so disposing an injected db twice is safe.
        /// </summary>
        public void Dispose()
        {
            if (_db != null) { _db.Dispose(); }
        }

        // =================================================================
        //  Reads
        // =================================================================

        /// <summary>A student's inbox, newest first. Pass unreadOnly for the filter dropdown.</summary>
        public List<QCUCRAM_NotificationItem> GetForStudent(int studentId, bool unreadOnly = false)
        {
            string sql =
                @"SELECT NotificationID, ReservationID, Message, IsRead, CreatedAt
                    FROM Notifications
                   WHERE StudentID = @sid";

            if (unreadOnly) sql += " AND IsRead = 0";

            sql += " ORDER BY CreatedAt DESC, NotificationID DESC";

            return _db.Query<QCUCRAM_NotificationItem>(sql, QCUCRAM_Db.P("@sid", studentId));
        }

        /// <summary>How many unread rows the navbar badge should show.</summary>
        public int CountUnread(int studentId)
        {
            return _db.Count(
                "SELECT COUNT(*) AS Total FROM Notifications WHERE StudentID = @sid AND IsRead = 0",
                QCUCRAM_Db.P("@sid", studentId));
        }

        /// <summary>Total rows, used by the "x unread out of y" summary line.</summary>
        public int CountAll(int studentId)
        {
            return _db.Count(
                "SELECT COUNT(*) AS Total FROM Notifications WHERE StudentID = @sid",
                QCUCRAM_Db.P("@sid", studentId));
        }

        /// <summary>One row, scoped to its owner. Returns null when it is not theirs.</summary>
        public QCUCRAM_NotificationItem GetById(int notificationId, int studentId)
        {
            return _db.QueryFirstOrDefault<QCUCRAM_NotificationItem>(
                @"SELECT NotificationID, ReservationID, Message, IsRead, CreatedAt
                    FROM Notifications
                   WHERE NotificationID = @id AND StudentID = @sid",
                QCUCRAM_Db.P("@id", notificationId),
                QCUCRAM_Db.P("@sid", studentId));
        }

        // =================================================================
        //  Writes
        // =================================================================

        /// <summary>
        /// Marks one row read. The StudentID condition is the ownership check.
        /// Returns 1 when a row changed, 0 when it was already read or not theirs.
        /// </summary>
        public int MarkRead(int notificationId, int studentId)
        {
            return _db.Execute(
                "UPDATE Notifications SET IsRead = 1 WHERE NotificationID = @id AND StudentID = @sid",
                QCUCRAM_Db.P("@id", notificationId),
                QCUCRAM_Db.P("@sid", studentId));
        }

        /// <summary>Marks every unread row read. Returns how many changed.</summary>
        public int MarkAllRead(int studentId)
        {
            return _db.Execute(
                "UPDATE Notifications SET IsRead = 1 WHERE StudentID = @sid AND IsRead = 0",
                QCUCRAM_Db.P("@sid", studentId));
        }

        /// <summary>
        /// Inserts a notification on its own connection. The reservation service
        /// does NOT use this overload - it calls the transaction-aware one so the
        /// message commits or rolls back with the status change.
        /// </summary>
        public int Insert(int studentId, int? reservationId, string message)
        {
            return _db.Execute(
                @"INSERT INTO Notifications (StudentID, ReservationID, Message, IsRead, CreatedAt)
                  VALUES (@sid, @rid, @msg, 0, @at)",
                QCUCRAM_Db.P("@sid", studentId),
                QCUCRAM_Db.P("@rid", reservationId),
                QCUCRAM_Db.P("@msg", message),
                QCUCRAM_Db.P("@at", QCUCRAM_Clock.Now));
        }

        /// <summary>Transaction-aware insert, for callers already inside a status change.</summary>
        public static int Insert(SqlConnection con, SqlTransaction tx, int studentId,
                                 int? reservationId, string message, DateTime at)
        {
            return QCUCRAM_Db.Exec(con, tx,
                @"INSERT INTO Notifications (StudentID, ReservationID, Message, IsRead, CreatedAt)
                  VALUES (@sid, @rid, @msg, 0, @at)",
                QCUCRAM_Db.P("@sid", studentId),
                QCUCRAM_Db.P("@rid", reservationId),
                QCUCRAM_Db.P("@msg", message),
                QCUCRAM_Db.P("@at", at));
        }

        /// <summary>Deletes one row, scoped to its owner.</summary>
        public int Delete(int notificationId, int studentId)
        {
            return _db.Execute(
                "DELETE FROM Notifications WHERE NotificationID = @id AND StudentID = @sid",
                QCUCRAM_Db.P("@id", notificationId),
                QCUCRAM_Db.P("@sid", studentId));
        }

        /// <summary>Clears a student's inbox (used when an account is reset).</summary>
        public int DeleteAll(int studentId)
        {
            return _db.Execute(
                "DELETE FROM Notifications WHERE StudentID = @sid",
                QCUCRAM_Db.P("@sid", studentId));
        }
    }
}
