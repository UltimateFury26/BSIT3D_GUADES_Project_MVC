namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Security
{
    /// <summary>
    /// Every action here requires an Active Student account - the MVC equivalent of
    /// Guades_QCUCram_StudentPage.
    ///
    /// Both attributes are deliberate: [QCUCRAM_RequireStudent] is an IAuthorizationFilter
    /// (runs first, checks the role) and [QCUCRAM_RequireLogin] is an ActionFilter (runs
    /// after, re-checks that the account still exists).
    /// </summary>
    [QCUCRAM_RequireStudent]
    [QCUCRAM_RequireLogin]
    public abstract class QCUCRAM_StudentController : QCUCRAM_BaseController
    {
    }
}
