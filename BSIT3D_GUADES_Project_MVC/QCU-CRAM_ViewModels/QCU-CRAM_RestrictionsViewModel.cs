using System;
using System.Collections.Generic;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>One currently restricted requester.</summary>
    public class QCUCRAMRestrictedRow
    {
        public int StudentID { get; set; }
        public string FullName { get; set; }
        public string StudentNumber { get; set; }
        public string Program { get; set; }
        public string RequesterType { get; set; }
        public int NoShowCount { get; set; }
        public DateTime? RestrictionLiftDate { get; set; }

        /// <summary>"Until lifted manually" or "Nov 24, 2026 9:00 AM".</summary>
        public string UntilText { get; set; }

        /// <summary>"2 day(s) 4 hr" or "-" when there is no automatic end date.</summary>
        public string RemainingText { get; set; }
    }

    /// <summary>One requester who is one strike away from being restricted.</summary>
    public class QCUCRAMStrikeWarningRow
    {
        public int StudentID { get; set; }
        public string FullName { get; set; }
        public string StudentNumber { get; set; }
        public string Program { get; set; }
        public int NoShowCount { get; set; }
        public int StrikesRemaining { get; set; }
    }

    public class QCUCRAMRestrictionsViewModel
    {
        public IList<QCUCRAMRestrictedRow> Restricted { get; set; }
        public IList<QCUCRAMStrikeWarningRow> Warnings { get; set; }

        /// <summary>NoShowThreshold setting - the number of forfeits that triggers a restriction.</summary>
        public int Threshold { get; set; }

        /// <summary>RestrictionDays setting - how long an automatic restriction lasts.</summary>
        public int RestrictionDays { get; set; }

        public QCUCRAMRestrictionsViewModel()
        {
            Restricted = new List<QCUCRAMRestrictedRow>();
            Warnings = new List<QCUCRAMStrikeWarningRow>();
            Threshold = 3;
            RestrictionDays = 7;
        }
    }
}
