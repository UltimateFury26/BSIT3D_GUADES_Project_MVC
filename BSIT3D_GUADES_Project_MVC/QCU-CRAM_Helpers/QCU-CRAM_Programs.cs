namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers
{
    /// <summary>
    /// One place for the dropdown lists used by Register, Profile and the Students page.
    /// </summary>
    public static class QCUCRAM_Programs
    {
        public static readonly string[] RequesterTypes = { "Student", "Faculty", "Visitor" };

        /// <summary>
        /// "230001" becomes "23-0001". Anything that is not exactly six digits
        /// (with or without the dash) is returned unchanged.
        /// </summary>
        public static string NormalizeStudentNumber(string value)
        {
            value = (value ?? "").Trim();
            string digits = value.Replace("-", "");
            if (digits.Length != 6) { return value; }
            foreach (char c in digits)
            {
                if (c < '0' || c > '9') { return value; }
            }
            return digits.Substring(0, 2) + "-" + digits.Substring(2);
        }

        /// <summary>The QCU programs offered on the Register form.</summary>
        public static readonly string[] All =
        {
            "BS Information Technology",
            "BS Computer Science",
            "BS Entrepreneurship",
            "BS Accountancy",
            "BS Industrial Engineering",
            "BS Computer Engineering",
            "BS Education",
            "Not applicable (Faculty / Visitor)"
        };

        public static bool IsValidRequesterType(string value)
        {
            return System.Array.IndexOf(RequesterTypes, value) >= 0;
        }

        public static bool IsValidProgram(string value)
        {
            return System.Array.IndexOf(All, value) >= 0;
        }
    }
}
