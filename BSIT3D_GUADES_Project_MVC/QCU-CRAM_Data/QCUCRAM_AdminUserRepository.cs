using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Models;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data
{
    /// <summary>
    /// Every read and write of dbo.AdminUsers (the library assistants).
    /// </summary>
    public class QCUCRAM_AdminUserRepository
    {
        private readonly QCUCRAM_Db _db;

        public QCUCRAM_AdminUserRepository() : this(new QCUCRAM_Db()) { }

        public QCUCRAM_AdminUserRepository(QCUCRAM_Db db)
        {
            if (db == null) { throw new ArgumentNullException("db"); }
            _db = db;
        }

        private const string SelectAdmin =
            @"SELECT AdminID, Username, PasswordHash, FullName, IsActive FROM AdminUsers";

        // =================================================================
        //  Sign in
        // =================================================================

        /// <summary>
        /// 0 = OK, 1 = invalid credentials, 2 = account inactive.
        /// Only active accounts are considered, so a deactivated assistant gets the same
        /// answer as a wrong password (no account enumeration).
        /// </summary>
        public QCUCRAM_LoginResult Login(string username, string password)
        {
            QCUCRAM_AdminUser row;
            try
            {
                row = _db.QueryFirstOrDefault<QCUCRAM_AdminUser>(
                    SelectAdmin + " WHERE Username = @u",
                    QCUCRAM_Db.P("@u", (username ?? "").Trim()));
            }
            catch (Exception)
            {
                return new QCUCRAM_LoginResult { ResultCode = 2 };
            }

            if (row == null || string.IsNullOrEmpty(row.PasswordHash))
            {
                QCUCRAM_PasswordHash.Verify(password, QCUCRAM_PasswordHash.DummyHash);
                return new QCUCRAM_LoginResult { ResultCode = 1 };
            }

            if (!QCUCRAM_PasswordHash.Verify(password, row.PasswordHash))
            {
                return new QCUCRAM_LoginResult { ResultCode = 1 };
            }

            if (!row.IsActive)
            {
                return new QCUCRAM_LoginResult { ResultCode = 2 };
            }

            return new QCUCRAM_LoginResult
            {
                ResultCode = 0,
                UserId = row.AdminID,
                Username = row.Username,
                FullName = row.FullName,
                Role = "Admin"
            };
        }

        // =================================================================
        //  Reads
        // =================================================================

        public QCUCRAM_AdminUser GetById(int adminId)
        {
            return _db.QueryFirstOrDefault<QCUCRAM_AdminUser>(
                SelectAdmin + " WHERE AdminID = @id",
                QCUCRAM_Db.P("@id", adminId));
        }

        public QCUCRAM_AdminUser GetByUsername(string username)
        {
            return _db.QueryFirstOrDefault<QCUCRAM_AdminUser>(
                SelectAdmin + " WHERE Username = @u",
                QCUCRAM_Db.P("@u", (username ?? "").Trim()));
        }

        public List<QCUCRAM_AdminUser> GetAll()
        {
            return _db.Query<QCUCRAM_AdminUser>(SelectAdmin + " ORDER BY AdminID");
        }

        public int Count()
        {
            return _db.Count("SELECT COUNT(*) AS Total FROM AdminUsers");
        }

        /// <summary>
        /// The number an assistant occupies in the list, used for the "admin1, admin2 ..."
        /// temporary password. 1-based.
        /// </summary>
        public int GetPosition(int adminId)
        {
            return _db.Count(
                "SELECT COUNT(*) AS Total FROM AdminUsers WHERE AdminID <= @id",
                QCUCRAM_Db.P("@id", adminId));
        }

        // =================================================================
        //  Writes
        // =================================================================

        /// <summary>
        /// 0 = OK, 1 = username taken, 3 = invalid input.
        /// </summary>
        public int Insert(string username, string fullName, string password, out int newId)
        {
            newId = 0;

            string user = (username ?? "").Trim();
            string name = (fullName ?? "").Trim();

            if (user.Length < 3 || user.Length > 50) { return 3; }
            if (name.Length == 0 || name.Length > 120) { return 3; }
            if (QCUCRAM_PasswordHash.GetStrengthMessage(password) != null) { return 3; }

            if (_db.Any("SELECT COUNT(*) AS Total FROM AdminUsers WHERE Username = @u",
                        QCUCRAM_Db.P("@u", user)))
            {
                return 1;
            }

            try
            {
                QCUCRAM_NewIdResult row = _db.QuerySingle<QCUCRAM_NewIdResult>(
                    @"INSERT INTO AdminUsers (Username, PasswordHash, FullName, IsActive)
                      OUTPUT INSERTED.AdminID AS NewId
                      VALUES (@u, @h, @n, 1)",
                    QCUCRAM_Db.P("@u", user),
                    QCUCRAM_Db.P("@h", QCUCRAM_PasswordHash.Hash(password)),
                    QCUCRAM_Db.P("@n", name));

                newId = row.NewId;
                return 0;
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2601 || ex.Number == 2627) { return 1; }
                throw;
            }
        }

        /// <summary>0 = OK, 1 = username taken, 2 = not found, 3 = invalid.</summary>
        public int Update(int adminId, string username, string fullName)
        {
            string user = (username ?? "").Trim();
            string name = (fullName ?? "").Trim();

            if (user.Length < 3 || user.Length > 50) { return 3; }
            if (name.Length == 0 || name.Length > 120) { return 3; }

            if (GetById(adminId) == null) { return 2; }

            bool taken = _db.Any(
                "SELECT COUNT(*) AS Total FROM AdminUsers WHERE Username = @u AND AdminID <> @id",
                QCUCRAM_Db.P("@u", user), QCUCRAM_Db.P("@id", adminId));
            if (taken) { return 1; }

            _db.Execute("UPDATE AdminUsers SET Username = @u, FullName = @n WHERE AdminID = @id",
                QCUCRAM_Db.P("@u", user),
                QCUCRAM_Db.P("@n", name),
                QCUCRAM_Db.P("@id", adminId));

            return 0;
        }

        /// <summary>0 = OK, 1 = wrong current password, 2 = not found, 3 = new password too weak.</summary>
        /// <summary>
        /// Full-name-only edit, matching the source SaveEdit() (the username is fixed
        /// once the account exists). 0 = OK, 2 = not found, 3 = invalid.
        /// </summary>
        public int UpdateFullName(int adminId, string fullName)
        {
            string name = (fullName ?? "").Trim();

            if (name.Length == 0 || name.Length > 120) { return 3; }
            if (GetById(adminId) == null) { return 2; }

            _db.Execute("UPDATE AdminUsers SET FullName = @n WHERE AdminID = @id",
                QCUCRAM_Db.P("@n", name),
                QCUCRAM_Db.P("@id", adminId));

            return 0;
        }

        public int ChangePassword(int adminId, string currentPassword, string newPassword)
        {
            QCUCRAM_AdminUser row = GetById(adminId);
            if (row == null) { return 2; }
            if (!QCUCRAM_PasswordHash.Verify(currentPassword, row.PasswordHash)) { return 1; }
            if (QCUCRAM_PasswordHash.GetStrengthMessage(newPassword) != null) { return 3; }

            _db.Execute("UPDATE AdminUsers SET PasswordHash = @h WHERE AdminID = @id",
                QCUCRAM_Db.P("@h", QCUCRAM_PasswordHash.Hash(newPassword)),
                QCUCRAM_Db.P("@id", adminId));

            return 0;
        }

        /// <summary>Admin reset: sets a temporary password without asking for the old one.</summary>
        public int ResetPassword(int adminId, string temporaryPassword)
        {
            if (QCUCRAM_PasswordHash.GetStrengthMessage(temporaryPassword) != null) { return 3; }

            int rows = _db.Execute("UPDATE AdminUsers SET PasswordHash = @h WHERE AdminID = @id",
                QCUCRAM_Db.P("@h", QCUCRAM_PasswordHash.Hash(temporaryPassword)),
                QCUCRAM_Db.P("@id", adminId));

            return rows == 0 ? 2 : 0;
        }

        /// <summary>
        /// Activates or deactivates an assistant. Refuses to deactivate the last active one,
        /// because that would lock everybody out of the admin area.
        /// </summary>
        public int SetActive(int adminId, bool active)
        {
            if (!active)
            {
                int activeOthers = _db.Count(
                    "SELECT COUNT(*) AS Total FROM AdminUsers WHERE IsActive = 1 AND AdminID <> @id",
                    QCUCRAM_Db.P("@id", adminId));
                if (activeOthers == 0) { return 4; }   // would leave nobody able to sign in
            }

            return _db.Execute("UPDATE AdminUsers SET IsActive = @a WHERE AdminID = @id",
                QCUCRAM_Db.P("@a", active),
                QCUCRAM_Db.P("@id", adminId));
        }

        /// <summary>Deletes an assistant. Refuses to delete the last active one.</summary>
        public int Delete(int adminId)
        {
            int activeOthers = _db.Count(
                "SELECT COUNT(*) AS Total FROM AdminUsers WHERE IsActive = 1 AND AdminID <> @id",
                QCUCRAM_Db.P("@id", adminId));
            if (activeOthers == 0) { return 4; }

            try
            {
                return _db.Execute("DELETE FROM AdminUsers WHERE AdminID = @id",
                    QCUCRAM_Db.P("@id", adminId));
            }
            catch (SqlException ex)
            {
                if (ex.Number == 547) { return -1; }
                throw;
            }
        }

        /// <summary>
        /// Creates the default account (admin / admin123) only when the table is empty.
        /// Port of DbSeeder.EnsureDefaultAdmin - the MVC equivalent of the Web Forms startup call.
        /// </summary>
        public bool EnsureDefaultAdmin()
        {
            if (Count() > 0) { return false; }

            _db.Execute(
                @"INSERT INTO AdminUsers (Username, PasswordHash, FullName, IsActive)
                  VALUES (@u, @h, @n, 1)",
                QCUCRAM_Db.P("@u", "admin"),
                QCUCRAM_Db.P("@h", QCUCRAM_PasswordHash.Hash("admin123")),
                QCUCRAM_Db.P("@n", "Library Assistant"));

            return true;
        }
    }
}
