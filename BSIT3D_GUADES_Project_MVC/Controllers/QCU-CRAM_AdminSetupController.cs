using System;
using System.Collections.Generic;
using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Services;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_ViewModels;

namespace BSIT3D_GUADES_Project_MVC.Controllers
{
    /// <summary>
    /// Phase 08 - the maintenance screens.
    ///
    /// Replaces these six Web Forms pages:
    ///
    ///   Guades_QCUCram_Admin/Guades_QCUCram_Rooms.aspx      -> Rooms, SaveRoom, ToggleRoom, DeleteRoom
    ///   Guades_QCUCram_Admin/Guades_QCUCram_TimeSlots.aspx  -> TimeSlots, SaveSlot, ToggleSlot, DeleteSlot
    ///   Guades_QCUCram_Admin/Guades_QCUCram_Students.aspx   -> Students, SaveStudent, ResetStudentPassword, DeleteStudent
    ///   Guades_QCUCram_Admin/Guades_QCUCram_AdminUsers.aspx -> AdminUsers, SaveAdmin, ToggleAdmin, ResetAdminPassword
    ///   Guades_QCUCram_Admin/Guades_QCUCram_Settings.aspx   -> Settings (GET/POST)
    ///   Guades_QCUCram_Admin/Guades_QCUCram_DevTools.aspx   -> DevTools, ShiftClock, RunSweep, ...
    ///
    /// The action names are fixed: Phase 05's navigation already links to
    /// AdminSetup.Rooms, .TimeSlots, .Students, .AdminUsers, .Settings and .DevTools.
    ///
    /// Every write follows Post/Redirect/Get. A validation failure that must reopen
    /// the modal is parked in TempData for exactly one redirect, then copied into the
    /// view model by the matching GET action - the MVC equivalent of the original's
    /// "_modalOpen = true; ModalError(...)" pattern.
    /// </summary>
    public class AdminSetupController : QCUCRAM_AdminController
    {
        private const string ModalErrorKey = "QCUCRAM_ModalError";
        private const string ModalEditKey = "QCUCRAM_ModalEdit";
        private const string SettingsValuesKey = "QCUCRAM_SettingsValues";
        private const string DevResultKey = "QCUCRAM_DevResult";
        private const string DevSweepKey = "QCUCRAM_DevSweep";

