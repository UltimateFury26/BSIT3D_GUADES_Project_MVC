using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers
{
    /// <summary>
    /// PBKDF2-SHA256 password hashing, carried over from the Web Forms PasswordHelper
    /// without changing the stored format. Existing hashes keep working, which matters
    /// because the database is shared with the old application.
    ///
    /// Stored format: base64(salt) + ":" + base64(hash), 16-byte salt, 32-byte hash,
    /// 100,000 iterations.
    /// </summary>
    public static class QCUCRAM_PasswordHash
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100000;

        // ---------------- Password policy (one place for the whole system) ----------------
        // Applied when a password is CREATED or CHANGED. Login never applies it, so older
        // accounts such as admin123 can still sign in.
        public const int MinLength = 8;
        public const int MaxLength = 100;

        /// <summary>What is still missing, e.g. "at least 8 characters". Empty list = OK.</summary>
        public static List<string> GetStrengthIssues(string password)
        {
            List<string> issues = new List<string>();
            password = password ?? "";

            bool upper = false, lower = false, digit = false, symbol = false;
            foreach (char c in password)
            {
                if (c >= 'A' && c <= 'Z') { upper = true; }
                else if (c >= 'a' && c <= 'z') { lower = true; }
                else if (c >= '0' && c <= '9') { digit = true; }
                else if (!char.IsWhiteSpace(c)) { symbol = true; }
            }

            if (password.Length < MinLength) { issues.Add("at least " + MinLength + " characters"); }
            if (password.Length > MaxLength) { issues.Add("no more than " + MaxLength + " characters"); }
            if (!upper) { issues.Add("an uppercase letter"); }
            if (!lower) { issues.Add("a lowercase letter"); }
            if (!digit) { issues.Add("a number"); }
            if (!symbol) { issues.Add("a symbol (for example ! @ # $ %)"); }
            return issues;
        }

        /// <summary>null when the password is acceptable, otherwise a user-friendly sentence.</summary>
        public static string GetStrengthMessage(string password)
        {
            List<string> issues = GetStrengthIssues(password);
            if (issues.Count == 0) { return null; }
            return "Password is too weak. It needs " + string.Join(", ", issues) + ".";
        }

        // ---------------- Simple temporary passwords ----------------
        // An admin hands these out and the user changes them after the first login, so they are
        // deliberately simple and predictable (the strength policy is NOT applied to them).
        //   Library assistant: admin1, admin2, admin6 ...  (the admin's number in the list)
        //   Student / others : student0001 for ID 23-0001 (the digits after the last dash)
        public static string AdminTemporaryPassword(int adminNumber)
        {
            if (adminNumber < 1) { adminNumber = 1; }
            return "admin" + adminNumber;
        }

        public static string StudentTemporaryPassword(string studentNumber)
        {
            string s = (studentNumber ?? "").Trim();
            int dash = s.LastIndexOf('-');
            string tail = dash >= 0 ? s.Substring(dash + 1) : s;

            StringBuilder sb = new StringBuilder();
            foreach (char c in tail)
            {
                if (char.IsLetterOrDigit(c)) { sb.Append(c); }
            }
            string part = sb.ToString();

            if (part.Length == 0) { part = "0000"; }
            bool allDigits = true;
            foreach (char c in part)
            {
                if (c < '0' || c > '9') { allDigits = false; break; }
            }
            if (allDigits && part.Length < 4) { part = part.PadLeft(4, '0'); }

            return "student" + part;
        }

        /// <summary>
        /// A random temporary password that always passes the policy. No look-alike characters
        /// (0/O, 1/l/I) so it is easy to read out over the counter.
        /// </summary>
        public static string GenerateTemporaryPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string digits = "23456789";
            const string symbols = "!@#$%*?";
            string all = upper + lower + digits + symbols;
            const int length = 12;

            List<char> chars = new List<char>();
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                chars.Add(upper[RandomIndex(rng, upper.Length)]);
                chars.Add(lower[RandomIndex(rng, lower.Length)]);
                chars.Add(digits[RandomIndex(rng, digits.Length)]);
                chars.Add(symbols[RandomIndex(rng, symbols.Length)]);
                while (chars.Count < length) { chars.Add(all[RandomIndex(rng, all.Length)]); }

                // Fisher-Yates shuffle so the guaranteed characters are not always first
                for (int i = chars.Count - 1; i > 0; i--)
                {
                    int j = RandomIndex(rng, i + 1);
                    char tmp = chars[i];
                    chars[i] = chars[j];
                    chars[j] = tmp;
                }
            }
            return new string(chars.ToArray());
        }

        /// <summary>Unbiased random number in [0, max).</summary>
        private static int RandomIndex(RandomNumberGenerator rng, int max)
        {
            byte[] b = new byte[4];
            uint limit = uint.MaxValue - (uint.MaxValue % (uint)max);
            uint v;
            do
            {
                rng.GetBytes(b);
                v = BitConverter.ToUInt32(b, 0);
            } while (v >= limit);
            return (int)(v % (uint)max);
        }

        /// <summary>Returns "salt:hash" (both Base64) to store in PasswordHash.</summary>
        public static string Hash(string password)
        {
            byte[] salt = new byte[SaltSize];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            byte[] hash = Derive(password, salt);
            return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored)) { return false; }

            string[] parts = stored.Split(':');
            if (parts.Length != 2) { return false; }

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(parts[0]);
                expected = Convert.FromBase64String(parts[1]);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] actual = Derive(password, salt);
            return SlowEquals(expected, actual);
        }

        private static byte[] Derive(string password, byte[] salt)
        {
            using (Rfc2898DeriveBytes pbkdf2 =
                   new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(HashSize);
            }
        }

        /// <summary>Compares every byte so timing does not reveal where a mismatch occurred.</summary>
        private static bool SlowEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) { return false; }
            int diff = 0;
            for (int i = 0; i < a.Length; i++) { diff |= a[i] ^ b[i]; }
            return diff == 0;
        }

        /// <summary>
        /// A real hash of a throwaway password. Verifying against this when the account does not
        /// exist keeps the response time the same, so timing cannot reveal unknown IDs.
        /// </summary>
        public const string DummyHash =
            "AAAAAAAAAAAAAAAAAAAAAA==:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    }
}
