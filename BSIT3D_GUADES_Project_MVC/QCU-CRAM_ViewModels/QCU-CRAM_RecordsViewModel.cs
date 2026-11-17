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
            PageSize = 25;
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
        public string SlotLabel { get; set; }
        public string RoomName { get; set; }
        public string Requester { get; set; }
        public string StudentNumber { get; set; }
        public string Program { get; set; }
        public int NumberOfMembers { get; set; }
        public string Purpose { get; set; }
        public string Status { get; set; }
        public string StatusCss { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class QCUCRAMRecordsViewModel
    {
        public QCUCRAMRecordsFilter Filter { get; set; }
        public IList<QCUCRAMRecordsRow> Rows { get; set; }
        public int TotalRows { get; set; }

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
