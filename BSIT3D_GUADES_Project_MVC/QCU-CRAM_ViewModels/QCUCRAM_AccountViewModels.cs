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
        [Required(ErrorMessage = "Please enter your ID.")]
        [Display(Name = "Student Number / ID")]
        public string Identifier { get; set; }

        [Required(ErrorMessage = "Please enter your password.")]
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
        [Required(ErrorMessage = "Please enter your current password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "Please choose a new password.")]
        [StringLength(64, MinimumLength = 8, ErrorMessage = "The new password must be at least 8 characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Please re-type the new password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The two new passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }

    /// <summary>Everything the registration form posts.</summary>
    public class QCUCRAM_RegisterViewModel
    {
        [Required(ErrorMessage = "Please enter your ID / student number.")]
        [Display(Name = "Student Number / ID")]
        public string StudentNumber { get; set; }

        [Required(ErrorMessage = "Please enter your last name.")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Please enter your first name.")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Display(Name = "M.I.")]
        [StringLength(2, ErrorMessage = "The middle initial is at most 2 letters.")]
        public string MI { get; set; }

        [Required(ErrorMessage = "Please enter your e-mail address.")]
        [EmailAddress(ErrorMessage = "That does not look like an e-mail address.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please choose your program.")]
        public string Program { get; set; }

        [Required(ErrorMessage = "Please choose your requester type.")]
        [Display(Name = "Requester Type")]
        public string RequesterType { get; set; }

        [Required(ErrorMessage = "Please choose a password.")]
        [StringLength(64, MinimumLength = 8, ErrorMessage = "The password must be at least 8 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "Please re-type the password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The two passwords do not match.")]
        public string ConfirmPassword { get; set; }

        public QCUCRAM_RegisterViewModel()
        {
            RequesterType = "Student";
        }

        /// <summary>Filled by the controller so the drop-downs can be re-rendered.</summary>
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
