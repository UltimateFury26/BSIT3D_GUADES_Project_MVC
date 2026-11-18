using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    // =====================================================================
    //  Phase 04 - account forms.
    //  These are the MVC replacements for the Web Forms Login.aspx and
    //  Register.aspx controls. The validation attributes carry the same
    //  rules the ASP.NET validators carried, so the browser still blocks
    //  an empty form and the server still re-checks everything.
    // =====================================================================

    /// <summary>Everything the login form posts.</summary>
    public class QCUCRAM_LoginViewModel
    {
        /// <summary>"Student" or "Admin" - drives which lookup runs.</summary>
        [Required(ErrorMessage = "Please choose an account type.")]
        public string Role { get; set; }

        /// <summary>Student number / ID for students, username for admins.</summary>
        [Required(ErrorMessage = "Student number / ID is required.")]
        [Display(Name = "Student Number / ID")]
        public string Identifier { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        /// <summary>Where to go after a successful sign-in. Validated by SafeReturnUrl.</summary>
        public string ReturnUrl { get; set; }

        public QCUCRAM_LoginViewModel()
        {
            Role = QCUCRAM_Constants.Roles.Student;
        }

        /// <summary>The label the Web Forms page swapped when Admin was picked.</summary>
        public string IdentifierLabel
        {
            get
            {
                return Role == QCUCRAM_Constants.Roles.Admin ? "Username" : "Student Number / ID";
            }
        }

        public string IdentifierPlaceholder
        {
            get
            {
                return Role == QCUCRAM_Constants.Roles.Admin ? "username" : "23-0001";
            }
        }
    }

    /// <summary>
    /// Phase 04 - the Change Password form (the Web Forms page was
    /// Guades_QCUCram_Account/Guades_QCUCram_ChangePassword.aspx). It is used by
    /// both roles, so it only carries the three password fields.
    /// </summary>
    public class QCUCRAM_ChangePasswordViewModel
    {
        // Phase 07: wording copied from the Web Forms validators. The strength rule is NOT an
        // attribute - the original showed "Password is too weak..." as a toast from the server.
        [Required(ErrorMessage = "Current password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "New password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Please confirm the new password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }

    /// <summary>Everything the registration form posts.</summary>
    public class QCUCRAM_RegisterViewModel
    {
        // Phase 07: every message below is the text of the matching Web Forms validator.
        [Required(ErrorMessage = "ID / Student Number is required.")]
        [Display(Name = "Student Number / ID")]
        public string StudentNumber { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        // The original box is MaxLength=2; the server trims, upper-cases and cuts to 2.
        [Display(Name = "M.I.")]
        public string MI { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [RegularExpression(@"^\w+([-+.']\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$",
            ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please select a program.")]
        public string Program { get; set; }

        [Required(ErrorMessage = "Please select a requester type.")]
        [Display(Name = "Requester Type")]
        public string RequesterType { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "Please confirm your password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }

        /// <summary>Drop-down choices.</summary>
        public IEnumerable<string> ProgramChoices
        {
            get { return QCUCRAM_Programs.All; }
        }

        public IEnumerable<string> RequesterTypeChoices
        {
            get { return QCUCRAM_Programs.RequesterTypes; }
        }
    }
}
