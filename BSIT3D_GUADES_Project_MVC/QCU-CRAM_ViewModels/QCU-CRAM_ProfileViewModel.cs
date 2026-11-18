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

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(60, ErrorMessage = "Last name and first name are required (maximum 60 characters each).")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(60, ErrorMessage = "Last name and first name are required (maximum 60 characters each).")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Display(Name = "M.I.")]
        [StringLength(5, ErrorMessage = "Middle initial is too long.")]
        public string MI { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [StringLength(120, ErrorMessage = "Please enter a valid email address.")]
        [RegularExpression(@"^\w+([-+.']\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$", ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please select a program.")]
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
            get
            {
                // The original kept a saved program that is no longer in the list so nothing is lost.
                if (!string.IsNullOrEmpty(Program) && Array.IndexOf(QCUCRAM_Programs.All, Program) < 0)
                {
                    List<string> list = new List<string>(QCUCRAM_Programs.All);
                    list.Add(Program);
                    return list;
                }
                return QCUCRAM_Programs.All;
            }
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
                    ? RestrictionLiftDate.Value.ToString("MMM d, yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture)
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
