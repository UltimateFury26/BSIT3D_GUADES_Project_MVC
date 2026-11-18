using System;
using System.Collections.Generic;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>
    /// One currently restricted requester.
    ///
    /// Restrictions.aspx bound a DataTable and added three computed columns:
    /// StudentHtml, UntilText and RemainingText. The first is markup, so it is
    /// kept as a raw string here and written with Html.Raw in the view - exactly
    /// what Eval("StudentHtml") did.
    /// </summary>
    public class QCUCRAMRestrictedRow
    {
        public int StudentID { get; set; }
        public string FullName { get; set; }
        public string StudentNumber { get; set; }
        public string Program { get; set; }
        public string RequesterType { get; set; }
        public int NoShowCount { get; set; }
        public DateTime? RestrictionLiftDate { get; set; }

        /// <summary>
        /// "&lt;strong&gt;Last, First MI&lt;/strong&gt;&lt;br /&gt;&lt;small&gt;Number | Type | Program&lt;/small&gt;"
        /// (Restrictions.aspx.cs StudentHtml - MI appended untrimmed, no period).
        /// </summary>
        public string StudentHtml { get; set; }

        /// <summary>"Until lifted manually" or "Nov 24, 2026 9:00 AM".</summary>
        public string UntilText { get; set; }

        /// <summary>"2 day(s) 4 hr", "-", or "Ending at the next sweep".</summary>
        public string RemainingText { get; set; }
    }

    /// <summary>One requester who is one strike away from being restricted.</summary>
    public class QCUCRAMStrikeWarningRow
    {
        public int StudentID { get; set; }
        public string FullName { get; set; }
        public string StudentNumber { get; set; }
        public string Program { get; set; }
        public string RequesterType { get; set; }
        public int NoShowCount { get; set; }
        public int StrikesRemaining { get; set; }

        /// <summary>Same StudentHtml shape as the restricted grid.</summary>
        public string StudentHtml { get; set; }

        /// <summary>"2 of 3" - Restrictions.aspx.cs StrikesText.</summary>
        public string StrikesText { get; set; }

        /// <summary>"Nov 24, 2026" of the most recent Forfeited booking, or "-".</summary>
        public string LastText { get; set; }
    }

    public class QCUCRAMRestrictionsViewModel
    {
        public IList<QCUCRAMRestrictedRow> Restricted { get; set; }
        public IList<QCUCRAMStrikeWarningRow> Warnings { get; set; }

        /// <summary>NoShowThreshold setting - the number of forfeits that triggers a restriction.</summary>
        public int Threshold { get; set; }

        /// <summary>RestrictionDays setting - how long an automatic restriction lasts.</summary>
        public int RestrictionDays { get; set; }

        /// <summary>
        /// The litRule sentence, pre-built: "Rule: <strong>3</strong> forfeited
        /// booking(s) restrict a student from booking for <strong>7</strong> day(s).
        /// Restrictions end automatically; you can also lift one early."
        /// </summary>
        public string RuleText { get; set; }

        /// <summary>The print-only "Printed ..." stamp.</summary>
        public string PrintedAtText { get; set; }

        /// <summary>lblMessage - null when there is nothing to show.</summary>
        public string Message { get; set; }

        /// <summary>lblMessage's CssClass: alert-danger when true, alert-success when false.</summary>
        public bool MessageIsError { get; set; }

        // ---- lift panel -------------------------------------------------
        /// <summary>Student whose "Lift" panel is open; 0 when closed.</summary>
        public int LiftId { get; set; }

        /// <summary>True adds " is-open" to the overlay, like LiftModalCss.</summary>
        public bool LiftOpen { get; set; }

        /// <summary>
        /// "&lt;p class=\"mb-0\"&gt;You are about to lift the restriction of
        /// &lt;strong&gt;Name&lt;/strong&gt; (Number), who has N strike(s) and is
        /// restricted until ...&lt;/p&gt;" - written with Html.Raw.
        /// </summary>
        public string LiftInfo { get; set; }

        /// <summary>" is-open" or "".</summary>
        public string LiftModalCss { get { return LiftOpen ? " is-open" : string.Empty; } }

        public QCUCRAMRestrictionsViewModel()
        {
            Restricted = new List<QCUCRAMRestrictedRow>();
            Warnings = new List<QCUCRAMStrikeWarningRow>();
            Threshold = 3;
            RestrictionDays = 7;
        }
    }
}
