using System;
using System.Collections.Generic;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    // =====================================================================
    //  Rooms
    // =====================================================================
    public class QCUCRAMRoomRow
    {
        public int RoomID { get; set; }
        public string RoomName { get; set; }
        public int MinCapacity { get; set; }
        public int MaxCapacity { get; set; }
        public bool IsActive { get; set; }
        public int ReservationCount { get; set; }

        /// <summary>A room that has ever been booked may only be disabled, never deleted.</summary>
        public bool CanDelete { get { return ReservationCount == 0; } }
    }

    public class QCUCRAMRoomEditModel
    {
        public int RoomID { get; set; }
        public string RoomName { get; set; }
        public int MinCapacity { get; set; }
        public int MaxCapacity { get; set; }
        public bool IsActive { get; set; }

        public QCUCRAMRoomEditModel()
        {
            MinCapacity = 5;
            MaxCapacity = 10;
            IsActive = true;
        }
    }

    public class QCUCRAMRoomsViewModel
    {
        public List<QCUCRAMRoomRow> Rows { get; set; }
        public QCUCRAMRoomEditModel Edit { get; set; }
        public bool ModalOpen { get; set; }
        public string ModalError { get; set; }

        public QCUCRAMRoomsViewModel()
        {
            Rows = new List<QCUCRAMRoomRow>();
        }
    }

    // =====================================================================
    //  Time slots
    // =====================================================================
    public class QCUCRAMSetupSlotRow
    {
        public int TimeSlotID { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Label { get; set; }
        public bool IsActive { get; set; }
        public int ReservationCount { get; set; }

        /// <summary>Once a slot has been reserved its times are frozen and it cannot be deleted.</summary>
        public bool CanDelete { get { return ReservationCount == 0; } }
        public bool CanEditTimes { get { return ReservationCount == 0; } }
    }

    public class QCUCRAMSlotEditModel
    {
        public int TimeSlotID { get; set; }
        public string Start { get; set; }
        public string End { get; set; }
        public bool IsActive { get; set; }

        public QCUCRAMSlotEditModel()
        {
            Start = string.Empty;
            End = string.Empty;
            IsActive = true;
        }
    }

    public class QCUCRAMTimeSlotsViewModel
    {
        public List<QCUCRAMSetupSlotRow> Rows { get; set; }
        public QCUCRAMSlotEditModel Edit { get; set; }
        public bool ModalOpen { get; set; }
        public string ModalError { get; set; }

        public QCUCRAMTimeSlotsViewModel()
        {
            Rows = new List<QCUCRAMSetupSlotRow>();
        }
    }

    // =====================================================================
    //  Student accounts
    // =====================================================================
    public class QCUCRAMStudentRow
    {
        public int StudentID { get; set; }
        public string StudentNumber { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string MI { get; set; }
        public string Email { get; set; }
        public string Program { get; set; }
        public string RequesterType { get; set; }
        public int NoShowCount { get; set; }
        public bool IsRestricted { get; set; }
        public DateTime? RestrictionLiftDate { get; set; }
        public int ReservationCount { get; set; }

        public string FullName
        {
            get
            {
                string name = (LastName ?? "") + ", " + (FirstName ?? "");
                if (!string.IsNullOrEmpty(MI)) { name += " " + MI + "."; }
                return name;
            }
        }

        /// <summary>Source FormatRestricted(): No / Yes / Yes (until MMM d, yyyy h:mm tt).</summary>
        public string RestrictionText
        {
            get
            {
                if (!IsRestricted) { return "No"; }
                if (!RestrictionLiftDate.HasValue) { return "Yes"; }
                return "Yes (until " +
                       RestrictionLiftDate.Value.ToString("MMM d, yyyy h:mm tt",
                           System.Globalization.CultureInfo.InvariantCulture) + ")";
            }
        }

        public bool CanDelete { get { return ReservationCount == 0; } }
    }

    public class QCUCRAMStudentEditModel
    {
        public int StudentID { get; set; }
        public string StudentNumber { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string MI { get; set; }
        public string Email { get; set; }
        public string Program { get; set; }
        public string RequesterType { get; set; }

        public QCUCRAMStudentEditModel()
        {
            RequesterType = "Student";
        }
    }

    public class QCUCRAMStudentsViewModel
    {
        public List<QCUCRAMStudentRow> Rows { get; set; }
        public QCUCRAMStudentEditModel Edit { get; set; }
        public bool ModalOpen { get; set; }
        public string ModalError { get; set; }

        public string Search { get; set; }
        public string Program { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalRows { get; set; }

        public int TotalPages
        {
            get
            {
                if (PageSize < 1) { return 1; }
                int pages = (TotalRows + PageSize - 1) / PageSize;
                return pages < 1 ? 1 : pages;
            }
        }

        public List<string> ProgramOptions { get; set; }
        public List<string> RequesterTypes { get; set; }

        public QCUCRAMStudentsViewModel()
        {
            Rows = new List<QCUCRAMStudentRow>();
            ProgramOptions = new List<string>();
            RequesterTypes = new List<string>();
            Page = 1;
            PageSize = 20;
        }
    }

    // =====================================================================
    //  Administrator accounts
    // =====================================================================
    public class QCUCRAMAdminRow
    {
        public int AdminID { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public bool IsActive { get; set; }
        public bool IsSelf { get; set; }
    }

    public class QCUCRAMAdminEditModel
    {
        public int AdminID { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
    }

    public class QCUCRAMAdminUsersViewModel
    {
        public List<QCUCRAMAdminRow> Rows { get; set; }
        public QCUCRAMAdminEditModel Edit { get; set; }
        public bool ModalOpen { get; set; }
        public string ModalError { get; set; }
        public int ActiveCount { get; set; }

        /// <summary>
        /// The password the next added account will receive, e.g. "admin6" when five
        /// accounts already exist. AdminUsers.aspx.cs called this TempPasswordPreview
        /// and showed it inside the Add pop-up.
        /// </summary>
        public string NextTemporaryPassword { get; set; }

        public QCUCRAMAdminUsersViewModel()
        {
            Rows = new List<QCUCRAMAdminRow>();
        }
    }

    // =====================================================================
    //  Settings
    // =====================================================================
    public class QCUCRAMSettingRow
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string Description { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }

        /// <summary>Source RangeText(): "Whole number from X to Y."</summary>
        public string RangeText { get { return "Whole number from " + Min + " to " + Max + "."; } }
    }

    public class QCUCRAMSettingsViewModel
    {
        public List<QCUCRAMSettingRow> Rows { get; set; }
        public string Error { get; set; }

        public QCUCRAMSettingsViewModel()
        {
            Rows = new List<QCUCRAMSettingRow>();
        }
    }

    // =====================================================================
    //  Developer tools
    // =====================================================================
    public class QCUCRAMDevToolsViewModel
    {
        public DateTime RealNow { get; set; }
        public DateTime SimulatedNow { get; set; }
        public int OffsetMinutes { get; set; }

        public int Students { get; set; }
        public int SampleStudents { get; set; }
        public int SampleRestricted { get; set; }
        public int SampleOneStrikeAway { get; set; }
        public int Reservations { get; set; }
        public int SampleReservations { get; set; }
        public int Notifications { get; set; }

        public string LastResult { get; set; }
        public string LastSweepSummary { get; set; }
    }

    // =====================================================================
    //  Shared modal shell
    // ---------------------------------------------------------------------
    //  _QCU-CRAM_SetupModal.cshtml renders the pop-up frame once so the six
    //  maintenance screens do not each repeat the same Bootstrap markup.
    //  Body is a Razor helper: the caller passes its own fields as @<text>...
    //  and the lambda still closes over the calling page's strongly typed Model.
    // =====================================================================
    public class QCUCRAM_SetupModalViewModel
    {
        public bool Open { get; set; }
        public string Title { get; set; }
        public string Action { get; set; }
        public string Error { get; set; }
        public string SubmitText { get; set; }
        public string CancelUrl { get; set; }

        /// <summary>The form fields, supplied by the calling view as an @&lt;text&gt; block.</summary>
        public Func<object, System.Web.WebPages.HelperResult> Body { get; set; }

        public QCUCRAM_SetupModalViewModel()
        {
            SubmitText = "Save";
        }
    }
}
