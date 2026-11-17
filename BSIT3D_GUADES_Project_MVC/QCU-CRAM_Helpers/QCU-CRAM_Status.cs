namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers
{
    /// <summary>
    /// The eight reservation statuses, in one place. Every status string in the system
    /// comes from here so a typo can never create a ninth status.
    /// </summary>
    public static class QCUCRAM_Status
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Reassigned = "Reassigned";
        public const string Declined = "Declined";
        public const string Cancelled = "Cancelled";
        public const string CheckedIn = "CheckedIn";
        public const string Completed = "Completed";
        public const string Forfeited = "Forfeited";

        /// <summary>All eight, in lifecycle order.</summary>
        public static readonly string[] All =
        {
            Pending, Approved, Reassigned, Declined, Cancelled, CheckedIn, Completed, Forfeited
        };

        /// <summary>
        /// "Active" = the slot is held. Everything except Declined, Cancelled and Forfeited.
        /// This is the definition the unique index UX_Res_ActiveSlot uses.
        /// </summary>
        public static bool IsActive(string status)
        {
            return status != Declined && status != Cancelled && status != Forfeited;
        }

        /// <summary>"Occupied" = confirmed bookings, shown as Taken on the availability grid.</summary>
        public static bool IsOccupied(string status)
        {
            return status == Approved || status == Reassigned
                || status == CheckedIn || status == Completed;
        }

        /// <summary>Terminal statuses never change again.</summary>
        public static bool IsTerminal(string status)
        {
            return status == Declined || status == Cancelled
                || status == Forfeited || status == Completed;
        }

        /// <summary>
        /// Phase 07 - the modifier class the admin tables put next to .qcu-badge.
        ///
        /// Kept here rather than in each view so the dashboard, the queue, the check-in
        /// desk and the records table can never drift apart: add a ninth status and
        /// this switch is the only place that needs a new line. The values match the
        /// .qcu-badge.is-* rules appended to QCU-CRAM_Site.css in this phase.
        /// </summary>
        public static string CssClass(string status)
        {
            switch (status)
            {
                case Pending: return "is-pending";
                case Approved: return "is-approved";
                case Reassigned: return "is-reassigned";
                case CheckedIn: return "is-checkedin";
                case Completed: return "is-completed";
                case Declined: return "is-declined";
                case Cancelled: return "is-cancelled";
                case Forfeited: return "is-forfeited";
                default: return string.Empty;
            }
        }

        /// <summary>SQL fragment for "the slot is still held" - fixed constants, never user input.</summary>
        public const string FreeListSql = "('Declined','Cancelled','Forfeited')";

        /// <summary>SQL fragment for confirmed bookings - fixed constants, never user input.</summary>
        public const string OccupiedListSql = "('Approved','Reassigned','CheckedIn','Completed')";

        /// <summary>Occupied plus Forfeited - the denominator the no-show rate uses.</summary>
        public const string OccupiedOrForfeitedListSql =
            "('Approved','Reassigned','CheckedIn','Completed','Forfeited')";
    }
}
