using System;
using System.Collections.Generic;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>
    /// Everything the records screen filters on. Kept as one object so the
    /// controller, the service, the view and the CSV export all read the same
    /// values - the Web Forms page kept these in ViewState, which is exactly
    /// what a query string does for an MVC page.
    /// </summary>
    public class QCUCRAMRecordsFilter
    {
        public string Status { get; set; }
        public int? RoomID { get; set; }
        public string Program { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string Search { get; set; }
        public string Sort { get; set; }
        public string Dir { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        public QCUCRAMRecordsFilter()
        {
            Status = string.Empty;
            Program = string.Empty;
            Search = string.Empty;
            Sort = "ReservationDate";
            Dir = "desc";
            Page = 1;
            PageSize = 20;
        }

        public bool HasFilters
        {
            get
            {
                return !string.IsNullOrEmpty(Status) || RoomID.HasValue ||
                       !string.IsNullOrEmpty(Program) || From.HasValue || To.HasValue ||
                       !string.IsNullOrEmpty(Search);
            }
        }

        /// <summary>True when the requested sort key is one this screen understands.</summary>
        public static bool IsSortKey(string key)
        {
            switch (key)
            {
                case "ReservationID":
                case "ReservationDate":
                case "StartTime":
                case "RoomName":
                case "Requester":
                case "Program":
                case "NumberOfMembers":
                case "Status":
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>One row of the records table.</summary>
    public class QCUCRAMRecordsRow
    {
        public int ReservationID { get; set; }
        public DateTime ReservationDate { get; set; }

        /// <summary>
        /// Phase 31: the raw slot boundaries, not just the formatted label.
        ///
        /// Records.aspx.cs sorted on ts.StartTime (a TimeSpan) and wrote the two CSV
        /// columns from the same values. The MVC row only carried the combined
        /// "9:00 AM - 10:00 AM" string, so sorting compared 12-hour text ("10:00 AM"
        /// sorts before "9:00 AM") and the export had to split the label back apart.
        /// Keeping the TimeSpans makes both operations exact.
        /// </summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>End of the slot; the CSV "End Time" column.</summary>
        public TimeSpan EndTime { get; set; }

        /// <summary>Display label built from StartTime/EndTime: "9:00 AM - 10:00 AM".</summary>
        public string SlotLabel { get; set; }
        public string RoomName { get; set; }
        public string Requester { get; set; }

        /// <summary>Phase 16A: sort keys only. Records.aspx.cs sorted Requester on s.LastName then s.FirstName, not on the joined display text.</summary>
        public string SortLastName { get; set; }
        public string SortFirstName { get; set; }
        public string StudentNumber { get; set; }
        public string Program { get; set; }
        public int NumberOfMembers { get; set; }
        public string Purpose { get; set; }
        public string Status { get; set; }
        public string StatusCss { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Null until the group is checked in; the CSV writes it as "Checked In".</summary>
        public DateTime? CheckedInAt { get; set; }
    }

    public class QCUCRAMRecordsViewModel
    {
        public QCUCRAMRecordsFilter Filter { get; set; }
        public IList<QCUCRAMRecordsRow> Rows { get; set; }
        public int TotalRows { get; set; }

        /// <summary>
        /// Phase 16A: lblMessage on Records.aspx. Set when a date filter is invalid; the
        /// grid is then empty and the count line is not shown.
        /// </summary>
        public string FilterError { get; set; }

        /// <summary>Ready-made pager whose UrlFor keeps every current filter.</summary>
        public QCUCRAMPagerViewModel Pager { get; set; }

        /// <summary>
        /// Set only when ?detail=N was supplied. The records table opens the same
        /// slide-over panel the queue uses, so an assistant can read a row without
        /// losing the filter, the sort or the page they were on.
        /// </summary>
        public QCUCRAMReservationDetailViewModel Detail { get; set; }

        public IList<KeyValuePair<string, string>> StatusOptions { get; set; }
        public IList<KeyValuePair<string, string>> RoomOptions { get; set; }
        public IList<string> ProgramOptions { get; set; }

        public QCUCRAMRecordsViewModel()
        {
            Filter = new QCUCRAMRecordsFilter();
            Rows = new List<QCUCRAMRecordsRow>();
            Pager = new QCUCRAMPagerViewModel();
            StatusOptions = new List<KeyValuePair<string, string>>();
            RoomOptions = new List<KeyValuePair<string, string>>();
            ProgramOptions = new List<string>();
        }
    }
}
