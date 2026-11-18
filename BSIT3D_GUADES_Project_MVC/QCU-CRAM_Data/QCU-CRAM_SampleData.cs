using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data
{
    using S = QCUCRAM_Status;

    /// <summary>
    /// Test-data generators and resets used by the Dev Tools page.
    ///
    /// One-to-one port of Guades_QCUCram_Helpers/Guades_QCUCram_SampleData.cs.
    ///
    ///  * Every click on "Generate 10 sample students" adds exactly 10 NEW students
    ///    (no cap, no duplicates).
    ///  * Student numbers look real (YY-NNNN, e.g. 24-0153). Faculty = FAC-0123,
    ///    Visitor = VIS-0123.
    ///  * A sample student is recognised by its e-mail domain (@cram.test), NOT by
    ///    its number. (Old SAMPLE-001 style accounts are still recognised so they
    ///    can be cleaned up.)
    ///  * Each batch of 10 contains: 4 clean, 3 one-strike-away, 2 restricted now,
    ///    1 expired restriction.
    ///  * Sample reservations cover all 8 statuses, every room, past days AND at
    ///    least 14 days ahead, and follow the same rules the automatic sweep
    ///    enforces, so the sweep leaves them unchanged.
    /// </summary>
    public static class QCUCRAM_SampleData
    {
        // ---------------------------------------------------------------- markers / constants
        public const string EmailDomain = "@cram.test";          // marks a sample student
        public const string LegacyStudentPrefix = "SAMPLE-";     // older sample accounts (SAMPLE-001 ...)
        public const string PurposePrefix = "SAMPLE - ";         // marks a sample reservation
        public const string SamplePassword = "Test1234!";        // passes the password policy
        public const int StudentsPerClick = 10;
        public const int MinDaysAhead = 14;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly Regex SampleEmailRegex =
            new Regex(@"^sample(\d+)@cram\.test$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // One place that says "this row is a sample student" (needs the parameters from SampleParams()).
        private const string SampleStudentWhere = "(Email LIKE @sd OR StudentNumber LIKE @sp)";

        private static SqlParameter[] SampleParams()
        {
            return new SqlParameter[]
            {
                QCUCRAM_Db.P("@sd", "%" + EmailDomain),
                QCUCRAM_Db.P("@sp", LegacyStudentPrefix + "%")
            };
        }

        private static readonly string[] LastNames =
        {
            "Santos", "Reyes", "Cruz", "Bautista", "Ocampo", "Garcia", "Mendoza", "Torres", "Flores", "Ramos",
            "Aquino", "Castillo", "Villanueva", "Navarro", "Domingo", "Salazar", "Pascual", "Soriano", "Lim", "Tan"
        };

        private static readonly string[] FirstNames =
        {
            "Juan", "Maria", "Jose", "Ana", "Mark", "Grace", "Paolo", "Kim", "Angelo", "Bianca",
            "Carlo", "Dani", "Ella", "Francis", "Gabriel", "Hannah", "Ivan", "Jasmine", "Kyle", "Lara"
        };

        private static readonly string[] Purposes =
        {
            "Group study", "Thesis consultation", "Capstone planning", "Organization meeting",
            "Review session", "Presentation practice", "Research discussion", "Case study analysis"
        };

        private static readonly string[] DeclineReasons =
        {
            "Slot needed for a scheduled library activity",
            "Purpose is not suitable for a collaboration room",
            "Member list is incomplete"
        };

        private static readonly string[] ReassignReasons =
        {
            "Room under maintenance",
            "Equipment problem in the original room",
            "Original slot was double-booked"
        };

        // ---------------------------------------------------------------- result objects
        public class Counts
        {
            public int Students { get; set; }
            public int SampleStudents { get; set; }
            public int SampleRestricted { get; set; }      // restricted right now
            public int SampleOneStrikeAway { get; set; }   // not restricted, NoShowThreshold - 1 strikes
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
            public int StrikeReservations { get; set; }
            public List<string> Numbers = new List<string>();
        }

        public class ReservationBatch
        {
            public int Created { get; set; }
            public int StrikeReservations { get; set; }
            public int RoomsCovered { get; set; }
            public int RoomsTotal { get; set; }
            public DateTime? FirstDate { get; set; }
            public DateTime? LastDate { get; set; }
            public bool CheckedInSkipped { get; set; }
            public Dictionary<string, int> ByStatus = new Dictionary<string, int>();

            public int Count(string status)
            {
                int n;
                return ByStatus.TryGetValue(status, out n) ? n : 0;
            }
        }

        // ---------------------------------------------------------------- counts
        public static Counts GetCounts()
        {
            int threshold = Math.Max(1, GetInt("NoShowThreshold", 3));
            DateTime now = QCUCRAM_Clock.Now;

            DataTable dt = Table(
                @"SELECT (SELECT COUNT(*) FROM Students)                                                   AS S,
                         (SELECT COUNT(*) FROM Students WHERE " + SampleStudentWhere + @")                 AS SS,
                         (SELECT COUNT(*) FROM Students WHERE " + SampleStudentWhere + @"
                              AND IsRestricted = 1
                              AND (RestrictionLiftDate IS NULL OR RestrictionLiftDate > @now))            AS SR,
                         (SELECT COUNT(*) FROM Students WHERE " + SampleStudentWhere + @"
                              AND IsRestricted = 0 AND NoShowCount = @near)                                AS SN,
                         (SELECT COUNT(*) FROM Reservations)                                               AS R,
                         (SELECT COUNT(*) FROM Reservations WHERE Purpose LIKE @pp)                        AS RR,
                         (SELECT COUNT(*) FROM Notifications)                                              AS N",
                QCUCRAM_Db.P("@sd", "%" + EmailDomain),
                QCUCRAM_Db.P("@sp", LegacyStudentPrefix + "%"),
                QCUCRAM_Db.P("@pp", PurposePrefix + "%"),
                QCUCRAM_Db.P("@now", now),
                QCUCRAM_Db.P("@near", threshold - 1));

            DataRow r = dt.Rows[0];
            return new Counts
            {
                Students = Convert.ToInt32(r["S"]),
                SampleStudents = Convert.ToInt32(r["SS"]),
                SampleRestricted = Convert.ToInt32(r["SR"]),
                SampleOneStrikeAway = Convert.ToInt32(r["SN"]),
                Reservations = Convert.ToInt32(r["R"]),
                SampleReservations = Convert.ToInt32(r["RR"]),
                Notifications = Convert.ToInt32(r["N"])
            };
        }

        /// <summary>"23 sample students total · 61 sample reservations"</summary>
        public static string TotalsText()
        {
            Counts c = GetCounts();
            return c.SampleStudents + " sample student" + (c.SampleStudents == 1 ? "" : "s") + " total \u00B7 " +
                   c.SampleReservations + " sample reservation" + (c.SampleReservations == 1 ? "" : "s");
        }

        // ================================================================== shared environment
        private class Env
        {
            public Random Rnd;
            public DateTime Now;
            public DateTime Today;
            public int Grace;
            public int Threshold;
            public int RestrictDays;
            public int? AdminId;
            public DataTable AllRooms;                              // every active room
            public DataTable Slots;                                 // every active time slot
            public List<DataRow> Rooms = new List<DataRow>();       // active rooms that can hold 5-10 people

            // what the run produced (filled by InsertReservation)
            public int Created;
            public Dictionary<string, int> ByStatus = new Dictionary<string, int>();
            public HashSet<int> RoomsHit = new HashSet<int>();
            public DateTime? FirstDate;
            public DateTime? LastDate;
        }

        private static Env LoadEnv(int adminId)
        {
            Env e = new Env();
            e.Rnd = new Random(Guid.NewGuid().GetHashCode());
            e.Now = QCUCRAM_Clock.Now;          // never DateTime.Now: the Dev Clock must work
            e.Today = QCUCRAM_Clock.Today;
            e.Grace = GetInt("GraceMinutes", 15);
            e.Threshold = Math.Max(1, GetInt("NoShowThreshold", 3));
            e.RestrictDays = Math.Max(1, GetInt("RestrictionDays", 7));
            e.AdminId = adminId > 0 ? (int?)adminId : null;

            e.AllRooms = Table(
                "SELECT RoomID, RoomName, MinCapacity, MaxCapacity FROM Rooms WHERE IsActive = 1 ORDER BY RoomID");
            e.Slots = Table(
                "SELECT TimeSlotID, StartTime, EndTime FROM TimeSlots WHERE IsActive = 1 ORDER BY StartTime");

            foreach (DataRow r in e.AllRooms.Rows)
            {
                int lo = Math.Max(5, Convert.ToInt32(r["MinCapacity"]));
                int hi = Math.Min(10, Convert.ToInt32(r["MaxCapacity"]));
                if (lo <= hi) e.Rooms.Add(r);
            }
            return e;
        }

        // ================================================================== sample students
        private enum Scenario { Clean, OneStrikeAway, RestrictedNow, ExpiredRestriction }

        /// <summary>
        /// Adds 'count' NEW sample students. It never reuses a number or e-mail that
        /// already exists, so clicking again always adds another full batch.
        /// </summary>
        public static StudentBatch GenerateStudents(int count, int adminId)
        {
            if (count < 1) count = StudentsPerClick;

            Env e = LoadEnv(adminId);
            Random rnd = e.Rnd;
            StudentBatch result = new StudentBatch();

            string hash = QCUCRAM_PasswordHash.Hash(SamplePassword);   // one hash reused for all
            string[] programs = QCUCRAM_Programs.All;
            string notApplicable = programs[programs.Length - 1];
            int realPrograms = Math.Max(1, programs.Length - 1);

            // Everything already taken (real accounts AND earlier samples)
            HashSet<string> usedNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int emailSeq = 0;

            DataTable existing = Table("SELECT StudentNumber, Email FROM Students");
            foreach (DataRow r in existing.Rows)
            {
                usedNumbers.Add(Convert.ToString(r["StudentNumber"]));
                string em = Convert.ToString(r["Email"]);
                usedEmails.Add(em);

                Match m = SampleEmailRegex.Match(em);
                int n;
                if (m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None, Inv, out n) && n > emailSeq)
                {
                    emailSeq = n;
                }
            }

            // Which scenario / requester type each of the new students gets
            // (per block of 10: 4 clean, 3 one-strike-away, 2 restricted, 1 expired;
            //  8 students, 1 faculty, 1 visitor)
            Scenario[] scenarioPattern =
            {
                Scenario.Clean, Scenario.Clean, Scenario.Clean, Scenario.Clean,
                Scenario.OneStrikeAway, Scenario.OneStrikeAway, Scenario.OneStrikeAway,
                Scenario.RestrictedNow, Scenario.RestrictedNow,
                Scenario.ExpiredRestriction
            };
            string[] typePattern =
            {
                "Student", "Student", "Student", "Student", "Student", "Student", "Student", "Student",
                "Faculty", "Visitor"
            };
            List<Scenario> scenarios = PatternFor(scenarioPattern, count, rnd);
            List<string> types = PatternFor(typePattern, count, rnd);

            int restrictedSeen = 0;

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                db.RunInTransaction(IsolationLevel.ReadCommitted, (con, tx) =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        string type = types[i];
                        Scenario sc = scenarios[i];

                        string number = NewNumber(type, usedNumbers, rnd);

                        string email = "sample" + (++emailSeq).ToString("000", Inv) + EmailDomain;
                        while (usedEmails.Contains(email))
                        {
                            email = "sample" + (++emailSeq).ToString("000", Inv) + EmailDomain;
                        }
                        usedEmails.Add(email);

                        string program = type == "Student" ? programs[rnd.Next(realPrograms)] : notApplicable;

                        // ---- the restriction scenario (all relative to the Dev Clock, never hard-coded 3 / 7) ----
                        int strikes = 0;
                        bool restricted = false;
                        DateTime? lift = null;

                        switch (sc)
                        {
                            case Scenario.Clean:
                                // 0 strikes, or 1 when the threshold allows it while staying "clean"
                                strikes = Math.Min(rnd.Next(0, 2), Math.Max(0, e.Threshold - 2));
                                result.Clean++;
                                break;

                            case Scenario.OneStrikeAway:
                                strikes = e.Threshold - 1;
                                result.OneStrikeAway++;
                                break;

                            case Scenario.RestrictedNow:
                                strikes = e.Threshold;
                                restricted = true;
                                // vary how long is left (e.g. +2 days, +6 days)
                                int daysLeft = (restrictedSeen % 2 == 0)
                                    ? Math.Min(2, e.RestrictDays)
                                    : Math.Max(1, e.RestrictDays - 1);
                                restrictedSeen++;
                                lift = e.Now.AddDays(daysLeft);
                                result.RestrictedNow++;
                                break;

                            case Scenario.ExpiredRestriction:
                                strikes = e.Threshold;
                                restricted = true;
                                lift = e.Now.AddDays(-1);   // already over: the sweep / next login must lift it
                                result.ExpiredRestriction++;
                                break;
                        }

                        if (type == "Faculty") result.Faculty++;
                        if (type == "Visitor") result.Visitors++;

                        int studentId = Convert.ToInt32(QCUCRAM_Db.Scalar(con, tx,
                            @"INSERT INTO Students
                                  (StudentNumber, LastName, FirstName, MI, Email, Program, RequesterType, PasswordHash,
                                   NoShowCount, IsRestricted, RestrictionLiftDate)
                              OUTPUT INSERTED.StudentID
                              VALUES (@n, @l, @f, @mi, @e, @p, @t, @h, @ns, @ir, @lift)",
                            QCUCRAM_Db.P("@n", number),
                            QCUCRAM_Db.P("@l", LastNames[rnd.Next(LastNames.Length)]),
                            QCUCRAM_Db.P("@f", FirstNames[rnd.Next(FirstNames.Length)]),
                            QCUCRAM_Db.P("@mi", ((char)('A' + rnd.Next(26))).ToString()),
                            QCUCRAM_Db.P("@e", email),
                            QCUCRAM_Db.P("@p", program),
                            QCUCRAM_Db.P("@t", type),
                            QCUCRAM_Db.P("@h", hash),
                            QCUCRAM_Db.P("@ns", strikes),
                            QCUCRAM_Db.P("@ir", restricted),
                            QCUCRAM_Db.P("@lift", lift.HasValue ? (object)lift.Value : null)));

                        // Nice-to-have: matching Forfeited reservations so Analytics
                        // ("no-shows per student") agrees with NoShowCount. They are dated
                        // BEFORE the restriction began.
                        if (strikes > 0)
                        {
                            DateTime lastDay = lift.HasValue
                                ? lift.Value.AddDays(-e.RestrictDays).Date.AddDays(-1)
                                : e.Today.AddDays(-1);
                            result.StrikeReservations += AddStrikeReservations(
                                e, con, tx, studentId, strikes, 0, lastDay, restricted ? lift : null);
                        }

                        result.Created++;
                        result.Numbers.Add(number);
                    }
                });
            }

            return result;
        }

        /// <summary>
        /// Real-style ID that is not used yet.
        /// Student: YY-NNNN (YY 22-26). Faculty: FAC-NNNN. Visitor: VIS-NNNN.
        /// </summary>
        private static string NewNumber(string type, HashSet<string> used, Random rnd)
        {
            for (int attempt = 0; attempt < 500; attempt++)
            {
                string n = MakeNumber(type, rnd.Next(22, 27), rnd.Next(1, 10000));
                if (used.Add(n)) return n;
            }

            // Very crowded range: walk it in order so we still find a free one
            for (int yy = 22; yy <= 26; yy++)
            {
                for (int seq = 1; seq <= 9999; seq++)
                {
                    string n = MakeNumber(type, yy, seq);
                    if (used.Add(n)) return n;
                }
            }
            throw new InvalidOperationException("No free sample ID is left for the " + type + " type.");
        }

        private static string MakeNumber(string type, int yy, int seq)
        {
            string tail = seq.ToString("0000", Inv);
            if (type == "Faculty") return "FAC-" + tail;
            if (type == "Visitor") return "VIS-" + tail;
            return yy.ToString("00", Inv) + "-" + tail;
        }

        /// <summary>Repeats 'pattern' until 'count' items exist; every full block is shuffled.</summary>
        private static List<T> PatternFor<T>(T[] pattern, int count, Random rnd)
        {
            List<T> list = new List<T>();
            while (list.Count < count)
            {
                List<T> block = new List<T>(pattern);
                Shuffle(block, rnd);
                foreach (T item in block)
                {
                    if (list.Count >= count) break;
                    list.Add(item);
                }
            }
            return list;
        }

        private static void Shuffle<T>(IList<T> list, Random rnd)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }

        /// <summary>
        /// 'count' past Forfeited reservations for one student (distinct days, all on or
        /// before 'lastDay'). 'alreadyForfeited' is how many the student has, so the
        /// strike text reads "Strike 2 of 3".
        /// </summary>
        private static int AddStrikeReservations(Env e, SqlConnection con, SqlTransaction tx, int studentId,
                                                 int count, int alreadyForfeited, DateTime lastDay, DateTime? restrictedUntil)
        {
            if (count <= 0 || e.Rooms.Count == 0 || e.Slots.Rows.Count == 0) return 0;

            List<int> offsets = new List<int>();
            for (int i = 0; i < 25; i++) offsets.Add(i);
            Shuffle(offsets, e.Rnd);

            List<DateTime> dates = new List<DateTime>();
            for (int i = 0; i < count && i < offsets.Count; i++) dates.Add(lastDay.AddDays(-offsets[i]));
            dates.Sort();

            int made = 0;
            for (int i = 0; i < dates.Count; i++)
            {
                int strikeNo = alreadyForfeited + i + 1;
                string extra = strikeNo <= e.Threshold
                    ? "Strike " + strikeNo + " of " + e.Threshold + "."
                    : "Total strikes: " + strikeNo + ".";
                if (restrictedUntil.HasValue && strikeNo == e.Threshold)
                {
                    extra += " You are now restricted from booking until " +
                             restrictedUntil.Value.ToString("MMM d, yyyy h:mm tt", Inv) + ".";
                }

                ResSpec spec = new ResSpec
                {
                    StudentId = studentId,
                    Room = e.Rooms[e.Rnd.Next(e.Rooms.Count)],
                    Slot = e.Slots.Rows[e.Rnd.Next(e.Slots.Rows.Count)],
                    Date = dates[i],
                    Status = S.Forfeited,
                    Extra = extra
                };
                InsertReservation(e, con, tx, spec);
                made++;
            }
            return made;
        }

        // ================================================================== sample reservations
        private class SStudent
        {
            public int Id;
            public int Strikes;
            public int Forfeited;                // Forfeited reservation rows that already exist
            public bool Restricted;              // IsRestricted flag
            public DateTime? Lift;
            public DateTime? RestrictionStart;   // Lift - RestrictionDays
            public int CleanRoom;                // how many more strikes a CLEAN student may take and still be "clean"

            // A restricted student cannot have booked while the restriction ran.
            public bool BlockedOn(DateTime date)
            {
                if (!RestrictionStart.HasValue || !Lift.HasValue) return false;
                DateTime bookedOn = date.Date.AddDays(-1);        // requests are made the day before
                return bookedOn >= RestrictionStart.Value.Date && bookedOn <= Lift.Value.Date;
            }

            public bool RestrictedNow(DateTime now)
            {
                return Restricted && (!Lift.HasValue || Lift.Value > now);
            }
        }

        private class Plan
        {
            public DateTime Date;
            public string Status;        // null = decided from the slot (used for today)
            public int RoomIndex = -1;   // -1 = any room
            public bool Must;            // part of the "every status / every room" guarantee
        }

        private class Pool
        {
            public HashSet<string> Slots = new HashSet<string>();         // yyyyMMdd|room|slot  (active reservations)
            public HashSet<string> StudentDays = new HashSet<string>();   // yyyyMMdd|student    (active reservations)
        }

        private class ResSpec
        {
            public int StudentId;
            public SStudent Student;
            public DataRow Room;
            public DataRow Slot;
            public DateTime Date;
            public string Status;
            public string Extra;         // extra sentence for the Forfeited notification
            public string SlotKey;
            public string DayKey;
            public bool Loose;           // fallback pick: strike count could not be kept in step
        }

        private static List<SStudent> LoadSampleStudents(Env e)
        {
            List<SStudent> list = new List<SStudent>();

            DataTable st = Table(
                "SELECT StudentID, NoShowCount, IsRestricted, RestrictionLiftDate FROM Students WHERE " +
                SampleStudentWhere + " ORDER BY StudentID", SampleParams());

            DataTable forf = Table(
                @"SELECT StudentID, COUNT(*) AS C FROM Reservations
                   WHERE Status = 'Forfeited'
                     AND StudentID IN (SELECT StudentID FROM Students WHERE " + SampleStudentWhere + @")
                   GROUP BY StudentID", SampleParams());

            Dictionary<int, int> forfeited = new Dictionary<int, int>();
            foreach (DataRow r in forf.Rows)
            {
                forfeited[Convert.ToInt32(r["StudentID"])] = Convert.ToInt32(r["C"]);
            }

            foreach (DataRow r in st.Rows)
            {
                SStudent s = new SStudent();
                s.Id = Convert.ToInt32(r["StudentID"]);
                s.Strikes = Convert.ToInt32(r["NoShowCount"]);
                s.Restricted = Convert.ToBoolean(r["IsRestricted"]);
                s.Lift = r["RestrictionLiftDate"] == DBNull.Value
                    ? (DateTime?)null
                    : Convert.ToDateTime(r["RestrictionLiftDate"]);
                if (s.Restricted && s.Lift.HasValue) s.RestrictionStart = s.Lift.Value.AddDays(-e.RestrictDays);

                int f;
                s.Forfeited = forfeited.TryGetValue(s.Id, out f) ? f : 0;
                RefreshCleanRoom(e, s);
                list.Add(s);
            }
            return list;
        }

        private static void RefreshCleanRoom(Env e, SStudent s)
        {
            s.CleanRoom = s.Restricted ? 0 : Math.Max(0, e.Threshold - 2 - s.Strikes);
        }

        /// <summary>
        /// Creates sample reservations: history over 'daysBack' days, and 'daysAhead' days
        /// forward (at least 14). Every run adds at least one reservation of each of the
        /// 8 statuses (CheckedIn needs a slot that is running right now - see
        /// ReservationBatch.CheckedInSkipped) and reservations in every active room.
        /// </summary>
        public static ReservationBatch GenerateReservations(int daysBack, int daysAhead, int adminId)
        {
            Env e = LoadEnv(adminId);
            Random rnd = e.Rnd;
            daysBack = Math.Max(1, daysBack);
            daysAhead = Math.Max(MinDaysAhead, daysAhead);

            List<SStudent> students = LoadSampleStudents(e);
            if (students.Count == 0)
            {
                throw new InvalidOperationException("Generate the sample students first.");
            }
            if (e.Rooms.Count == 0 || e.Slots.Rows.Count == 0)
            {
                throw new InvalidOperationException("There are no active rooms (holding 5 to 10 people) or time slots.");
            }

            DateTime from = e.Today.AddDays(-daysBack);
            DateTime to = e.Today.AddDays(daysAhead);

            // Slots and student-days already taken by ACTIVE reservations (unique index + 1-per-day rule)
            Pool pool = new Pool();
            DataTable existing = Table(
                @"SELECT ReservationDate, RoomID, TimeSlotID, StudentID FROM Reservations
                   WHERE ReservationDate BETWEEN @f AND @t AND Status NOT IN ('Declined','Cancelled','Forfeited')",
                QCUCRAM_Db.P("@f", from), QCUCRAM_Db.P("@t", to));

            foreach (DataRow r in existing.Rows)
            {
                string dk = Convert.ToDateTime(r["ReservationDate"]).ToString("yyyyMMdd", Inv);
                pool.Slots.Add(dk + "|" + r["RoomID"] + "|" + r["TimeSlotID"]);
                pool.StudentDays.Add(dk + "|" + r["StudentID"]);
            }

            // Slots that are running right now (only those can hold a stable CheckedIn reservation)
            List<DataRow> running = new List<DataRow>();
            foreach (DataRow r in e.Slots.Rows)
            {
                DateTime s0 = e.Today.Add((TimeSpan)r["StartTime"]);
                DateTime s1 = e.Today.Add((TimeSpan)r["EndTime"]);
                if (s0 <= e.Now && e.Now < s1) running.Add(r);
            }

            // ------------------------------------------------------------ the plan
            List<Plan> plans = new List<Plan>();

            // (1) Guarantee: one reservation of every status. Final states go in the past, open states ahead.
            string[] aheadStatuses = { S.Pending, S.Approved, S.Reassigned, S.Declined, S.Cancelled };
            foreach (string s in aheadStatuses)
            {
                plans.Add(new Plan { Date = e.Today.AddDays(rnd.Next(1, daysAhead + 1)), Status = s, Must = true });
            }
            plans.Add(new Plan { Date = e.Today.AddDays(-rnd.Next(1, daysBack + 1)), Status = S.Completed, Must = true });
            plans.Add(new Plan { Date = e.Today.AddDays(-rnd.Next(1, daysBack + 1)), Status = S.Forfeited, Must = true });
            plans.Add(new Plan { Date = e.Today, Status = S.CheckedIn, Must = true });

            // (2) Guarantee: every room gets at least one past and one future reservation
            for (int ri = 0; ri < e.Rooms.Count; ri++)
            {
                plans.Add(new Plan
                {
                    Date = e.Today.AddDays(rnd.Next(1, daysAhead + 1)),
                    Status = rnd.Next(2) == 0 ? S.Approved : S.Pending,
                    RoomIndex = ri,
                    Must = true
                });
                plans.Add(new Plan
                {
                    Date = e.Today.AddDays(-rnd.Next(1, daysBack + 1)),
                    Status = S.Completed,
                    RoomIndex = ri,
                    Must = true
                });
            }

            // (3) Past days: final states only (a past Pending would be auto-declined, a past Approved auto-forfeited)
            for (DateTime d = from; d < e.Today; d = d.AddDays(1))
            {
                int perDay = rnd.Next(3, 8);
                for (int k = 0; k < perDay; k++)
                {
                    int roll = rnd.Next(100);
                    string status = roll < 60 ? S.Completed : roll < 74 ? S.Forfeited : roll < 87 ? S.Declined : S.Cancelled;
                    plans.Add(new Plan { Date = d, Status = status });
                }
            }

            // (4) Today: the status follows the slot (earlier = Completed, running = CheckedIn, later = Approved/Reassigned)
            int todayCount = rnd.Next(3, 6);
            for (int k = 0; k < todayCount; k++) plans.Add(new Plan { Date = e.Today, Status = null });

            // (5) Future days: nothing can be checked in, completed or forfeited yet
            for (int day = 1; day <= daysAhead; day++)
            {
                int perDay = day <= 3 ? rnd.Next(8, 14) : rnd.Next(4, 10);
                for (int k = 0; k < perDay; k++)
                {
                    int roll = rnd.Next(100);
                    string status = roll < 28 ? S.Pending : roll < 70 ? S.Approved : roll < 80 ? S.Reassigned
                                  : roll < 90 ? S.Cancelled : S.Declined;
                    plans.Add(new Plan { Date = e.Today.AddDays(day), Status = status });
                }
            }

            // ------------------------------------------------------------ write everything in ONE transaction
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                return db.InTransaction(IsolationLevel.ReadCommitted, (con, tx) =>
                {
                    int strikeRows = 0;

                    // Strike history: make sure every student's NoShowCount has matching Forfeited rows
                    // (they are lost when "Clear sample reservations" is used).
                    foreach (SStudent s in students)
                    {
                        if (s.Strikes <= s.Forfeited) continue;
                        DateTime lastDay = s.RestrictionStart.HasValue
                            ? s.RestrictionStart.Value.Date.AddDays(-1)
                            : e.Today.AddDays(-1);
                        int made = AddStrikeReservations(e, con, tx, s.Id, s.Strikes - s.Forfeited, s.Forfeited,
                                                         lastDay, s.Restricted ? s.Lift : null);
                        s.Forfeited += made;
                        strikeRows += made;
                    }

                    foreach (Plan p in plans)
                    {
                        ResSpec spec = BuildSpec(e, p, students, pool, running, false);

                        // A random Forfeited needs a student who can still take a strike without changing
                        // their category; if nobody can, it simply becomes a Completed one.
                        if (spec == null && !p.Must && p.Status == S.Forfeited)
                        {
                            p.Status = S.Completed;
                            spec = BuildSpec(e, p, students, pool, running, false);
                        }
                        // The guarantee wins over keeping the strike count in step.
                        if (spec == null && p.Must)
                        {
                            spec = BuildSpec(e, p, students, pool, running, true);
                        }
                        if (spec == null) continue;

                        if (spec.Status == S.Forfeited && !spec.Loose)
                        {
                            // a real forfeiture adds a strike: keep NoShowCount equal to the Forfeited rows
                            QCUCRAM_Db.Exec(con, tx,
                                "UPDATE Students SET NoShowCount = NoShowCount + 1 WHERE StudentID = @s",
                                QCUCRAM_Db.P("@s", spec.StudentId));
                            spec.Student.Strikes++;
                            RefreshCleanRoom(e, spec.Student);
                            spec.Extra = "Strike " + spec.Student.Strikes + " of " + e.Threshold + ".";
                        }
                        if (spec.Status == S.Forfeited) spec.Student.Forfeited++;

                        if (S.IsActive(spec.Status))
                        {
                            pool.Slots.Add(spec.SlotKey);
                            pool.StudentDays.Add(spec.DayKey);
                        }
                        InsertReservation(e, con, tx, spec);
                    }

                    ReservationBatch batch = new ReservationBatch();
                    batch.Created = e.Created;
                    batch.StrikeReservations = strikeRows;
                    batch.ByStatus = e.ByStatus;
                    batch.RoomsCovered = e.RoomsHit.Count;
                    batch.RoomsTotal = e.AllRooms.Rows.Count;
                    batch.FirstDate = e.FirstDate;
                    batch.LastDate = e.LastDate;
                    batch.CheckedInSkipped = batch.Count(S.CheckedIn) == 0;
                    return batch;
                });
            }
        }

        /// <summary>
        /// Finds a free room / slot / student for one planned reservation
        /// (null = nothing fits). Nothing is written here.
        /// </summary>
        private static ResSpec BuildSpec(Env e, Plan p, List<SStudent> students, Pool pool,
                                         List<DataRow> running, bool loose)
        {
            string dk = p.Date.ToString("yyyyMMdd", Inv);

            for (int attempt = 0; attempt < 40; attempt++)
            {
                // Skewed pick (low room numbers a bit busier) so the analytics show a clear busiest room.
                DataRow room = (p.RoomIndex >= 0 && p.RoomIndex < e.Rooms.Count)
                    ? e.Rooms[p.RoomIndex]
                    : e.Rooms[Math.Min(e.Rnd.Next(e.Rooms.Count), e.Rnd.Next(e.Rooms.Count))];

                DataRow slot;
                if (p.Status == S.CheckedIn)
                {
                    if (running.Count == 0) return null;
                    slot = running[e.Rnd.Next(running.Count)];
                }
                else
                {
                    int n = e.Slots.Rows.Count;
                    slot = e.Slots.Rows[(e.Rnd.Next(n) + e.Rnd.Next(n)) / 2];   // peak around midday
                }

                DateTime slotStart = p.Date.Date.Add((TimeSpan)slot["StartTime"]);
                DateTime slotEnd = p.Date.Date.Add((TimeSpan)slot["EndTime"]);

                string status = p.Status;
                if (status == null)
                {
                    if (slotEnd <= e.Now) status = S.Completed;
                    else if (slotStart <= e.Now) status = S.CheckedIn;
                    else status = e.Rnd.Next(100) < 80 ? S.Approved : S.Reassigned;
                }

                bool active = S.IsActive(status);
                string slotKey = dk + "|" + room["RoomID"] + "|" + slot["TimeSlotID"];
                if (active && pool.Slots.Contains(slotKey)) continue;

                SStudent stu = PickStudent(e, students, status, p.Date, dk, active, pool, loose);
                if (stu == null) continue;

                return new ResSpec
                {
                    StudentId = stu.Id,
                    Student = stu,
                    Room = room,
                    Slot = slot,
                    Date = p.Date.Date,
                    Status = status,
                    SlotKey = slotKey,
                    DayKey = dk + "|" + stu.Id,
                    Loose = loose && status == S.Forfeited
                };
            }
            return null;
        }

        private static SStudent PickStudent(Env e, List<SStudent> students, string status, DateTime date, string dk,
                                            bool active, Pool pool, bool loose)
        {
            List<int> order = new List<int>();
            for (int i = 0; i < students.Count; i++) order.Add(i);
            Shuffle(order, e.Rnd);

            foreach (int idx in order)
            {
                SStudent s = students[idx];

                if (!loose)
                {
                    if (s.BlockedOn(date)) continue;                         // restricted students cannot book
                    if (status == S.Forfeited && s.CleanRoom <= 0) continue; // keep clean / one-strike categories intact
                }
                if (active && pool.StudentDays.Contains(dk + "|" + s.Id)) continue;   // max 1 active booking per day
                return s;
            }
            return null;
        }

        /// <summary>Writes one reservation with its members, status history and notifications.</summary>
        private static void InsertReservation(Env e, SqlConnection con, SqlTransaction tx, ResSpec x)
        {
            Random rnd = e.Rnd;
            int roomId = Convert.ToInt32(x.Room["RoomID"]);
            string roomName = Convert.ToString(x.Room["RoomName"]);
            TimeSpan st = (TimeSpan)x.Slot["StartTime"];
            TimeSpan en = (TimeSpan)x.Slot["EndTime"];
            DateTime slotStart = x.Date.Date.Add(st);
            DateTime slotEnd = x.Date.Date.Add(en);

            int lo = Math.Max(5, Convert.ToInt32(x.Room["MinCapacity"]));
            int hi = Math.Min(10, Convert.ToInt32(x.Room["MaxCapacity"]));
            int members = rnd.Next(lo, hi + 1);

            string summary = roomName + " on " + x.Date.ToString("MMM d, yyyy", Inv) +
                             " (" + QCUCRAM_ReservationInfo.FormatSlot(st, en) + ")";

            // ---- timeline ----
            // Requests are made the day before; for future days that would be "later than now",
            // so use a recent time.
            DateTime createdAt = x.Date.Date.AddDays(-1).AddHours(rnd.Next(8, 19)).AddMinutes(rnd.Next(60));
            if (createdAt >= e.Now) createdAt = e.Now.AddMinutes(-rnd.Next(5, 600));

            DateTime decided = createdAt.AddMinutes(rnd.Next(10, 240));
            if (decided > slotStart.AddMinutes(-5)) decided = slotStart.AddMinutes(-5);
            if (decided > e.Now) decided = e.Now;
            if (decided <= createdAt) decided = createdAt.AddMinutes(1);

            DateTime checkedIn = slotStart.AddMinutes(rnd.Next(0, 11));
            if (checkedIn > e.Now) checkedIn = e.Now;

            bool hasCheckIn = (x.Status == S.CheckedIn || x.Status == S.Completed);

            int resId = Convert.ToInt32(QCUCRAM_Db.Scalar(con, tx,
                @"INSERT INTO Reservations
                      (StudentID, RoomID, TimeSlotID, ReservationDate, Purpose, NumberOfMembers, Status, CreatedAt, CheckedInAt)
                  OUTPUT INSERTED.ReservationID
                  VALUES (@s, @r, @t, CAST(@d AS DATE), @p, @n, @st, @c, @ci)",
                QCUCRAM_Db.P("@s", x.StudentId),
                QCUCRAM_Db.P("@r", roomId),
                QCUCRAM_Db.P("@t", Convert.ToInt32(x.Slot["TimeSlotID"])),
                QCUCRAM_Db.P("@d", x.Date.Date),
                QCUCRAM_Db.P("@p", PurposePrefix + Purposes[rnd.Next(Purposes.Length)]),
                QCUCRAM_Db.P("@n", members),
                QCUCRAM_Db.P("@st", x.Status),
                QCUCRAM_Db.P("@c", createdAt),
                QCUCRAM_Db.P("@ci", hasCheckIn ? (object)checkedIn : null)));

            // ---- members: one multi-row insert ----
            StringBuilder sb = new StringBuilder(
                "INSERT INTO ReservationMembers (ReservationID, LastName, FirstName, MI) VALUES ");
            List<SqlParameter> ps = new List<SqlParameter>();
            ps.Add(QCUCRAM_Db.P("@r", resId));
            for (int m = 0; m < members; m++)
            {
                if (m > 0) sb.Append(", ");
                sb.Append("(@r, @l").Append(m).Append(", @f").Append(m).Append(", @m").Append(m).Append(")");
                ps.Add(QCUCRAM_Db.P("@l" + m, LastNames[rnd.Next(LastNames.Length)]));
                ps.Add(QCUCRAM_Db.P("@f" + m, FirstNames[rnd.Next(FirstNames.Length)]));
                ps.Add(QCUCRAM_Db.P("@m" + m, ((char)('A' + rnd.Next(26))).ToString()));
            }
            QCUCRAM_Db.Exec(con, tx, sb.ToString(), ps.ToArray());

            // ---- history + notifications (same wording as the real workflow) ----
            string head = "Reservation #" + resId + " (" + summary + ")";
            int sid = x.StudentId;
            int? adminBy = e.AdminId;

            AddHistory(con, tx, resId, null, S.Pending, "Student", sid, createdAt, "Request submitted");
            AddNotification(e, con, tx, sid, resId, createdAt,
                "Your request #" + resId + " (" + summary + ") was received and is awaiting review by the library assistant.");

            string approvedText = head + " was approved. Bring the physical QCID of all members and arrive on time; " +
                                  "unclaimed rooms are forfeited after " + e.Grace + " minutes.";

            switch (x.Status)
            {
                case S.Pending:
                    break;

                case S.Approved:
                    AddHistory(con, tx, resId, S.Pending, S.Approved, "Admin", adminBy, decided, null);
                    AddNotification(e, con, tx, sid, resId, decided, approvedText);
                    break;

                case S.Reassigned:
                    {
                        DataRow oldRoom = x.Room;
                        if (e.AllRooms.Rows.Count > 1)
                        {
                            do { oldRoom = e.AllRooms.Rows[rnd.Next(e.AllRooms.Rows.Count)]; }
                            while (Convert.ToInt32(oldRoom["RoomID"]) == roomId);
                        }
                        DataRow oldSlot = e.Slots.Rows[rnd.Next(e.Slots.Rows.Count)];
                        string note = "Moved from " + Convert.ToString(oldRoom["RoomName"]) + ", " +
                                      QCUCRAM_ReservationInfo.FormatSlot((TimeSpan)oldSlot["StartTime"], (TimeSpan)oldSlot["EndTime"]) +
                                      " to " + roomName + ", " + QCUCRAM_ReservationInfo.FormatSlot(st, en) +
                                      ". Reason: " + ReassignReasons[rnd.Next(ReassignReasons.Length)];
                        AddHistory(con, tx, resId, S.Pending, S.Reassigned, "Admin", adminBy, decided, note);
                        AddNotification(e, con, tx, sid, resId, decided,
                            head + " was reassigned to this new room/time. Details: " + note);
                        break;
                    }

                case S.Declined:
                    {
                        // Past requests are sometimes declined by the sweep ("expired"), otherwise by the assistant
                        bool expired = slotStart <= e.Now && rnd.Next(100) < 25;
                        if (expired)
                        {
                            string why = "Expired - not reviewed before the slot start time.";
                            AddHistory(con, tx, resId, S.Pending, S.Declined, "System", null, slotStart, why);
                            AddNotification(e, con, tx, sid, resId, slotStart, head + " was declined. Reason: " + why);
                        }
                        else
                        {
                            string why = DeclineReasons[rnd.Next(DeclineReasons.Length)];
                            AddHistory(con, tx, resId, S.Pending, S.Declined, "Admin", adminBy, decided, why);
                            AddNotification(e, con, tx, sid, resId, decided, head + " was declined. Reason: " + why);
                        }
                        break;
                    }

                case S.Cancelled:
                    {
                        DateTime cancelAt = createdAt.AddMinutes(rnd.Next(1, 120));
                        if (cancelAt >= slotStart) cancelAt = slotStart.AddMinutes(-1);
                        if (cancelAt > e.Now) cancelAt = e.Now;
                        if (cancelAt <= createdAt) cancelAt = createdAt.AddMinutes(1);
                        AddHistory(con, tx, resId, S.Pending, S.Cancelled, "Student", sid, cancelAt, "Cancelled by the requester");
                        AddNotification(e, con, tx, sid, resId, cancelAt, "You cancelled " + head + ".");
                        break;
                    }

                case S.CheckedIn:
                    AddHistory(con, tx, resId, S.Pending, S.Approved, "Admin", adminBy, decided, null);
                    AddNotification(e, con, tx, sid, resId, decided, approvedText);
                    AddHistory(con, tx, resId, S.Approved, S.CheckedIn, "Admin", adminBy, checkedIn, null);
                    AddNotification(e, con, tx, sid, resId, checkedIn,
                        "Check-in confirmed for " + head + ". Please keep the room tidy, no eating or drinking.");
                    break;

                case S.Completed:
                    AddHistory(con, tx, resId, S.Pending, S.Approved, "Admin", adminBy, decided, null);
                    AddNotification(e, con, tx, sid, resId, decided, approvedText);
                    AddHistory(con, tx, resId, S.Approved, S.CheckedIn, "Admin", adminBy, checkedIn, null);
                    AddNotification(e, con, tx, sid, resId, checkedIn,
                        "Check-in confirmed for " + head + ". Please keep the room tidy, no eating or drinking.");
                    AddHistory(con, tx, resId, S.CheckedIn, S.Completed, "System", null, slotEnd, "Slot ended");
                    AddNotification(e, con, tx, sid, resId, slotEnd,
                        head + " is completed. Thank you for using the collaboration rooms.");
                    break;

                case S.Forfeited:
                    {
                        DateTime forfeitAt = slotStart.AddMinutes(e.Grace + 1);
                        AddHistory(con, tx, resId, S.Pending, S.Approved, "Admin", adminBy, decided, null);
                        AddNotification(e, con, tx, sid, resId, decided, approvedText);
                        AddHistory(con, tx, resId, S.Approved, S.Forfeited, "System", null, forfeitAt,
                                   "Not checked in within the grace period");
                        AddNotification(e, con, tx, sid, resId, forfeitAt,
                            head + " was forfeited because it was not claimed within " + e.Grace +
                            " minutes of the start time." + (string.IsNullOrEmpty(x.Extra) ? "" : " " + x.Extra));
                        break;
                    }
            }

            // ---- statistics for the result message ----
            e.Created++;
            int c;
            e.ByStatus[x.Status] = e.ByStatus.TryGetValue(x.Status, out c) ? c + 1 : 1;
            e.RoomsHit.Add(roomId);
            if (!e.FirstDate.HasValue || x.Date.Date < e.FirstDate.Value) e.FirstDate = x.Date.Date;
            if (!e.LastDate.HasValue || x.Date.Date > e.LastDate.Value) e.LastDate = x.Date.Date;
        }

        // ================================================================== clean-up
        /// <summary>
        /// Removes only generated reservations (members + history go with them) and
        /// their notifications.
        /// </summary>
        public static int ClearSampleReservations()
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                return db.InTransaction(IsolationLevel.ReadCommitted, (con, tx) =>
                {
                    QCUCRAM_Db.Exec(con, tx,
                        @"DELETE FROM Notifications
                           WHERE ReservationID IN (SELECT ReservationID FROM Reservations WHERE Purpose LIKE @p)",
                        QCUCRAM_Db.P("@p", PurposePrefix + "%"));

                    return QCUCRAM_Db.Exec(con, tx,
                        "DELETE FROM Reservations WHERE Purpose LIKE @p",
                        QCUCRAM_Db.P("@p", PurposePrefix + "%"));
                });
            }
        }

        /// <summary>
        /// Removes sample students AND everything that belongs to them. Returns students
        /// deleted. Real accounts are never touched (a sample student is recognised by
        /// the @cram.test e-mail).
        /// </summary>
        public static int DeleteSampleStudents()
        {
            string ids = "StudentID IN (SELECT StudentID FROM Students WHERE " + SampleStudentWhere + ")";

            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                return db.InTransaction(IsolationLevel.ReadCommitted, (con, tx) =>
                {
                    QCUCRAM_Db.Exec(con, tx, "DELETE FROM Notifications WHERE " + ids, SampleParams());
                    QCUCRAM_Db.Exec(con, tx, "DELETE FROM Reservations WHERE " + ids, SampleParams());
                    return QCUCRAM_Db.Exec(con, tx, "DELETE FROM Students WHERE " + SampleStudentWhere, SampleParams());
                });
            }
        }

        /// <summary>
        /// Wipes ALL reservations and notifications and resets every student's
        /// strikes/restriction. Students and accounts are kept. Returns how many
        /// reservations were deleted.
        /// </summary>
        public static int ClearAllReservations()
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                return db.InTransaction(IsolationLevel.ReadCommitted, (con, tx) =>
                {
                    QCUCRAM_Db.Exec(con, tx, "DELETE FROM Notifications");
                    int deleted = QCUCRAM_Db.Exec(con, tx, "DELETE FROM Reservations");
                    QCUCRAM_Db.Exec(con, tx,
                        "UPDATE Students SET NoShowCount = 0, IsRestricted = 0, RestrictionLiftDate = NULL");
                    return deleted;
                });
            }
        }

        // ================================================================== private helpers
        private static int GetInt(string key, int fallback)
        {
            using (QCUCRAM_Db db = new QCUCRAM_Db())
            {
                return new QCUCRAM_ContentRepository(db).GetSettingInt(key, fallback);
            }
        }

        /// <summary>Self-opening SELECT that returns a DataTable (the source used Guades_QCUCram_Db.Query).</summary>
        private static DataTable Table(string sql, params SqlParameter[] parameters)
        {
            using (SqlConnection con = new SqlConnection(QCUCRAM_AppSettings.ConnectionString))
            {
                con.Open();
                return QCUCRAM_Db.Table(con, null, sql, parameters);
            }
        }

        private static void AddHistory(SqlConnection con, SqlTransaction tx, int reservationId,
                                       string oldStatus, string newStatus, string role, int? changedBy,
                                       DateTime at, string notes)
        {
            QCUCRAM_Db.Exec(con, tx,
                @"INSERT INTO ReservationStatusHistory
                      (ReservationID, OldStatus, NewStatus, ChangedByRole, ChangedBy, ChangedAt, Notes)
                  VALUES (@r, @o, @n, @role, @by, @at, @notes)",
                QCUCRAM_Db.P("@r", reservationId),
                QCUCRAM_Db.P("@o", oldStatus),
                QCUCRAM_Db.P("@n", newStatus),
                QCUCRAM_Db.P("@role", role),
                QCUCRAM_Db.P("@by", changedBy),
                QCUCRAM_Db.P("@at", at),
                QCUCRAM_Db.P("@notes", notes));
        }

        /// <summary>
        /// Notifications.Message is NVARCHAR(400). Anything older than 2 days is already
        /// "read".
        /// </summary>
        private static void AddNotification(Env e, SqlConnection con, SqlTransaction tx, int studentId,
                                            int reservationId, DateTime at, string message)
        {
            if (message != null && message.Length > 400) message = message.Substring(0, 397) + "...";
            QCUCRAM_Db.Exec(con, tx,
                @"INSERT INTO Notifications (StudentID, ReservationID, Message, IsRead, CreatedAt)
                  VALUES (@s, @r, @m, @read, @at)",
                QCUCRAM_Db.P("@s", studentId),
                QCUCRAM_Db.P("@r", reservationId),
                QCUCRAM_Db.P("@m", message),
                QCUCRAM_Db.P("@read", at < e.Now.AddDays(-2)),
                QCUCRAM_Db.P("@at", at));
        }
    }
}
