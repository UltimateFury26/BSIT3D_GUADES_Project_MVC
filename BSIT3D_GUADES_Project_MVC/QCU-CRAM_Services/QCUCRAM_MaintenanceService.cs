using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Services
{
    /// <summary>
    /// The four automatic rules of the original application, ported one-for-one from
    /// Guades_QCUCram_MaintenanceService. Safe to call from several requests at once.
    ///
    ///   1. a Pending request whose slot has started            -> Declined   ("Expired")
    ///   2. an Approved/Reassigned booking not checked in within
    ///      GraceMinutes of the slot start                       -> Forfeited  (+1 strike,
    ///                                                              restricted at the threshold)
    ///   3. a CheckedIn booking whose slot has ended             -> Completed
    ///   4. a restriction whose RestrictionLiftDate has passed   -> lifted, strikes reset
    ///
    /// Order matters and is the same as the original: expire, forfeit, complete, lift.
    /// </summary>
    public static class QCUCRAM_MaintenanceService
    {
        // ------------------------------------------------------------------
        //  Result object
        // ------------------------------------------------------------------

        public class SweepResult
        {
            public bool Skipped { get; set; }
            public int Expired { get; set; }
            public int Forfeited { get; set; }
            public int NewlyRestricted { get; set; }
            public int Completed { get; set; }
            public int Lifted { get; set; }
            public int Errors { get; set; }
            public DateTime RanAt { get; set; }

            public bool ChangedAnything
            {
                get { return Expired + Forfeited + Completed + Lifted > 0; }
            }

            public string Summary
            {
                get
                {
                    if (Skipped) { return "Another sweep is already running, so this one was skipped."; }

                    string s = "Sweep finished at " +
                               RanAt.ToString("h:mm:ss tt", CultureInfo.InvariantCulture) + ": " +
                               Expired + " pending request(s) expired, " +
                               Forfeited + " reservation(s) forfeited" +
                               (NewlyRestricted > 0 ? " (" + NewlyRestricted + " student(s) newly restricted)" : string.Empty) + ", " +
                               Completed + " completed, " +
                               Lifted + " restriction(s) lifted.";

                    if (Errors > 0) { s += " " + Errors + " error(s) occurred - see the trace log."; }
                    return s;
                }
            }
        }

        // Only one sweep at a time inside this web application.
        private static readonly object Gate = new object();
        private static long _lastRunTicks;

        /// <summary>Real (not simulated) time the last sweep finished; null if none yet.</summary>
        public static DateTime? LastRun
        {
            get
            {
                long t = Interlocked.Read(ref _lastRunTicks);
                return t == 0 ? (DateTime?)null : new DateTime(t);
            }
        }

        // ------------------------------------------------------------------
        //  The sweep
        // ------------------------------------------------------------------

        public static SweepResult RunSweep()
        {
            SweepResult result = new SweepResult();

            if (!Monitor.TryEnter(Gate))
            {
                result.Skipped = true;
                return result;
            }

            try
            {
                DateTime now = QCUCRAM_Clock.Now;
                result.RanAt = now;

                QCUCRAM_ContentRepository content = new QCUCRAM_ContentRepository();
                int grace = content.GetSettingInt("GraceMinutes", 15);
                int threshold = Math.Max(1, content.GetSettingInt("NoShowThreshold", 3));
                int restrictDays = content.GetSettingInt("RestrictionDays", 7);

                ExpirePending(now, result);
                ForfeitUnclaimed(now, grace, threshold, restrictDays, result);
                CompleteFinished(now, result);
                LiftExpiredRestrictions(now, result);

                Interlocked.Exchange(ref _lastRunTicks, DateTime.Now.Ticks);
            }
            finally
            {
                Monitor.Exit(Gate);
            }

            return result;
        }

        // ---- rule 1: expire pending requests ----
        private static void ExpirePending(DateTime now, SweepResult result)
        {
            List<QCUCRAM_ReservationInfo> due = new QCUCRAM_ReservationRepository().GetPendingPastStart(now);

            foreach (QCUCRAM_ReservationInfo info in due)
            {
                try
                {
                    new QCUCRAM_ReservationRepository().ChangeStatus(
                        info.ReservationId, QCUCRAM_Status.Declined,
                        QCUCRAM_ReservationRepository.ActorSystem, null,
                        "Expired - not reviewed before the slot start time.");

                    result.Expired++;
                }
                catch (InvalidOperationException)
                {
                    // Someone (an admin or the student) changed it first - nothing to do.
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    Trace.TraceError("Sweep/expire #" + info.ReservationId + ": " + ex);
                }
            }
        }

        // ---- rule 2: forfeit unclaimed bookings ----
        private static void ForfeitUnclaimed(DateTime now, int grace, int threshold, int restrictDays, SweepResult result)
        {
            List<QCUCRAM_ReservationInfo> due =
                new QCUCRAM_ReservationRepository().GetUnclaimedPastGrace(now, grace);

            foreach (QCUCRAM_ReservationInfo info in due)
            {
                try
                {
                    int outcome = ForfeitOne(info.ReservationId, now, threshold, restrictDays);
                    if (outcome >= 1) { result.Forfeited++; }
                    if (outcome == 2) { result.NewlyRestricted++; }
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    Trace.TraceError("Sweep/forfeit #" + info.ReservationId + ": " + ex);
                }
            }
        }

        /// <summary>0 = skipped (already handled), 1 = forfeited, 2 = forfeited AND newly restricted.</summary>
        private static int ForfeitOne(int reservationId, DateTime now, int threshold, int restrictDays)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            using (SqlConnection con = db.OpenConnection())
            using (SqlTransaction tx = con.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    // Lock the reservation and make sure it is still unclaimed.
                    object sidObj = QCUCRAM_Db.Scalar(con, tx,
                        @"SELECT StudentID FROM Reservations WITH (UPDLOCK, ROWLOCK)
                           WHERE ReservationID = @id AND Status IN ('Approved','Reassigned') AND CheckedInAt IS NULL",
                        QCUCRAM_Db.P("@id", reservationId));

                    if (sidObj == null || sidObj == DBNull.Value)
                    {
                        QCUCRAM_Db.SafeRollback(tx);
                        return 0;
                    }

                    int studentId = Convert.ToInt32(sidObj);

                    // Add the strike.
                    QCUCRAM_Db.Exec(con, tx,
                        "UPDATE Students SET NoShowCount = NoShowCount + 1 WHERE StudentID = @s",
                        QCUCRAM_Db.P("@s", studentId));

                    int strikes = QCUCRAM_Db.ScalarInt(con, tx,
                        "SELECT NoShowCount FROM Students WHERE StudentID = @s",
                        QCUCRAM_Db.P("@s", studentId));

                    object restrictedObj = QCUCRAM_Db.Scalar(con, tx,
                        "SELECT IsRestricted FROM Students WHERE StudentID = @s",
                        QCUCRAM_Db.P("@s", studentId));

                    bool wasRestricted = restrictedObj != null && restrictedObj != DBNull.Value
                                         && Convert.ToBoolean(restrictedObj);

                    // Threshold reached -> restrict.
                    bool restrictNow = !wasRestricted && strikes >= threshold;
                    DateTime lift = now.AddDays(restrictDays);

                    if (restrictNow)
                    {
                        QCUCRAM_Db.Exec(con, tx,
                            "UPDATE Students SET IsRestricted = 1, RestrictionLiftDate = @lift WHERE StudentID = @s",
                            QCUCRAM_Db.P("@lift", lift),
                            QCUCRAM_Db.P("@s", studentId));
                    }

                    string extra = strikes <= threshold
                        ? "Strike " + strikes + " of " + threshold + "."
                        : "Total strikes: " + strikes + ".";

                    if (restrictNow)
                    {
                        extra += " You are now restricted from booking until " +
                                 lift.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture) + ".";
                    }

                    // Status change + history + notification (also verifies the transition is allowed).
                    new QCUCRAM_ReservationRepository(db).ChangeStatus(
                        con, tx, reservationId, QCUCRAM_Status.Forfeited,
                        QCUCRAM_ReservationRepository.ActorSystem, null,
                        "Not checked in within the grace period", extra);

                    tx.Commit();
                    return restrictNow ? 2 : 1;
                }
                catch (InvalidOperationException)
                {
                    QCUCRAM_Db.SafeRollback(tx);   // state changed under us: undo the strike too
                    return 0;
                }
                catch
                {
                    QCUCRAM_Db.SafeRollback(tx);
                    throw;
                }
            }
        }

        // ---- rule 3: complete finished sessions ----
        private static void CompleteFinished(DateTime now, SweepResult result)
        {
            List<QCUCRAM_ReservationInfo> due = new QCUCRAM_ReservationRepository().GetCheckedInPastEnd(now);

            foreach (QCUCRAM_ReservationInfo info in due)
            {
                try
                {
                    new QCUCRAM_ReservationRepository().ChangeStatus(
                        info.ReservationId, QCUCRAM_Status.Completed,
                        QCUCRAM_ReservationRepository.ActorSystem, null,
                        "Slot ended");

                    result.Completed++;
                }
                catch (InvalidOperationException)
                {
                    // Already handled elsewhere.
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    Trace.TraceError("Sweep/complete #" + info.ReservationId + ": " + ex);
                }
            }
        }

        // ---- rule 4: lift expired restrictions ----
        private static void LiftExpiredRestrictions(DateTime now, SweepResult result)
        {
            List<QCUCRAM_Student> due = new QCUCRAM_StudentRepository().GetExpiredRestrictions(now);

            foreach (QCUCRAM_Student student in due)
            {
                try
                {
                    if (LiftOne(student.StudentID, now)) { result.Lifted++; }
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    Trace.TraceError("Sweep/lift student " + student.StudentID + ": " + ex);
                }
            }
        }

        private static bool LiftOne(int studentId, DateTime now)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            using (SqlConnection con = db.OpenConnection())
            using (SqlTransaction tx = con.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    int rows = QCUCRAM_Db.Exec(con, tx,
                        @"UPDATE Students
                             SET IsRestricted = 0, NoShowCount = 0, RestrictionLiftDate = NULL
                           WHERE StudentID = @s AND IsRestricted = 1
                             AND RestrictionLiftDate IS NOT NULL AND RestrictionLiftDate <= @now",
                        QCUCRAM_Db.P("@s", studentId),
                        QCUCRAM_Db.P("@now", now));

                    if (rows != 1)
                    {
                        QCUCRAM_Db.SafeRollback(tx);
                        return false;
                    }

                    QCUCRAM_NotificationRepository.Insert(con, tx, studentId, null,
                        "Your booking restriction has ended and your strike count was reset. You can reserve rooms again.",
                        now);

                    tx.Commit();
                    return true;
                }
                catch
                {
                    QCUCRAM_Db.SafeRollback(tx);
                    throw;
                }
            }
        }

        // ------------------------------------------------------------------
        //  Triggers
        // ------------------------------------------------------------------

        private static Timer _timer;
        private static readonly object TimerLock = new object();

        /// <summary>Runs the sweep only if the last one finished more than minSeconds ago.</summary>
        public static SweepResult RunIfDue(int minSeconds)
        {
            DateTime? last = LastRun;
            if (last.HasValue && (DateTime.Now - last.Value).TotalSeconds < minSeconds) { return null; }
            return RunSweep();
        }

        /// <summary>Same, but NEVER throws: a sweep problem must not break the page the user asked for.</summary>
        public static void TryRunIfDue(int minSeconds)
        {
            try
            {
                RunIfDue(minSeconds);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Automatic sweep failed: " + ex);
            }
        }

        /// <summary>Background timer: first run after 5 seconds, then every 'everySeconds'.</summary>
        public static void StartBackgroundTimer(int everySeconds)
        {
            lock (TimerLock)
            {
                if (_timer != null) { return; }   // already running

                int period = Math.Max(10, everySeconds) * 1000;
                int minGap = Math.Max(5, everySeconds / 2);

                _timer = new Timer(state => TryRunIfDue(minGap), null, 5000, period);
            }
        }

        public static void StopBackgroundTimer()
        {
            lock (TimerLock)
            {
                if (_timer == null) { return; }
                _timer.Dispose();
                _timer = null;
            }
        }
    }
}
