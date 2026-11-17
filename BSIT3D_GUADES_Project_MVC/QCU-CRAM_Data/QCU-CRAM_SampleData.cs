using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data
{
    /// <summary>
    /// Demonstration data used by the Dev Tools screen. Every row it creates is
    /// tagged so it can be removed again without touching real records:
    ///   * students carry an e-mail ending in <see cref="EmailDomain"/>
    ///   * reservations carry a purpose beginning with <see cref="PurposePrefix"/>
    /// This is the MVC port of Guades_QCUCram_SampleData.
    /// </summary>
    public static class QCUCRAM_SampleData
    {
        public const string EmailDomain = "@cram.test";
        public const string PurposePrefix = "SAMPLE - ";
        public const string SamplePassword = "Test1234!";
        public const int StudentsPerClick = 10;
        public const int MaxReservationsPerRun = 200;

        // ------------------------------------------------------------ counts
        public class Counts
        {
            public int Students { get; set; }
            public int SampleStudents { get; set; }
            public int SampleRestricted { get; set; }
            public int SampleOneStrikeAway { get; set; }
            public int Reservations { get; set; }
            public int SampleReservations { get; set; }
            public int Notifications { get; set; }
        }

        public class StudentBatch
        {
            public int Created { get; set; }
            public int Clean { get; set; }
            public int OneStrikeAway { get; set; }
            public int RestrictedNow { get; set; }
            public int ExpiredRestriction { get; set; }
            public int Faculty { get; set; }
            public int Visitors { get; set; }
            public List<string> Numbers = new List<string>();
        }

        public class ReservationBatch
        {
            public int Created { get; set; }
            public int RoomsCovered { get; set; }
            public int RoomsTotal { get; set; }
            public DateTime? FirstDate { get; set; }
            public DateTime? LastDate { get; set; }
        }

        public static Counts GetCounts()
        {
            Counts c = new Counts();

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                int threshold = Math.Max(1, new QCUCRAM_ContentRepository(db)
                    .GetSettingInt("NoShowThreshold", 3));

                c.Students = db.Count("SELECT COUNT(*) AS Total FROM Students");
                c.SampleStudents = db.Count(
                    "SELECT COUNT(*) AS Total FROM Students WHERE Email LIKE @p",
                    QCUCRAM_Db.P("@p", "%" + EmailDomain));
                c.SampleRestricted = db.Count(
                    "SELECT COUNT(*) AS Total FROM Students WHERE Email LIKE @p AND IsRestricted = 1",
                    QCUCRAM_Db.P("@p", "%" + EmailDomain));
                c.SampleOneStrikeAway = db.Count(
                    @"SELECT COUNT(*) AS Total FROM Students
                       WHERE Email LIKE @p AND IsRestricted = 0 AND NoShowCount = @n",
                    QCUCRAM_Db.P("@p", "%" + EmailDomain),
                    QCUCRAM_Db.P("@n", threshold - 1));

                c.Reservations = db.Count("SELECT COUNT(*) AS Total FROM Reservations");
                c.SampleReservations = db.Count(
                    "SELECT COUNT(*) AS Total FROM Reservations WHERE Purpose LIKE @p",
                    QCUCRAM_Db.P("@p", PurposePrefix + "%"));
                c.Notifications = db.Count("SELECT COUNT(*) AS Total FROM Notifications");
            }

            return c;
        }

        // ---------------------------------------------------------- students
        private static readonly string[] FirstNames =
        {
            "Maria", "Jose", "Andrea", "Miguel", "Patricia", "Rafael",
            "Camille", "Daniel", "Isabel", "Nathaniel", "Sofia", "Gabriel"
        };

        private static readonly string[] LastNames =
        {
            "Santos", "Reyes", "Cruz", "Bautista", "Garcia", "Mendoza",
            "Torres", "Flores", "Ramos", "Aquino", "Navarro", "Villanueva"
        };

        public static StudentBatch GenerateStudents(int count, int adminId)
        {
            StudentBatch batch = new StudentBatch();
            if (count < 1) { count = StudentsPerClick; }
            if (count > 100) { count = 100; }

            string hash = QCUCRAM_PasswordHash.Hash(SamplePassword);
            Random rnd = new Random();

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_ContentRepository content = new QCUCRAM_ContentRepository(db);
                int threshold = Math.Max(1, content.GetSettingInt("NoShowThreshold", 3));
                int restrictDays = Math.Max(1, content.GetSettingInt("RestrictionDays", 7));

                int existing = db.Count(
                    "SELECT COUNT(*) AS Total FROM Students WHERE Email LIKE @p",
                    QCUCRAM_Db.P("@p", "%" + EmailDomain));

                for (int i = 0; i < count; i++)
                {
                    int seq = existing + i + 1;
                    string number = "99-" + (1000 + seq).ToString("0000", CultureInfo.InvariantCulture);
                    string first = FirstNames[rnd.Next(FirstNames.Length)];
                    string last = LastNames[rnd.Next(LastNames.Length)];

                    string type = "Student";
                    string program = QCUCRAM_Programs.All[rnd.Next(QCUCRAM_Programs.All.Length - 1)];
                    if (seq % 10 == 0) { type = "Faculty"; program = "Not applicable (Faculty / Visitor)"; }
                    else if (seq % 10 == 5) { type = "Visitor"; program = "Not applicable (Faculty / Visitor)"; }

                    // 20% restricted, 30% one strike away, the rest clean.
                    int strikes = 0;
                    bool restricted = false;
                    DateTime? lift = null;

                    int bucket = seq % 10;
                    if (bucket == 1 || bucket == 2)
                    {
                        restricted = true;
                        strikes = threshold;
                        lift = QCUCRAM_Clock.Now.AddDays(restrictDays);
                        batch.RestrictedNow++;
                    }
                    else if (bucket == 3 || bucket == 4 || bucket == 5)
                    {
                        strikes = Math.Max(0, threshold - 1);
                        batch.OneStrikeAway++;
                    }
                    else
                    {
                        batch.Clean++;
                    }

                    if (type == "Faculty") { batch.Faculty++; }
                    if (type == "Visitor") { batch.Visitors++; }

                    try
                    {
                        db.Execute(
                            @"INSERT INTO Students
                                  (StudentNumber, LastName, FirstName, MI, Email, Program,
                                   RequesterType, PasswordHash, NoShowCount, IsRestricted,
                                   RestrictionLiftDate, CreatedAt)
                              VALUES
                                  (@num, @last, @first, NULL, @mail, @prog,
                                   @type, @hash, @strikes, @restricted, @lift, @created)",
                            QCUCRAM_Db.P("@num", number),
                            QCUCRAM_Db.P("@last", last),
                            QCUCRAM_Db.P("@first", first),
                            QCUCRAM_Db.P("@mail", number.Replace("-", "") + EmailDomain),
                            QCUCRAM_Db.P("@prog", program),
                            QCUCRAM_Db.P("@type", type),
                            QCUCRAM_Db.P("@hash", hash),
                            QCUCRAM_Db.P("@strikes", strikes),
                            QCUCRAM_Db.P("@restricted", restricted),
                            QCUCRAM_Db.P("@lift", lift.HasValue ? (object)lift.Value : DBNull.Value),
                            QCUCRAM_Db.P("@created", QCUCRAM_Clock.Now));

                        batch.Created++;
                        batch.Numbers.Add(number);
                    }
                    catch (SqlException ex)
                    {
                        if (ex.Number != 2601 && ex.Number != 2627) { throw; }
                    }
                }
            }

            return batch;
        }

        // ------------------------------------------------------ reservations
        public static ReservationBatch GenerateReservations(int daysBack, int daysAhead, int adminId)
        {
            ReservationBatch batch = new ReservationBatch();

            if (daysBack < 0) { daysBack = 0; }
            if (daysAhead < 0) { daysAhead = 0; }
            if (daysBack > 60) { daysBack = 60; }
            if (daysAhead > 60) { daysAhead = 60; }

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                QCUCRAM_ContentRepository content = new QCUCRAM_ContentRepository(db);

                List<QCUCRAM_Models.QCUCRAM_Room> rooms = content.GetRooms(true);
                List<QCUCRAM_Models.QCUCRAM_TimeSlot> slots = content.GetTimeSlots(true);

                batch.RoomsTotal = rooms.Count;
                if (rooms.Count == 0 || slots.Count == 0) { return batch; }

                List<int> students = new List<int>();
                foreach (QCUCRAM_Models.QCUCRAM_Student s in
                         new QCUCRAM_StudentRepository(db).Search(null, null, null))
                {
                    if (s.Email != null &&
                        s.Email.EndsWith(EmailDomain, StringComparison.OrdinalIgnoreCase))
                    {
                        students.Add(s.StudentID);
                    }
                }

                if (students.Count == 0) { return batch; }

                DateTime today = QCUCRAM_Clock.Today;
                HashSet<int> roomsHit = new HashSet<int>();
                Random rnd = new Random();
                int cursor = 0;

                for (int offset = -daysBack; offset <= daysAhead; offset++)
                {
                    if (batch.Created >= MaxReservationsPerRun) { break; }

                    DateTime date = today.AddDays(offset);

                    foreach (QCUCRAM_Models.QCUCRAM_Room room in rooms)
                    {
                        foreach (QCUCRAM_Models.QCUCRAM_TimeSlot slot in slots)
                        {
                            if (batch.Created >= MaxReservationsPerRun) { break; }

                            // Leave roughly half the grid empty so the screens still
                            // show plenty of free slots.
                            if (rnd.Next(100) < 50) { continue; }

                            string status;
                            if (offset < 0) { status = rnd.Next(100) < 25 ? "Forfeited" : "Completed"; }
                            else if (offset == 0) { status = rnd.Next(100) < 30 ? "Pending" : "Approved"; }
                            else { status = rnd.Next(100) < 40 ? "Pending" : "Approved"; }

                            int studentId = students[cursor % students.Count];
                            cursor++;

                            try
                            {
                                db.Execute(
                                    @"INSERT INTO Reservations
                                          (StudentID, RoomID, TimeSlotID, ReservationDate, Purpose,
                                           NumberOfMembers, Status, CreatedAt)
                                      VALUES
                                          (@sid, @rid, @tid, @date, @purpose,
                                           @members, @status, @created)",
                                    QCUCRAM_Db.P("@sid", studentId),
                                    QCUCRAM_Db.P("@rid", room.RoomID),
                                    QCUCRAM_Db.P("@tid", slot.TimeSlotID),
                                    QCUCRAM_Db.P("@date", date),
                                    QCUCRAM_Db.P("@purpose",
                                        PurposePrefix + "Demonstration booking for " + room.RoomName),
                                    QCUCRAM_Db.P("@members", 5 + rnd.Next(6)),
                                    QCUCRAM_Db.P("@status", status),
                                    QCUCRAM_Db.P("@created", QCUCRAM_Clock.Now));

                                batch.Created++;
                                roomsHit.Add(room.RoomID);

                                if (!batch.FirstDate.HasValue || date < batch.FirstDate.Value)
                                {
                                    batch.FirstDate = date;
                                }
                                if (!batch.LastDate.HasValue || date > batch.LastDate.Value)
                                {
                                    batch.LastDate = date;
                                }
                            }
                            catch (SqlException ex)
                            {
                                // 2601/2627 = the UX_Res_ActiveSlot guard already covers
                                // this room/date/slot. Skip it and keep going.
                                if (ex.Number != 2601 && ex.Number != 2627) { throw; }
                            }
                        }
                    }
                }

                batch.RoomsCovered = roomsHit.Count;
            }

            return batch;
        }

        // ----------------------------------------------------------- cleanup
        public static int ClearSampleReservations()
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                return db.Execute(
                    "DELETE FROM Reservations WHERE Purpose LIKE @p",
                    QCUCRAM_Db.P("@p", PurposePrefix + "%"));
            }
        }

        public static int DeleteSampleStudents()
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                return db.Execute(
                    "DELETE FROM Students WHERE Email LIKE @p",
                    QCUCRAM_Db.P("@p", "%" + EmailDomain));
            }
        }

        public static int ClearAllReservations()
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                return db.Execute("DELETE FROM Reservations");
            }
        }
    }
}
