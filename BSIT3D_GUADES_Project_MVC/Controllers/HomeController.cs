using System.Linq;
using System.Web.Mvc;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Data;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;

namespace BSIT3D_GUADES_Project_MVC.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";
            return View();
        }

        // Phase 1 verification (kept so this drop does not lose it).
        public ActionResult QCUCRAM_DbCheck()
        {
            using (var cn = new System.Data.SqlClient.SqlConnection(QCUCRAM_AppSettings.ConnectionString))
            {
                cn.Open();
                using (var cmd = new System.Data.SqlClient.SqlCommand(
                    "SELECT DB_NAME(), (SELECT COUNT(*) FROM dbo.Rooms)", cn))
                using (var r = cmd.ExecuteReader())
                {
                    r.Read();
                    return Content(r.GetString(0) + " / rooms=" + r.GetInt32(1));
                }
            }
        }

        // Phase 2 verification only. Remove after the phase is signed off.
        public ActionResult QCUCRAM_ModelCheck()
        {
            using (var db = new QCUCRAMContext())
            {
                return Content(string.Format(
                    "rooms={0} slots={1} students={2} admins={3} reservations={4} members={5} history={6} notifications={7} settings={8}",
                    db.Rooms.Count(), db.TimeSlots.Count(), db.Students.Count(),
                    db.AdminUsers.Count(), db.Reservations.Count(),
                    db.ReservationMembers.Count(), db.ReservationStatusHistories.Count(),
                    db.Notifications.Count(), db.Settings.Count()));
            }
        }
    }
}
