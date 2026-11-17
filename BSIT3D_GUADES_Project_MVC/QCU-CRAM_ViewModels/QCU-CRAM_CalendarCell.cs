using System;
using System.Collections.Generic;
using System.Globalization;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>
    /// Phase 06 - the month calendar on the student dashboard.
    ///
    /// Replaces the &lt;asp:Calendar&gt; on Guades_QCUCram_Dashboard.aspx. The Web Forms page
    /// kept two dictionaries, _dayRank and _dayTips, filled in Page_Load and read back in
    /// DayRender; here the service pre-computes one QCUCRAMCalendarDay per visible day and
    /// the partial just paints them.
    ///
    /// Rank, exactly as the original defined it:
    ///   3 = a CheckedIn booking whose slot is running right now (ongoing)
    ///   2 = a Pending / Approved / Reassigned booking (reserved)
    ///   1 = a Completed booking (done)
    ///   0 = nothing
    /// </summary>
    public class QCUCRAMCalendarViewModel
    {
        public QCUCRAMCalendarViewModel()
        {
            Days = new List<QCUCRAMCalendarDay>();
        }

        /// <summary>The first day of the month being shown.</summary>
        public DateTime Month { get; set; }

        /// <summary>Every visible cell, including the leading/trailing blanks, in rows of seven.</summary>
        public List<QCUCRAMCalendarDay> Days { get; set; }

        public string MonthLabel
        {
            get { return Month.ToString("MMMM yyyy", CultureInfo.InvariantCulture); }
        }

        public DateTime PreviousMonth { get { return Month.AddMonths(-1); } }
        public DateTime NextMonth { get { return Month.AddMonths(1); } }

        /// <summary>How many rows the table needs (4-6).</summary>
        public int RowCount
        {
            get { return Days.Count / 7; }
        }
    }

    /// <summary>One day cell of the dashboard calendar.</summary>
    public class QCUCRAMCalendarDay
    {
        /// <summary>Midnight of the day; DateTime.MinValue for a leading/trailing blank.</summary>
        public DateTime Date { get; set; }

        /// <summary>False for the blanks that pad the first and last week.</summary>
        public bool InMonth { get; set; }

        public bool IsToday { get; set; }

        /// <summary>0 = nothing, 1 = completed, 2 = reserved, 3 = ongoing.</summary>
        public int Rank { get; set; }

        /// <summary>The tooltip the original put on the cell ("2 bookings", "Ongoing booking", ...).</summary>
        public string Tip { get; set; }

        /// <summary>How many bookings fall on this day (used for the tooltip and the count).</summary>
        public int BookingCount { get; set; }

        public bool HasBookings { get { return Rank > 0; } }

        /// <summary>Bootstrap background class for the rank (matches .qcu-cal in QCU-CRAM_Site.css).</summary>
        public string CssClass
        {
            get
            {
                // The class names follow the original legend, and
                // Content/QCU-CRAM_Site.css repaints them:
                //   bg-success  -> blue   (Reserved: pending / approved / reassigned)
                //   bg-primary  -> orange (Ongoing: checked in, slot running now)
                //   bg-secondary-> grey   (Done: completed)
                switch (Rank)
                {
                    case 3: return "bg-primary";     // ongoing
                    case 2: return "bg-success";     // reserved
                    case 1: return "bg-secondary";   // done
                    default: return string.Empty;
                }
            }
        }

        public string DayNumber
        {
            get { return InMonth ? Date.Day.ToString(CultureInfo.InvariantCulture) : string.Empty; }
        }
    }
}
