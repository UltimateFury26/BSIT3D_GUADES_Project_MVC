using System;
using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Services;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.Controllers
{
    /// <summary>
    /// Phase 07 - the admin core, in one controller.
    ///
    /// Replaces these four Web Forms pages:
    ///
    ///   Guades_QCUCram_Admin/Guades_QCUCram_Dashboard.aspx    -> Dashboard
    ///   Guades_QCUCram_Admin/Guades_QCUCram_Queue.aspx        -> Queue, Approve, Decline, Reassign, DoReassign
    ///   Guades_QCUCram_Admin/Guades_QCUCram_CheckIn.aspx      -> CheckIn, DoCheckIn
    ///   Guades_QCUCram_Admin/Guades_QCUCram_Restrictions.aspx -> Restrictions, Lift
    ///
    /// URL shape. The class sits in the root Controllers namespace and derives
    /// QCUCRAM_AdminController, which already carries [QCUCRAM_RequireAdmin] and
    /// [QCUCRAM_RequireLogin]. Attribute routing keeps the URLs flat (/admin/queue,
    /// /admin/check-in) while the conventional "admin" route still serves /admin.
    ///
    /// Every write follows Post/Redirect/Get: the POST validates, calls the service,
    /// drops a Flash or FlashError into TempData and redirects back to the board it
    /// came from, so a browser refresh never re-submits a decision.
    /// </summary>
    [RoutePrefix("admin")]
    public class AdminController : QCUCRAM_AdminController
    {
        private readonly QCUCRAMAdminService _service;

        public AdminController()
        {
            _service = new QCUCRAMAdminService();
        }

        public AdminController(QCUCRAMAdminService service)
        {
            _service = service ?? new QCUCRAMAdminService();
        }

        // ==================================================================
        //  Dashboard
        // ==================================================================

        [HttpGet]
        [Route("~/admin")]
        [Route("dashboard")]
        public ActionResult Dashboard()
        {
            QCUCRAMAdminDashboardViewModel model;

            try
            {
                model = _service.Dashboard();
            }
            catch (Exception)
            {
                // Dashboard.aspx.cs: the page still opened and showed this toast.
                model = new QCUCRAMAdminDashboardViewModel();
                FlashError("Sorry, the dashboard could not be fully loaded. Please refresh the page.");
            }

            // Dashboard.aspx.cs lblWelcome: "Welcome, " + CurrentUserName + "."
            model.WelcomeName = "Welcome, " + CurrentUserName + ".";

            return View("QCU-CRAM_Dashboard", model);
        }

        // ==================================================================
        //  Approval queue
        // ==================================================================

        [HttpGet]
        [Route("queue")]
        public ActionResult Queue(int? review, int? reassign)
        {
            int reviewId = review.HasValue && review.Value > 0 ? review.Value : 0;
            int reassignId = reassign.HasValue && reassign.Value > 0 ? reassign.Value : 0;

            QCUCRAMQueueViewModel model = _service.Queue(reviewId, reassignId);

            // A stale link (deleted reservation) should still land on a working board.
            if (reviewId > 0 && model.Detail == null)
            {
                FlashError("Reservation #" + reviewId + " was not found.");
                return RedirectToAction("Queue");
            }

            // ShowDetails(): only a Pending request may open the review panel.
            if (reviewId > 0 && model.Detail != null && model.Detail.Status != QCUCRAM_Status.Pending)
            {
                FlashError("Reservation #" + reviewId + " is no longer pending. The list has been refreshed.");
                return RedirectToAction("Queue");
            }

            if (reassignId > 0 && model.Reassign != null && !string.IsNullOrEmpty(model.Reassign.Error))
            {
                FlashError(model.Reassign.Error);
                return RedirectToAction("Queue");
            }

            if (reassignId > 0 && model.Reassign == null)
            {
                FlashError("Reservation #" + reassignId + " was not found.");
                return RedirectToAction("Queue");
            }

            return View("QCU-CRAM_Queue", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("approve")]
        public ActionResult Approve(int id)
        {
            QCUCRAMOperationResult result = _service.Approve(id, CurrentUserId);
            Report(result);
            return RedirectToAction("Queue");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("decline")]
        public ActionResult Decline(int id, string reason)
        {
            QCUCRAMOperationResult result = _service.Decline(id, reason, CurrentUserId);
            Report(result);

            // btnDecline_Click kept the panel open when the reason was rejected (empty,
            // too long) or the write failed unexpectedly, and closed it when the change
            // itself was refused. The service marks the first group with Retry.
            if (!result.Ok && result.Retry)
            {
                return RedirectToAction("Queue", new { review = id });
            }

            return RedirectToAction("Queue");
        }

        /// <summary>
        /// The queue page opens the reassign panel from ?reassign=N. This GET exists
        /// so a plain link or a bookmark still works instead of a 404.
        /// </summary>
        [HttpGet]
        [Route("reassign/{id:int}")]
        public ActionResult Reassign(int id)
        {
            return RedirectToAction("Queue", new { reassign = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("reassign")]
        public ActionResult DoReassign(int id, string target, string reason)
        {
            QCUCRAMOperationResult result = _service.DoReassign(id, target, reason, CurrentUserId);
            Report(result);

            // .Retry means the slot was taken between the read and the write, or the
            // reason was too short: reopen the panel on the same request.
            if (!result.Ok && result.Retry)
            {
                return RedirectToAction("Queue", new { reassign = id });
            }

            return RedirectToAction("Queue");
        }

        // ==================================================================
        //  Check-in desk
        // ==================================================================

        [HttpGet]
        [Route("check-in")]
        public ActionResult CheckIn(string q)
        {
            return View("QCU-CRAM_CheckIn", _service.CheckInBoard(q));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("check-in")]
        public ActionResult DoCheckIn(int id, string q)
        {
            QCUCRAMOperationResult result = _service.CheckIn(id, CurrentUserId);
            Report(result);
            return RedirectToAction("CheckIn", new { q = q });
        }

        // ==================================================================
        //  Restrictions
        // ==================================================================

        /// <summary>
        /// lift = the student whose "Lift restriction" panel should be open. The Web
        /// Forms page held that in ViewState; a query string is the MVC equivalent
        /// and it keeps the panel open across the failed-lift round trip.
        /// </summary>
        [HttpGet]
        [Route("restrictions")]
        public ActionResult Restrictions(int? lift)
        {
            return View("QCU-CRAM_Restrictions", _service.Restrictions(lift ?? 0));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("restrictions/lift")]
        public ActionResult Lift(int id, string note)
        {
            QCUCRAMOperationResult result = _service.LiftRestriction(id, note, CurrentUserId);
            Report(result);

            // btnConfirmLift_Click kept the panel open when the write threw.
            if (result != null && !result.Ok && result.Retry)
            {
                return RedirectToAction("Restrictions", new { lift = id });
            }

            return RedirectToAction("Restrictions");
        }

        // ==================================================================
        //  Helpers
        // ==================================================================

        /// <summary>Turns a service result into the toast the layout already renders.</summary>
        private void Report(QCUCRAMOperationResult result)
        {
            if (result == null)
            {
                FlashError("Something went wrong. Please try again.");
                return;
            }

            if (result.Ok)
            {
                Flash(result.Message);
            }
            else
            {
                FlashError(result.Message);
            }
        }
    }
}
