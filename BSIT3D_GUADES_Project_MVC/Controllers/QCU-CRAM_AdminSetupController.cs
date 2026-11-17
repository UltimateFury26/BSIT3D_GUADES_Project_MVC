using System.Collections.Generic;
using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Security;
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
        private const string SettingsErrorKey = "QCUCRAM_SettingsError";
        private const string DevResultKey = "QCUCRAM_DevResult";
        private const string DevSweepKey = "QCUCRAM_DevSweep";

        // =================================================================
        //  Rooms
        // =================================================================
        [HttpGet]
        public ActionResult Rooms(int? edit)
        {
            QCUCRAMRoomsViewModel vm = QCUCRAMSetupService.Rooms(edit);
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
            Toast(QCUCRAMSetupService.ToggleRoom(id));
            return RedirectToAction("Rooms");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteRoom(int id)
        {
            Toast(QCUCRAMSetupService.DeleteRoom(id));
            return RedirectToAction("Rooms");
        }

        // =================================================================
        //  Time slots
        // =================================================================
        [HttpGet]
        public ActionResult TimeSlots(int? edit)
        {
            QCUCRAMTimeSlotsViewModel vm = QCUCRAMSetupService.TimeSlots(edit);
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
            Toast(QCUCRAMSetupService.ToggleSlot(id));
            return RedirectToAction("TimeSlots");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteSlot(int id)
        {
            Toast(QCUCRAMSetupService.DeleteSlot(id));
            return RedirectToAction("TimeSlots");
        }

        // =================================================================
        //  Student accounts
        // =================================================================
        [HttpGet]
        public ActionResult Students(string q, string program, int? page, int? edit)
        {
            QCUCRAMStudentsViewModel vm =
                QCUCRAMSetupService.Students(q, program, page ?? 1, 20);

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
                return RedirectToAction("Students",
                    new { edit = model != null && model.StudentID > 0 ? model.StudentID : (int?)null });
            }

            Toast(result);
            return RedirectToAction("Students");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetStudentPassword(int id)
        {
            Toast(QCUCRAMSetupService.ResetStudentPassword(id));
            return RedirectToAction("Students");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteStudent(int id)
        {
            Toast(QCUCRAMSetupService.DeleteStudent(id));
            return RedirectToAction("Students");
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
            Toast(QCUCRAMSetupService.ToggleAdmin(id));
            return RedirectToAction("AdminUsers");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetAdminPassword(int id)
        {
            Toast(QCUCRAMSetupService.ResetAdminPassword(id));
            return RedirectToAction("AdminUsers");
        }

        // =================================================================
        //  Settings
        // =================================================================
        [HttpGet]
        public ActionResult Settings()
        {
            QCUCRAMSettingsViewModel vm = QCUCRAMSetupService.Settings();

            object error = TempData[SettingsErrorKey];
            if (error != null) { vm.Error = error.ToString(); }

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
                TempData[SettingsErrorKey] = result.Message;
                return RedirectToAction("Settings");
            }

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

            QCUCRAMDevToolsViewModel vm = QCUCRAMDevToolsService.Load();

            object last = TempData[DevResultKey];
            if (last != null) { vm.LastResult = last.ToString(); }

            object sweep = TempData[DevSweepKey];
            if (sweep != null) { vm.LastSweepSummary = sweep.ToString(); }

            return View("~/Views/Admin/QCU-CRAM_DevTools.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ShiftClock(string command)
        {
            TempData[DevResultKey] = QCUCRAMDevToolsService.ShiftClock(command);
            return RedirectToAction("DevTools");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RunSweep()
        {
            TempData[DevSweepKey] = QCUCRAMDevToolsService.RunSweep();
            return RedirectToAction("DevTools");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateStudents()
        {
            TempData[DevResultKey] = QCUCRAMDevToolsService.GenerateStudents(CurrentUserId);
            return RedirectToAction("DevTools");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateReservations(int daysBack, int daysAhead)
        {
            TempData[DevResultKey] = QCUCRAMDevToolsService.GenerateReservations(
                daysBack, daysAhead, CurrentUserId);
            return RedirectToAction("DevTools");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ClearSampleReservations()
        {
            TempData[DevResultKey] = QCUCRAMDevToolsService.ClearSampleReservations();
            return RedirectToAction("DevTools");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteSampleStudents()
        {
            TempData[DevResultKey] = QCUCRAMDevToolsService.DeleteSampleStudents();
            return RedirectToAction("DevTools");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ClearAllReservations()
        {
            TempData[DevResultKey] = QCUCRAMDevToolsService.ClearAllReservations();
            return RedirectToAction("DevTools");
        }

        // =================================================================
        //  Helpers
        // =================================================================
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
        }

        private void PullModalError(QCUCRAMTimeSlotsViewModel vm)
        {
            object error = TempData[ModalErrorKey];
            if (error == null) { return; }
            vm.ModalError = error.ToString();
            vm.ModalOpen = true;
        }

        private void PullModalError(QCUCRAMStudentsViewModel vm)
        {
            object error = TempData[ModalErrorKey];
            if (error == null) { return; }
            vm.ModalError = error.ToString();
            vm.ModalOpen = true;
        }

        private void PullModalError(QCUCRAMAdminUsersViewModel vm)
        {
            object error = TempData[ModalErrorKey];
            if (error == null) { return; }
            vm.ModalError = error.ToString();
            vm.ModalOpen = true;
        }
    }
}
