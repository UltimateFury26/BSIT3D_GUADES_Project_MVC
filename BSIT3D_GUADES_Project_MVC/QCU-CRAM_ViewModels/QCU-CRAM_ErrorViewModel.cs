using System;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels
{
    /// <summary>
    /// QCU-CRAM  |  QCU-CRAM_ViewModels/QCU-CRAM_ErrorViewModel.cs
    ///
    /// Everything the shared error page needs. Built by QCUCRAMHandleErrorAttribute;
    /// also usable from ErrorController for a direct /error hit.
    /// </summary>
    public class QCUCRAMErrorViewModel
    {
        /// <summary>404 or 500.</summary>
        public int StatusCode { get; set; }

        /// <summary>Short heading, e.g. "Page not found".</summary>
        public string Title { get; set; }

        /// <summary>One sentence the user can act on.</summary>
        public string Message { get; set; }

        /// <summary>The 8-character code that matches a line in the error log.</summary>
        public string Reference { get; set; }

        /// <summary>Exception detail - only shown when ShowDetail is true (local debugging).</summary>
        public string Detail { get; set; }

        /// <summary>True only when QCU-CRAM_ShowErrorDetail is "true" in Web.config.</summary>
        public bool ShowDetail { get; set; }

        /// <summary>Optional page to offer as "Go back".</summary>
        public string ReturnUrl { get; set; }

        public QCUCRAMErrorViewModel()
        {
            StatusCode = 500;
            Title = "Something went wrong";
            Message = "An unexpected error stopped that page from loading. Please try again.";
        }
    }
}
