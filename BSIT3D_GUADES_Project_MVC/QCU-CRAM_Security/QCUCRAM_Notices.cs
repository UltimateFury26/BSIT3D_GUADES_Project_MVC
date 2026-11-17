using System;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Security
{
    /// <summary>
    /// The single translation table from ?msg=code to the sentence the user reads.
    /// The wording is copied verbatim from Guades_QCUCram_Login.aspx.cs so the ported
    /// screens say exactly what the Web Forms screens said.
    /// </summary>
    public static class QCUCRAM_Notices
    {
        /// <summary>The sentence for a ?msg= code, or null when the code is unknown.</summary>
        public static string TextFor(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) { return null; }

            switch (code.Trim().ToLowerInvariant())
            {
                case QCUCRAM_Constants.Messages.Login:
                    return "Please log in to continue.";

                case QCUCRAM_Constants.Messages.Role:
                    return "You do not have access to that page. Log in with the correct account type.";

                case QCUCRAM_Constants.Messages.Registered:
                    return "Registration successful. You can now log in.";

                case QCUCRAM_Constants.Messages.LoggedOut:
                    return "You have been logged out.";

                case QCUCRAM_Constants.Messages.PasswordChanged:
                    return "Password changed. Please log in with your new password.";

                case QCUCRAM_Constants.Messages.Deactivated:
                    return "Your account has been deactivated. Please contact the administrator.";

                default:
                    return null;
            }
        }

        /// <summary>True when the code should be shown as a red message rather than a green one.</summary>
        public static bool IsError(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) { return false; }

            switch (code.Trim().ToLowerInvariant())
            {
                case QCUCRAM_Constants.Messages.Login:
                case QCUCRAM_Constants.Messages.Role:
                case QCUCRAM_Constants.Messages.Deactivated:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>"success" or "error" - the value the layout puts on the alert element.</summary>
        public static string KindFor(string code)
        {
            return IsError(code) ? "error" : "success";
        }
    }
}
