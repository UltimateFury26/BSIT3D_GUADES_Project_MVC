using System;
using System.Configuration;
using System.Globalization;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Services
{
    /// <summary>
    /// Phase 08 - the Developer Tools screen.
    ///
    /// Everything here exists for demonstration and grading only. In a real
    /// deployment set QCU-CRAM_DevToolsEnabled to false in Web.config and the
    /// controller will answer 404.
    /// </summary>
    public static class QCUCRAMDevToolsService
    {
        public const string OffsetKey = "DevTimeOffsetMinutes";
        public const int MaxOffset = 1000000;

        /// <summary>Reads QCU-CRAM_DevToolsEnabled from Web.config (default true).</summary>
        public static bool Enabled
        {
            get
            {
                string raw = ConfigurationManager.AppSettings["QCU-CRAM_DevToolsEnabled"];
                bool value;
                if (bool.TryParse(raw, out value)) { return value; }
                return true;
            }
        }

        public static QCUCRAMDevToolsViewModel Load()
        {
            int offset = 0;

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                offset = new QCUCRAM_ContentRepository(db).GetSettingInt(OffsetKey, 0);
            }

            DateTime real = DateTime.Now;

            QCUCRAMDevToolsViewModel vm = new QCUCRAMDevToolsViewModel
            {
                RealNow = real,
                SimulatedNow = real.AddMinutes(offset),
                OffsetMinutes = offset
            };

            QCUCRAM_SampleData.Counts counts = QCUCRAM_SampleData.GetCounts();

            vm.Students = counts.Students;
            vm.SampleStudents = counts.SampleStudents;
            vm.SampleRestricted = counts.SampleRestricted;
            vm.SampleOneStrikeAway = counts.SampleOneStrikeAway;
            vm.Reservations = counts.Reservations;
            vm.SampleReservations = counts.SampleReservations;
            vm.Notifications = counts.Notifications;

            return vm;
        }

        public static string ShiftClock(string command)
        {
            if (string.Equals(command, "reset", StringComparison.OrdinalIgnoreCase))
            {
                SetOffset(0);
                return "Clock reset to real time.";
            }

            int delta;
            if (!int.TryParse(command, NumberStyles.Integer,
                              CultureInfo.InvariantCulture, out delta))
            {
                return "Unknown clock command.";
            }

            int current = 0;
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                current = new QCUCRAM_ContentRepository(db).GetSettingInt(OffsetKey, 0);
            }

            long next = (long)current + delta;
            if (next < 0) { next = 0; }
            if (next > MaxOffset) { next = MaxOffset; }

            SetOffset((int)next);
            return "Clock moved. The offset is now " + next + " minute(s).";
        }

        private static void SetOffset(int minutes)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                new QCUCRAM_ContentRepository(db).SetSetting(
                    OffsetKey, minutes.ToString(CultureInfo.InvariantCulture));
            }

            QCUCRAM_Clock.Invalidate();
        }

        public static string RunSweep()
        {
            try
            {
                QCUCRAM_MaintenanceService.SweepResult result =
                    QCUCRAM_MaintenanceService.RunSweep();
                return result.Summary;
            }
            catch (Exception ex)
            {
                return "The sweep failed: " + ex.Message;
            }
        }

        public static string GenerateStudents(int adminId)
        {
            try
            {
                QCUCRAM_SampleData.StudentBatch batch =
                    QCUCRAM_SampleData.GenerateStudents(
                        QCUCRAM_SampleData.StudentsPerClick, adminId);

                return batch.Created + " students added (" +
                       batch.Clean + " clean, " +
                       batch.OneStrikeAway + " one strike away, " +
                       batch.RestrictedNow + " restricted now). " +
                       "Log in with any of: " + string.Join(", ", batch.Numbers.ToArray()) +
                       " / password " + QCUCRAM_SampleData.SamplePassword;
            }
            catch (Exception)
            {
                return "Could not create the sample students.";
            }
        }

        public static string GenerateReservations(int daysBack, int daysAhead, int adminId)
        {
            try
            {
                QCUCRAM_SampleData.ReservationBatch batch =
                    QCUCRAM_SampleData.GenerateReservations(daysBack, daysAhead, adminId);

                if (batch.Created == 0)
                {
                    return "No sample reservations were added. Add sample students and make sure " +
                           "at least one room and one time slot are active.";
                }

                return batch.Created + " sample reservation(s) added across " +
                       daysBack + " day(s) back and " + daysAhead + " day(s) ahead, " +
                       "covering " + batch.RoomsCovered + " of " + batch.RoomsTotal + " room(s).";
            }
            catch (Exception)
            {
                return "Could not create the sample reservations.";
            }
        }

        public static string ClearSampleReservations()
        {
            try
            {
                int removed = QCUCRAM_SampleData.ClearSampleReservations();
                return removed + " sample reservation(s) removed.";
            }
            catch (Exception)
            {
                return "Could not remove the sample reservations.";
            }
        }

        public static string DeleteSampleStudents()
        {
            try
            {
                int removed = QCUCRAM_SampleData.DeleteSampleStudents();
                return removed + " sample student account(s) removed.";
            }
            catch (Exception)
            {
                return "Could not remove the sample students. Clear their reservations first.";
            }
        }

        public static string ClearAllReservations()
        {
            try
            {
                int removed = QCUCRAM_SampleData.ClearAllReservations();
                return removed + " reservation(s) removed. Rooms, slots, and accounts were kept.";
            }
            catch (Exception)
            {
                return "Could not clear the reservations.";
            }
        }
    }
}
