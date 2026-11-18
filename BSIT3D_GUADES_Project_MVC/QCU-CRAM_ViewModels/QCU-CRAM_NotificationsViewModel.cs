using System.Collections.Generic;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>
    /// Phase 06 - the model behind Views/Student/QCU-CRAM_Notifications.cshtml.
    ///
    /// Port of Guades_QCUCram_Student/Guades_QCUCram_Notifications.aspx: the All / Unread
    /// filter, "Mark all as read", the fifteen-row GridView with the unread rows in bold
    /// and the "You have N unread notification(s) out of M total." summary.
    /// </summary>
    public class QCUCRAMNotificationsViewModel
    {
        public QCUCRAMNotificationsViewModel()
        {
            Items = new List<QCUCRAM_NotificationItem>();
        }

        public List<QCUCRAM_NotificationItem> Items { get; set; }

        /// <summary>True when the Unread filter is on.</summary>
        public bool UnreadOnly { get; set; }

        public int UnreadCount { get; set; }
        public int TotalCount { get; set; }

        /// <summary>Rows before paging.</summary>
        public int TotalItems { get; set; }

        public QCUCRAMPagerViewModel Pager { get; set; }

        public bool IsEmpty
        {
            get { return Items.Count == 0; }
        }

        /// <summary>"You have <b>3</b> unread notification(s) out of 12 total." (litSummary)</summary>
        public string SummaryLine
        {
            get
            {
                return "You have <strong>" + UnreadCount + "</strong> unread notification(s) out of " +
                       TotalCount + " total.";
            }
        }
    }
}