        // =================================================================
        //  Rooms
        // =================================================================
        [HttpGet]
        public ActionResult Rooms(int? edit)
        {
            QCUCRAMRoomsViewModel vm = QCUCRAMSetupService.Rooms(edit);
            if (edit.HasValue && edit.Value > 0 && vm.Edit == null) { FlashError("That room no longer exists."); }
            PullModalError(vm);
            return View("~/Views/Admin/QCU-CRAM_Rooms.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveRoom(QCUCRAMRoomEditModel model)
        {
            QCUCRAMOperationResult result = QCUCRAMSetupService.SaveRoom(model);

            if (!result.Ok && result.Retry)
            {
                TempData[ModalErrorKey] = result.Message;
                TempData[ModalEditKey] = model;
                return RedirectToAction("Rooms",
                    new { edit = model != null && model.RoomID > 0 ? model.RoomID : (int?)null });
            }

            Toast(result);
            return RedirectToAction("Rooms");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleRoom(int id)
        {
            // Rooms.aspx.cs gvRooms_RowCommand: any failure -> the same generic message
            try { Toast(QCUCRAMSetupService.ToggleRoom(id)); }
            catch (System.Exception) { FlashError("Sorry, that action could not be completed. Please try again."); }
            return RedirectToAction("Rooms");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteRoom(int id)
        {
            try { Toast(QCUCRAMSetupService.DeleteRoom(id)); }
            catch (System.Exception) { FlashError("Sorry, that action could not be completed. Please try again."); }
            return RedirectToAction("Rooms");
        }

        // =================================================================
        //  Time slots
        // =================================================================
        [HttpGet]
        public ActionResult TimeSlots(int? edit)
        {
            QCUCRAMTimeSlotsViewModel vm = QCUCRAMSetupService.TimeSlots(edit);
            if (edit.HasValue && edit.Value > 0 && vm.Edit == null) { FlashError("That time slot no longer exists."); }
            PullModalError(vm);
            return View("~/Views/Admin/QCU-CRAM_TimeSlots.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveSlot(QCUCRAMSlotEditModel model)
        {
            QCUCRAMOperationResult result = QCUCRAMSetupService.SaveSlot(model);

            if (!result.Ok && result.Retry)
            {
                TempData[ModalErrorKey] = result.Message;
                TempData[ModalEditKey] = model;
                return RedirectToAction("TimeSlots",
                    new { edit = model != null && model.TimeSlotID > 0 ? model.TimeSlotID : (int?)null });
            }

            Toast(result);
            return RedirectToAction("TimeSlots");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleSlot(int id)
        {
            // TimeSlots.aspx.cs gvSlots_RowCommand: any failure -> the same generic message
            try { Toast(QCUCRAMSetupService.ToggleSlot(id)); }
            catch (System.Exception) { FlashError("Sorry, that action could not be completed. Please try again."); }
            return RedirectToAction("TimeSlots");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteSlot(int id)
        {
            try { Toast(QCUCRAMSetupService.DeleteSlot(id)); }
            catch (System.Exception) { FlashError("Sorry, that action could not be completed. Please try again."); }
            return RedirectToAction("TimeSlots");
        }

        // =================================================================
        //  Student accounts
        // =================================================================
        [HttpGet]
        public ActionResult Students(string q, string program, int? page, int? edit)
        {
            QCUCRAMStudentsViewModel vm =
                QCUCRAMSetupService.Students(q, program, page ?? 1, 10);   // GridView PageSize="10"

            // edit absent -> plain list; edit = 0 -> "Add student"; edit > 0 -> "Edit student"
            if (edit.HasValue)
            {
                if (edit.Value > 0)
                {
                    vm.Edit = QCUCRAMSetupService.LoadStudent(edit.Value);
                    vm.ModalOpen = vm.Edit != null;
                }
                else
                {
                    vm.Edit = new QCUCRAMStudentEditModel();
                    vm.ModalOpen = true;
                }
            }

            PullModalError(vm);
            return View("~/Views/Admin/QCU-CRAM_Students.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveStudent(QCUCRAMStudentEditModel model)
        {
            QCUCRAMOperationResult result = QCUCRAMSetupService.SaveStudent(model);

            if (!result.Ok && result.Retry)
            {
                TempData[ModalErrorKey] = result.Message;
                TempData[ModalEditKey] = model;
                return RedirectToAction("Students",
                    StudentListRoute(model != null && model.StudentID > 0 ? model.StudentID : (int?)null));
            }

            Toast(result);
            return RedirectToAction("Students", StudentListRoute(null));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetStudentPassword(int id)
        {
            // gvStudents_RowCommand wrapped every row action in one try/catch
            try { Toast(QCUCRAMSetupService.ResetStudentPassword(id)); }
            catch (System.Exception) { FlashError("Sorry, that action could not be completed. Please try again."); }
            return RedirectToAction("Students", StudentListRoute(null));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteStudent(int id)
        {
            try { Toast(QCUCRAMSetupService.DeleteStudent(id)); }
            catch (System.Exception) { FlashError("Sorry, that action could not be completed. Please try again."); }
            return RedirectToAction("Students", StudentListRoute(null));
        }

        // R04: the Web Forms grid kept its search text and page number in the UpdatePanel across
        // Save / Reset / Delete. The Students forms post them back as hidden "q" and "page" fields,
        // and the redirect puts them in the URL again (the service clamps a page that no longer exists).
        private System.Web.Routing.RouteValueDictionary StudentListRoute(int? edit)
        {
            System.Web.Routing.RouteValueDictionary route = new System.Web.Routing.RouteValueDictionary();

            string q = (Request.Form["q"] ?? string.Empty).Trim();
            if (q.Length > 0) { route["q"] = q; }

            int page;
            if (int.TryParse(Request.Form["page"], out page) && page > 1) { route["page"] = page; }

            if (edit.HasValue) { route["edit"] = edit.Value; }
            return route;
        }

        // =================================================================
        //  Administrator accounts
        // =================================================================
        [HttpGet]
        public ActionResult AdminUsers(int? edit)
        {
            QCUCRAMAdminUsersViewModel vm = QCUCRAMSetupService.AdminUsers();

            // edit absent -> plain list; edit = 0 -> "Add admin"; edit > 0 -> "Edit admin"
            if (edit.HasValue)
            {
                if (edit.Value > 0)
                {
                    foreach (QCUCRAMAdminRow row in vm.Rows)
                    {
                        if (row.AdminID != edit.Value) { continue; }

                        vm.Edit = new QCUCRAMAdminEditModel
                        {
                            AdminID = row.AdminID,
                            Username = row.Username,
                            FullName = row.FullName
                        };
                        vm.ModalOpen = true;
                        break;
                    }

                    // R05: AdminUsers.aspx.cs LoadForEdit -> "That account no longer exists."
                    if (vm.Edit == null) { FlashError("That account no longer exists."); }
                }
                else
                {
                    vm.Edit = new QCUCRAMAdminEditModel();
                    vm.ModalOpen = true;
                }
            }

            PullModalError(vm);
            return View("~/Views/Admin/QCU-CRAM_AdminUsers.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveAdmin(QCUCRAMAdminEditModel model)
        {
            QCUCRAMOperationResult result = QCUCRAMSetupService.SaveAdmin(model);

            if (!result.Ok && result.Retry)
            {
                TempData[ModalErrorKey] = result.Message;
                TempData[ModalEditKey] = model;
                return RedirectToAction("AdminUsers",
                    new { edit = model != null && model.AdminID > 0 ? model.AdminID : (int?)null });
            }

            Toast(result);
            return RedirectToAction("AdminUsers");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleAdmin(int id)
        {
            // gvAdmins_RowCommand wrapped every row action in one try/catch
            try { Toast(QCUCRAMSetupService.ToggleAdmin(id)); }
            catch (System.Exception) { FlashError("Sorry, that action could not be completed. Please try again."); }
            return RedirectToAction("AdminUsers");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetAdminPassword(int id)
        {
            try { Toast(QCUCRAMSetupService.ResetAdminPassword(id)); }
            catch (System.Exception) { FlashError("Sorry, that action could not be completed. Please try again."); }
            return RedirectToAction("AdminUsers");
        }

        // =================================================================
        //  Settings
        // =================================================================
        [HttpGet]
        public ActionResult Settings()
        {
            QCUCRAMSettingsViewModel vm = QCUCRAMSetupService.Settings();

            // A failed save keeps what was typed (the Web Forms grid kept it in ViewState);
            // the error itself arrives as a toast through FlashError.
            Dictionary<string, string> typed = TempData[SettingsValuesKey] as Dictionary<string, string>;
            if (typed != null)
            {
                foreach (QCUCRAMSettingRow row in vm.Rows)
                {
                    string value;
                    if (typed.TryGetValue(row.Key, out value)) { row.Value = value; }
                }
            }

            return View("~/Views/Admin/QCU-CRAM_Settings.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Settings(FormCollection form)
        {
            Dictionary<string, string> values = new Dictionary<string, string>();

            foreach (string key in form.AllKeys)
            {
                if (key == null || !key.StartsWith("s_")) { continue; }
                values[key.Substring(2)] = form[key];
            }

            QCUCRAMOperationResult result = QCUCRAMSetupService.SaveSettings(values);

            if (!result.Ok)
            {
                FlashError(result.Message);
                if (result.Retry) { TempData[SettingsValuesKey] = values; }
                return RedirectToAction("Settings");
            }

            // "Settings saved (N changed)." and "No changes to save." are both green toasts.
            Flash(result.Message);
            return RedirectToAction("Settings");
        }

        // =================================================================
        //  Developer tools
        // =================================================================
        [HttpGet]
        public ActionResult DevTools()
        {
            if (!QCUCRAMDevToolsService.Enabled) { return HttpNotFound(); }

            QCUCRAMDevToolsViewModel vm;
            try
            {
                vm = QCUCRAMDevToolsService.Load();
            }
            catch (Exception ex)
            {
                // Do not let an initial Dev Tools read crash the whole MVC page.
                // The exception detail is rendered as encoded text; the full stack is logged.
                string reference = QCUCRAMErrorLog.Write(ex, "AdminSetup.DevTools.InitialLoad", null);
                vm = new QCUCRAMDevToolsViewModel
                {
                    RealNow = DateTime.Now,
                    SimulatedNow = DateTime.Now,
                    OffsetMinutes = 0,
                    LoadError = "Dev Tools could not load its database information. Reference: " + reference
                };
            }

            object last = TempData[DevResultKey];
            if (last != null) { vm.LastResult = last.ToString(); }

            object sweep = TempData[DevSweepKey];
            if (sweep != null) { vm.LastSweepSummary = sweep.ToString(); }

            return View("~/Views/Admin/QCU-CRAM_DevTools.cshtml", vm);
        }

        // Every Dev Tools POST is admin-only (base class), anti-forgery protected, and answers 404
        // when QCU-CRAM_DevToolsEnabled is switched off - the same switch the GET uses.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ShiftClock(string command)
        {
            if (!QCUCRAMDevToolsService.Enabled) { return HttpNotFound(); }

            try { ApplyDev(QCUCRAMDevToolsService.ShiftClock(command)); }
            catch (Exception ex)
            {
                string reference = QCUCRAMErrorLog.Write(ex, "AdminSetup.DevTools.ShiftClock", null);
                FlashError("The clock could not be changed. Reference: " + reference);
            }
            return RedirectToAction("DevTools");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RunSweep()
        {
            if (!QCUCRAMDevToolsService.Enabled) { return HttpNotFound(); }

            try { TempData[DevSweepKey] = QCUCRAMDevToolsService.RunSweep(); }
            catch (Exception ex)
            {
                string reference = QCUCRAMErrorLog.Write(ex, "AdminSetup.DevTools.RunSweep", null);
                TempData[DevSweepKey] = "<div class=\"alert alert-danger\">The maintenance sweep failed. Reference: " +
                    System.Web.HttpUtility.HtmlEncode(reference) + "</div>";
            }
            return RedirectToAction("DevTools");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateStudents()
        {
            if (!QCUCRAMDevToolsService.Enabled) { return HttpNotFound(); }

            ApplyDev(QCUCRAMDevToolsService.GenerateStudents(CurrentUserId));
            return RedirectToAction("DevTools");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateReservations(int daysBack, int daysAhead)
        {
            if (!QCUCRAMDevToolsService.Enabled) { return HttpNotFound(); }

            ApplyDev(QCUCRAMDevToolsService.GenerateReservations(daysBack, daysAhead, CurrentUserId));
            return RedirectToAction("DevTools");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ClearSampleReservations()
        {
            if (!QCUCRAMDevToolsService.Enabled) { return HttpNotFound(); }

            ApplyDev(QCUCRAMDevToolsService.ClearSampleReservations());
            return RedirectToAction("DevTools");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteSampleStudents()
        {
            if (!QCUCRAMDevToolsService.Enabled) { return HttpNotFound(); }

            ApplyDev(QCUCRAMDevToolsService.DeleteSampleStudents());
            return RedirectToAction("DevTools");
        }

        /// <summary>
        /// Port of btnClearAll_Click: the box must read DELETE (all capitals); anything else
        /// changes nothing and shows the original refusal message. The "I understand this cannot
        /// be undone." tick box lives in the qcuConfirm pop-up, as in the .aspx.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ClearAllReservations(string confirmText)
        {
            if (!QCUCRAMDevToolsService.Enabled) { return HttpNotFound(); }

            ApplyDev(QCUCRAMDevToolsService.ClearAllReservations(confirmText));
            return RedirectToAction("DevTools");
        }

        /// <summary>
        /// DevTools.aspx.cs RunSweepOnLoad = false: this page must not run the automatic rules
        /// by itself, so "Run sweep now" shows real counts. Applies to the page and its postbacks.
        /// </summary>
        protected override bool RunSweepOnLoad(string actionName)
        {
            switch ((actionName ?? string.Empty).ToLowerInvariant())
            {
                case "devtools":
                case "shiftclock":
                case "runsweep":
                case "generatestudents":
                case "generatereservations":
                case "clearsamplereservations":
                case "deletesamplestudents":
                case "clearallreservations":
                    return false;
                default:
                    return true;
            }
        }

        // =================================================================
        //  Helpers
        // =================================================================
        /// <summary>Toast (lblMessage) plus the detail box that stays on the page (litSampleResult).</summary>
        private void ApplyDev(QCUCRAMDevResult result)
        {
            if (result == null) { return; }

            if (!string.IsNullOrEmpty(result.Toast))
            {
                if (result.IsError) { FlashError(result.Toast); }
                else { Flash(result.Toast); }
            }

            if (!string.IsNullOrEmpty(result.DetailHtml)) { TempData[DevResultKey] = result.DetailHtml; }
        }

        private void Toast(QCUCRAMOperationResult result)
        {
            if (result == null) { return; }

            if (result.Ok) { Flash(result.Message); }
            else { FlashError(result.Message); }
        }

        private void PullModalError(QCUCRAMRoomsViewModel vm)
        {
            object error = TempData[ModalErrorKey];
            if (error == null) { return; }
            vm.ModalError = error.ToString();
            vm.ModalOpen = true;

            // keep what the person typed, as the Web Forms pop-up did
            QCUCRAMRoomEditModel typed = TempData[ModalEditKey] as QCUCRAMRoomEditModel;
            if (typed != null) { vm.Edit = typed; }
        }

        private void PullModalError(QCUCRAMTimeSlotsViewModel vm)
        {
            object error = TempData[ModalErrorKey];
            if (error == null) { return; }
            vm.ModalError = error.ToString();
            vm.ModalOpen = true;

            // keep what the person typed, as the Web Forms pop-up did
            QCUCRAMSlotEditModel typed = TempData[ModalEditKey] as QCUCRAMSlotEditModel;
            if (typed != null) { vm.Edit = typed; }
        }

        private void PullModalError(QCUCRAMStudentsViewModel vm)
        {
            object error = TempData[ModalErrorKey];
            if (error == null) { return; }
            vm.ModalError = error.ToString();
            vm.ModalOpen = true;

            // keep what the person typed, as the Web Forms pop-up did
            QCUCRAMStudentEditModel typed = TempData[ModalEditKey] as QCUCRAMStudentEditModel;
            if (typed != null) { vm.Edit = typed; }
        }

        private void PullModalError(QCUCRAMAdminUsersViewModel vm)
        {
            object error = TempData[ModalErrorKey];
            if (error == null) { return; }
            vm.ModalError = error.ToString();
            vm.ModalOpen = true;

            // keep what the person typed, as the Web Forms pop-up did
            QCUCRAMAdminEditModel typed = TempData[ModalEditKey] as QCUCRAMAdminEditModel;
            if (typed != null)
            {
                // R05: the edit pop-up no longer posts the username (the source cannot change it),
                // so keep the stored one for the "Edit Admin: {username}" title.
                if (string.IsNullOrEmpty(typed.Username) && vm.Edit != null) { typed.Username = vm.Edit.Username; }
                vm.Edit = typed;
            }
        }
    }
}
