namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Security
{
    /// <summary>
    /// Every action here requires an Active Admin account - the MVC equivalent of
    /// Guades_QCUCram_AdminPage.
    ///
    /// Both attributes are deliberate: [QCUCRAM_RequireAdmin] is an IAuthorizationFilter
    /// (runs first, checks the role) and [QCUCRAM_RequireLogin] is an ActionFilter (runs
    /// after, re-checks that the library assistant account is still IsActive = 1).
    /// </summary>
    [QCUCRAM_RequireAdmin]
    [QCUCRAM_RequireLogin]
    public abstract class QCUCRAM_AdminController : QCUCRAM_BaseController
    {
    }
}
