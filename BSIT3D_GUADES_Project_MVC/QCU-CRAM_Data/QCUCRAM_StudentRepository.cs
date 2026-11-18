using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data
{
    /// <summary>
    /// Every read and write of dbo.Students. Method-for-method port of the Web Forms
    /// Register / Login / Profile / Students pages, with the same ResultCode contracts.
    /// </summary>
    public class QCUCRAM_StudentRepository
    {
        private static readonly Regex StudentNumberRegex = new Regex(@"^\d{2}-\d{4}$");
        private static readonly Regex OtherIdRegex = new Regex(@"^[A-Za-z0-9\-]{3,20}$");

        private readonly QCUCRAM_Db _db;

        public QCUCRAM_StudentRepository() : this(new QCUCRAM_Db()) { }

        public QCUCRAM_StudentRepository(QCUCRAM_Db db)
        {
            if (db == null) { throw new ArgumentNullException("db"); }
            _db = db;
        }

        public QCUCRAM_Db Db { get { return _db; } }

        // =================================================================
        //  Registration
        // =================================================================

        /// <summary>
        /// 0 = OK, 1 = ID / student number taken, 2 = email taken, 3 = invalid input.
        /// The password is hashed here; the plain text never reaches the database.
        /// R04: the Register page enforces the password policy (default). The admin Students
        /// page passes enforcePasswordPolicy = false because the source never applied the
        /// policy to the temporary password (student0001 would always fail it).
        /// </summary>
        public QCUCRAM_RegisterResult Register(string studentNumber, string lastName, string firstName,
                                               string mi, string email, string program,
                                               string requesterType, string password,
                                               bool enforcePasswordPolicy = true)
        {
            QCUCRAM_RegisterResult result = new QCUCRAM_RegisterResult { ResultCode = 3 };

            string number = (studentNumber ?? "").Trim();
            string last = (lastName ?? "").Trim();
            string first = (firstName ?? "").Trim();
            string middle = (mi ?? "").Trim().TrimEnd('.').ToUpperInvariant();
            string mail = (email ?? "").Trim();

            if (middle.Length > 2) { middle = middle.Substring(0, 2); }
            if (requesterType == "Student") { number = QCUCRAM_Programs.NormalizeStudentNumber(number); }

            // ---- server-side re-checks (browser validation can be bypassed) ----
            if (!QCUCRAM_Programs.IsValidRequesterType(requesterType)) { return result; }
            if (!QCUCRAM_Programs.IsValidProgram(program)) { return result; }

            if (requesterType == "Student")
            {
                if (!StudentNumberRegex.IsMatch(number)) { return result; }
            }
            else if (!OtherIdRegex.IsMatch(number))
            {
                return result;
            }

            if (last.Length == 0 || first.Length == 0) { return result; }
            if (last.Length > 60 || first.Length > 60) { return result; }
            if (mail.Length == 0 || mail.Length > 120) { return result; }
            if (enforcePasswordPolicy && QCUCRAM_PasswordHash.GetStrengthMessage(password) != null) { return result; }

            // ---- friendly duplicate checks ----
            if (_db.Any("SELECT COUNT(*) AS Total FROM Students WHERE StudentNumber = @n",
                        QCUCRAM_Db.P("@n", number)))
            {
                result.ResultCode = 1;
                return result;
            }
            if (_db.Any("SELECT COUNT(*) AS Total FROM Students WHERE Email = @e",
                        QCUCRAM_Db.P("@e", mail)))
            {
                result.ResultCode = 2;
                return result;
            }

            try
            {
                // NoShowCount / IsRestricted / CreatedAt use their column defaults.
                QCUCRAM_NewIdResult inserted = _db.QuerySingle<QCUCRAM_NewIdResult>(
                    @"INSERT INTO Students
                          (StudentNumber, LastName, FirstName, MI, Email, Program, RequesterType, PasswordHash)
                      OUTPUT INSERTED.StudentID AS NewId
                      VALUES (@n, @last, @first, @mi, @e, @prog, @type, @hash)",
                    QCUCRAM_Db.P("@n", number),
                    QCUCRAM_Db.P("@last", last),
                    QCUCRAM_Db.P("@first", first),
                    QCUCRAM_Db.P("@mi", middle.Length == 0 ? null : middle),
                    QCUCRAM_Db.P("@e", mail),
                    QCUCRAM_Db.P("@prog", program),
                    QCUCRAM_Db.P("@type", requesterType),
                    QCUCRAM_Db.P("@hash", QCUCRAM_PasswordHash.Hash(password)));

                result.ResultCode = 0;
                result.StudentId = inserted.NewId;
                return result;
            }
            catch (SqlException ex)
            {
                // 2601 / 2627 = unique index hit by a simultaneous registration
                if (ex.Number == 2601 || ex.Number == 2627)
                {
                    bool numberTaken = _db.Any(
                        "SELECT COUNT(*) AS Total FROM Students WHERE StudentNumber = @n",
                        QCUCRAM_Db.P("@n", number));
                    result.ResultCode = numberTaken ? 1 : 2;
                    return result;
                }
                throw;
            }
        }

        // =================================================================
        //  Sign in
        // =================================================================

        /// <summary>
        /// 0 = OK, 1 = invalid credentials. Verifies the PBKDF2 hash and auto-lifts an
        /// expired restriction on the way in (the sweep retries if this fails).
        /// </summary>
        public QCUCRAM_LoginResult Login(string studentNumber, string password)
        {
            QCUCRAM_Student row = null;
            try
            {
                row = GetByStudentNumber(studentNumber);
            }
            catch (Exception)
            {
                // A database problem must not look like "invalid credentials".
                return new QCUCRAM_LoginResult { ResultCode = 2 };
            }

            if (row == null || string.IsNullOrEmpty(row.PasswordHash))
            {
                // Spend the same time as a real check so timing cannot reveal unknown IDs.
                QCUCRAM_PasswordHash.Verify(password, QCUCRAM_PasswordHash.DummyHash);
                return new QCUCRAM_LoginResult { ResultCode = 1 };
            }

            if (!QCUCRAM_PasswordHash.Verify(password, row.PasswordHash))
            {
                return new QCUCRAM_LoginResult { ResultCode = 1 };
            }

            try { AutoLift(row.StudentID, QCUCRAM_Clock.Now); }
            catch { /* never block a login because the auto-lift failed */ }

            return new QCUCRAM_LoginResult
            {
                ResultCode = 0,
                UserId = row.StudentID,
                Username = row.StudentNumber,
                FullName = (row.FirstName + " " + row.LastName).Trim(),
                Role = "Student"
            };
        }

        /// <summary>
        /// Tries the typed ID, its dashed form and its undashed form - a student who types
        /// 230001 should still get in when the stored value is 23-0001.
        /// </summary>
        public QCUCRAM_LoginResult LoginWithAnyNumberForm(string typed, string password)
        {
            QCUCRAM_LoginResult last = new QCUCRAM_LoginResult { ResultCode = 1 };
            foreach (string candidate in StudentNumberCandidates(typed))
            {
                last = Login(candidate, password);
                if (last.ResultCode == 0) { return last; }
                if (last.ResultCode == 2) { return last; }   // database problem, stop trying
            }
            return last;
        }

        public static IEnumerable<string> StudentNumberCandidates(string typed)
        {
            List<string> list = new List<string>();
            string value = (typed ?? "").Trim();
            string[] options =
            {
                value,
                QCUCRAM_Programs.NormalizeStudentNumber(value),
                value.Replace("-", "")
            };
            foreach (string o in options)
            {
                if (o.Length > 0 && !list.Contains(o)) { list.Add(o); }
            }
            return list;
        }

        // =================================================================
        //  Reads
        // =================================================================

        private const string SelectStudent =
            @"SELECT StudentID, StudentNumber, LastName, FirstName, MI, Email, Program,
                     RequesterType, PasswordHash, NoShowCount, IsRestricted,
                     RestrictionLiftDate, CreatedAt
                FROM Students";

        public QCUCRAM_Student GetById(int studentId)
        {
            return _db.QueryFirstOrDefault<QCUCRAM_Student>(
                SelectStudent + " WHERE StudentID = @id",
                QCUCRAM_Db.P("@id", studentId));
        }

        public QCUCRAM_Student GetByStudentNumber(string studentNumber)
        {
            return _db.QueryFirstOrDefault<QCUCRAM_Student>(
                SelectStudent + " WHERE StudentNumber = @n",
                QCUCRAM_Db.P("@n", (studentNumber ?? "").Trim()));
        }

        public QCUCRAM_Student GetByEmail(string email)
        {
            return _db.QueryFirstOrDefault<QCUCRAM_Student>(
                SelectStudent + " WHERE Email = @e",
                QCUCRAM_Db.P("@e", (email ?? "").Trim()));
        }

        /// <summary>
        /// Admin list. Every filter is optional; null / blank means "no filter".
        /// </summary>
        public List<QCUCRAM_Student> Search(string search = null, string requesterType = null,
                                            bool? restricted = null)
        {
            string sql = SelectStudent + " WHERE 1 = 1";
            List<SqlParameter> p = new List<SqlParameter>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                sql += @" AND (StudentNumber LIKE @q OR LastName LIKE @q
                          OR FirstName LIKE @q OR Email LIKE @q)";
                p.Add(QCUCRAM_Db.P("@q", "%" + search.Trim() + "%"));
            }
            if (!string.IsNullOrWhiteSpace(requesterType))
            {
                sql += " AND RequesterType = @type";
                p.Add(QCUCRAM_Db.P("@type", requesterType.Trim()));
            }
            if (restricted.HasValue)
            {
                sql += " AND IsRestricted = @r";
                p.Add(QCUCRAM_Db.P("@r", restricted.Value));
            }

            sql += " ORDER BY LastName, FirstName";
            return _db.Query<QCUCRAM_Student>(sql, p.ToArray());
        }

        /// <summary>Requesters whose restriction has expired but who are still flagged.</summary>
        public List<QCUCRAM_Student> GetExpiredRestrictions(DateTime now)
        {
            return _db.Query<QCUCRAM_Student>(
                SelectStudent + @" WHERE IsRestricted = 1
                     AND RestrictionLiftDate IS NOT NULL
                     AND RestrictionLiftDate <= @now
                   ORDER BY RestrictionLiftDate",
                QCUCRAM_Db.P("@now", now));
        }

        public int CountAll()
        {
            return _db.Count("SELECT COUNT(*) AS Total FROM Students");
        }

        public int CountRestricted()
        {
            return _db.Count("SELECT COUNT(*) AS Total FROM Students WHERE IsRestricted = 1");
        }

        // =================================================================
        //  Profile
        // =================================================================

        /// <summary>0 = OK, 1 = email taken, 2 = not found, 3 = invalid.</summary>
        public int UpdateProfile(int studentId, string lastName, string firstName, string mi,
                                 string email, string program)
        {
            string last = (lastName ?? "").Trim();
            string first = (firstName ?? "").Trim();
            string middle = (mi ?? "").Trim().TrimEnd('.');
            string mail = (email ?? "").Trim();

            QCUCRAM_Student existing = GetById(studentId);
            if (existing == null) { return 2; }

            // Same server-side re-checks as btnSave_Click (browser checks can be bypassed).
            if (last.Length == 0 || first.Length == 0 || last.Length > 60 || first.Length > 60) { return 3; }
            if (middle.Length > 5) { return 3; }
            if (mail.Length == 0 || mail.Length > 120) { return 3; }
            // A saved program that has since left the list may be kept, nothing else.
            if (!QCUCRAM_Programs.IsValidProgram(program) &&
                !string.Equals(program, existing.Program, StringComparison.Ordinal)) { return 3; }

            if (!string.Equals(existing.Email, mail, StringComparison.OrdinalIgnoreCase))
            {
                bool taken = _db.Any(
                    "SELECT COUNT(*) AS Total FROM Students WHERE Email = @e AND StudentID <> @id",
                    QCUCRAM_Db.P("@e", mail), QCUCRAM_Db.P("@id", studentId));
                if (taken) { return 1; }
            }

            _db.Execute(
                @"UPDATE Students
                     SET LastName = @last, FirstName = @first, MI = @mi,
                         Email = @e, Program = @prog
                   WHERE StudentID = @id",
                QCUCRAM_Db.P("@last", last),
                QCUCRAM_Db.P("@first", first),
                QCUCRAM_Db.P("@mi", middle.Length == 0 ? null : middle),
                QCUCRAM_Db.P("@e", mail),
                QCUCRAM_Db.P("@prog", program),
                QCUCRAM_Db.P("@id", studentId));

            return 0;
        }

        /// <summary>0 = OK, 1 = wrong current password, 2 = not found, 3 = new password too weak.</summary>
        /// <summary>
        /// Admin search used by the Students maintenance screen. Mirrors the source
        /// LIKE-escaped search across number, names, e-mail and program, including the
        /// computed "First Last" and "Last, First" forms, ordered by last/first name.
        /// </summary>
        public List<QCUCRAM_Student> SearchAdmin(string search)
        {
            string sql = SelectStudent + " WHERE 1 = 1";
            List<SqlParameter> p = new List<SqlParameter>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string q = "%" + EscapeLike(search.Trim()) + "%";

                sql += @" AND (StudentNumber LIKE @q ESCAPE '\'
                          OR LastName LIKE @q ESCAPE '\'
                          OR FirstName LIKE @q ESCAPE '\'
                          OR Email LIKE @q ESCAPE '\'
                          OR Program LIKE @q ESCAPE '\'
                          OR (FirstName + ' ' + LastName) LIKE @q ESCAPE '\'
                          OR (LastName + ', ' + FirstName) LIKE @q ESCAPE '\')";

                p.Add(QCUCRAM_Db.P("@q", q));
            }

            sql += " ORDER BY LastName, FirstName, StudentID";
            return _db.Query<QCUCRAM_Student>(sql, p.ToArray());
        }

        private static string EscapeLike(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_")
                .Replace("[", "\\[");
        }

        /// <summary>
        /// Admin edit from the Students page. Unlike the self-service
        /// <see cref="UpdateProfile"/> this also writes StudentNumber and
        /// RequesterType, exactly like the source SaveEdit().
        /// 0 = OK, 1 = e-mail taken, 2 = not found, 3 = invalid, 4 = ID taken.
        /// </summary>
        public int UpdateAdminProfile(int studentId, string studentNumber, string lastName,
                                      string firstName, string mi, string email,
                                      string program, string requesterType)
        {
            // R04: Students.aspx.cs SaveEdit() - no 23-0001 rule and no number normalisation on
            // edit (older IDs such as SAMPLE-001 stay editable), middle initial is not upper-cased.
            string number = (studentNumber ?? "").Trim();
            string last = (lastName ?? "").Trim();
            string first = (firstName ?? "").Trim();
            string middle = (mi ?? "").Trim().TrimEnd('.');
            string mail = (email ?? "").Trim();
            string type = (requesterType ?? "").Trim();
            string prog = (program ?? "").Trim();

            if (!QCUCRAM_Programs.IsValidRequesterType(type)) { return 3; }
            if (!OtherIdRegex.IsMatch(number)) { return 3; }
            if (middle.Length > 5) { return 3; }
            if (last.Length == 0 || first.Length == 0 || last.Length > 60 || first.Length > 60) { return 3; }
            if (mail.Length == 0 || mail.Length > 120) { return 3; }

            QCUCRAM_Student existing = GetById(studentId);
            if (existing == null) { return 2; }

            // the stored program stays valid even if it is no longer in the list
            if (!QCUCRAM_Programs.IsValidProgram(prog) && prog != existing.Program) { return 3; }

            if (!string.Equals(existing.StudentNumber, number, StringComparison.OrdinalIgnoreCase))
            {
                bool idTaken = _db.Any(
                    "SELECT COUNT(*) AS Total FROM Students WHERE StudentNumber = @n AND StudentID <> @id",
                    QCUCRAM_Db.P("@n", number), QCUCRAM_Db.P("@id", studentId));
                if (idTaken) { return 4; }
            }

            if (!string.Equals(existing.Email, mail, StringComparison.OrdinalIgnoreCase))
            {
                bool taken = _db.Any(
                    "SELECT COUNT(*) AS Total FROM Students WHERE Email = @e AND StudentID <> @id",
                    QCUCRAM_Db.P("@e", mail), QCUCRAM_Db.P("@id", studentId));
                if (taken) { return 1; }
            }

            _db.Execute(
                @"UPDATE Students
                     SET StudentNumber = @n, LastName = @last, FirstName = @first, MI = @mi,
                         Email = @e, Program = @prog, RequesterType = @type
                   WHERE StudentID = @id",
                QCUCRAM_Db.P("@n", number),
                QCUCRAM_Db.P("@last", last),
                QCUCRAM_Db.P("@first", first),
                QCUCRAM_Db.P("@mi", middle.Length == 0 ? null : middle),
                QCUCRAM_Db.P("@e", mail),
                QCUCRAM_Db.P("@prog", prog),
                QCUCRAM_Db.P("@type", type),
                QCUCRAM_Db.P("@id", studentId));

            return 0;
        }

        public int ChangePassword(int studentId, string currentPassword, string newPassword)
        {
            QCUCRAM_Student row = GetById(studentId);
            if (row == null) { return 2; }
            if (!QCUCRAM_PasswordHash.Verify(currentPassword, row.PasswordHash)) { return 1; }
            if (QCUCRAM_PasswordHash.GetStrengthMessage(newPassword) != null) { return 3; }

            _db.Execute("UPDATE Students SET PasswordHash = @h WHERE StudentID = @id",
                QCUCRAM_Db.P("@h", QCUCRAM_PasswordHash.Hash(newPassword)),
                QCUCRAM_Db.P("@id", studentId));

            return 0;
        }

        /// <summary>Admin reset: sets a temporary password without asking for the old one.</summary>
        public int ResetPassword(int studentId, string temporaryPassword)
        {
            // R04: Students.aspx.cs ResetPassword never applied the strength policy to the
            // temporary password ("student0001" is deliberately simple), so it is not applied here.
            if (string.IsNullOrEmpty(temporaryPassword)) { return 3; }

            int rows = _db.Execute("UPDATE Students SET PasswordHash = @h WHERE StudentID = @id",
                QCUCRAM_Db.P("@h", QCUCRAM_PasswordHash.Hash(temporaryPassword)),
                QCUCRAM_Db.P("@id", studentId));

            return rows == 0 ? 2 : 0;
        }

        // =================================================================
        //  Restrictions and strikes
        // =================================================================

        /// <summary>
        /// Sets or clears the restriction flag. Passing restricted = false also resets the
        /// strike count, which is what "lift" means everywhere in the system.
        /// </summary>
        public int SetRestriction(int studentId, bool restricted, DateTime? liftDate)
        {
            if (restricted)
            {
                return _db.Execute(
                    @"UPDATE Students
                         SET IsRestricted = 1, RestrictionLiftDate = @lift
                       WHERE StudentID = @id",
                    QCUCRAM_Db.P("@lift", liftDate),
                    QCUCRAM_Db.P("@id", studentId));
            }

            return _db.Execute(
                @"UPDATE Students
                     SET IsRestricted = 0, NoShowCount = 0, RestrictionLiftDate = NULL
                   WHERE StudentID = @id",
                QCUCRAM_Db.P("@id", studentId));
        }

        public int ResetStrikes(int studentId)
        {
            return _db.Execute(
                @"UPDATE Students
                     SET NoShowCount = 0, IsRestricted = 0, RestrictionLiftDate = NULL
                   WHERE StudentID = @id",
                QCUCRAM_Db.P("@id", studentId));
        }

        /// <summary>Adds one no-show strike and returns the new count.</summary>
        public int IncrementNoShow(int studentId)
        {
            QCUCRAM_NewIdResult row = _db.QuerySingle<QCUCRAM_NewIdResult>(
                @"UPDATE Students
                     SET NoShowCount = NoShowCount + 1
                   OUTPUT INSERTED.NoShowCount AS NewId
                   WHERE StudentID = @id",
                QCUCRAM_Db.P("@id", studentId));

            return row.NewId;
        }

        /// <summary>
        /// Clears every restriction whose lift date has passed and returns how many were lifted.
        /// The caller is responsible for the notification rows.
        /// </summary>
        public int AutoLift(DateTime now)
        {
            return _db.Execute(
                @"UPDATE Students
                     SET IsRestricted = 0, NoShowCount = 0, RestrictionLiftDate = NULL
                   WHERE IsRestricted = 1
                     AND RestrictionLiftDate IS NOT NULL
                     AND RestrictionLiftDate <= @now",
                QCUCRAM_Db.P("@now", now));
        }

        /// <summary>Auto-lift for one student (called at login). Returns rows changed.</summary>
        public int AutoLift(int studentId, DateTime now)
        {
            return _db.Execute(
                @"UPDATE Students
                     SET IsRestricted = 0, NoShowCount = 0, RestrictionLiftDate = NULL
                   WHERE StudentID = @id AND IsRestricted = 1
                     AND RestrictionLiftDate IS NOT NULL AND RestrictionLiftDate <= @now",
                QCUCRAM_Db.P("@id", studentId),
                QCUCRAM_Db.P("@now", now));
        }

        // =================================================================
        //  Delete
        // =================================================================

        /// <summary>
        /// Removes a requester. Returns 0 when the student still has reservations
        /// (FK_R reservations), so the admin page can explain instead of crashing.
        /// </summary>
        /// <summary>
        /// Deletes an account only when it has no reservations, removing its
        /// notifications in the same transaction (source DeleteStudent()).
        /// 0 = deleted, -1 = FK constraint, -2 = still has reservations.
        /// </summary>
        public int Delete(int studentId)
        {
            try
            {
                int affected = _db.Execute(
                    @"SET XACT_ABORT ON;
                      BEGIN TRAN;
                      IF NOT EXISTS (SELECT 1 FROM Reservations WHERE StudentID = @id)
                      BEGIN
                          DELETE FROM Notifications WHERE StudentID = @id;
                          DELETE FROM Students WHERE StudentID = @id;
                      END
                      COMMIT;",
                    QCUCRAM_Db.P("@id", studentId));

                return affected > 0 ? 0 : -2;
            }
            catch (SqlException ex)
            {
                if (ex.Number == 547) { return -1; }   // FK constraint
                throw;
            }
        }
    }
}
