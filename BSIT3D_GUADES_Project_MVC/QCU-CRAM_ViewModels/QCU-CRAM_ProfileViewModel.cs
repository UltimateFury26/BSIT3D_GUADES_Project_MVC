using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>
    /// Phase 06 - the model behind Views/Student/QCU-CRAM_Profile.cshtml.
    ///
    /// Port of Guades_QCUCram_Student/Guades_QCUCram_Profile.aspx: the standing banner,
    /// the read-only identity block, the editable contact fields, the reservation-history
    /// summary table and the Account security card.
    ///
    /// The validation attributes mirror the ASP.NET validators that were on the Web Forms
    /// page; QCUCRAM_StudentRepository.UpdateProfile re-checks all of them on the server.
    /// </summary>
    public class QCUCRAMProfileViewModel
    {
        // ---- read-only identity ----

        public int StudentID { get; set; }
        public string StudentNumber { get; set; }
        public string RequesterType { get; set; }

        // ---- editable fields ----

        [Required(ErrorMessage = "Please enter your last name.")]
        [StringLength(60, ErrorMessage = "The last name is at most 60 characters.")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Please enter your first name.")]
        [StringLength(60, ErrorMessage = "The first name is at most 60 characters.")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Display(Name = "M.I.")]
        [StringLength(2, ErrorMessage = "The middle initial is at most 2 letters.")]
        public string MI { get; set; }

        [Required(ErrorMessage = "Please enter your e-mail address.")]
        [StringLength(120, ErrorMessage = "The e-mail address is at most 120 characters.")]
        [EmailAddress(ErrorMessage = "That does not look like an e-mail address.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please choose your program.")]
        public string Program { get; set; }

        // ---- standing ----

        public bool IsRestricted { get; set; }
        public int NoShowCount { get; set; }
        public int NoShowThreshold { get; set; }
        public DateTime? RestrictionLiftDate { get; set; }

        public QCUCRAMReservationStats Stats { get; set; }

        /// <summary>The drop-down source; QCUCRAM_Programs.All.</summary>
        public IEnumerable<string> ProgramChoices
        {
            get { return QCUCRAM_Programs.All; }
        }

        public string FullName
        {
            get
            {
                string mi = string.IsNullOrWhiteSpace(MI) ? string.Empty : " " + MI + ".";
                return FirstName + mi + " " + LastName;
            }
        }

        /// <summary>restricted | warning | good - drives the banner colour.</summary>
        public string Standing
        {
            get
            {
                if (IsRestricted) { return "restricted"; }
                return NoShowCount > 0 ? "warning" : "good";
            }
        }

        public string RestrictionLiftLabel
        {
            get
            {
                return RestrictionLiftDate.HasValue
                    ? RestrictionLiftDate.Value.ToString("MMM d, yyyy h:mm tt")
                    : "not set";
            }
        }

        /// <summary>Strikes left before the account is restricted.</summary>
        public int StrikesLeft
        {
            get
            {
                int left = NoShowThreshold - NoShowCount;
                return left < 0 ? 0 : left;
            }
        }
    }

    /// <summary>The reservation-history summary table on the profile page.</summary>
    public class QCUCRAMReservationStats
    {
        public int Total { get; set; }
        public int Pending { get; set; }
        public int Approved { get; set; }
        public int CheckedIn { get; set; }
        public int Completed { get; set; }
        public int Forfeited { get; set; }
        public int Cancelled { get; set; }
        public int Declined { get; set; }
    }
}
