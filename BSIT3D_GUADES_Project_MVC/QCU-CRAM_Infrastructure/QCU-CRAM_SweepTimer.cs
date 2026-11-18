using System;
using System.Configuration;
using System.Diagnostics;
using System.Threading;
using System.Web.Hosting;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Services;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Infrastructure
{
    /// <summary>
    /// QCU-CRAM  |  QCU-CRAM_Infrastructure/QCU-CRAM_SweepTimer.cs
    ///
    /// A hosted background timer that runs the maintenance sweep on a fixed period. It is
    /// registered with ASP.NET (IRegisteredObject) so the runtime knows a long-running
    /// object exists and does not tear the app domain down underneath it without calling
    /// Stop first.
    ///
    /// Replaces QCUCRAM_MaintenanceService.StartBackgroundTimer for the Phase 10 wiring,
    /// but the service method is kept for compatibility.
    /// </summary>
    public sealed class QCUCRAMSweepTimer : IRegisteredObject
    {
        private static readonly Lazy<QCUCRAMSweepTimer> _instance =
            new Lazy<QCUCRAMSweepTimer>(() => new QCUCRAMSweepTimer());

        private readonly object _gate = new object();
        private Timer _timer;
        private int _ticking;
        private int _minGapSeconds = 30;
        private bool _registered;

        public static QCUCRAMSweepTimer Instance { get { return _instance.Value; } }

        private QCUCRAMSweepTimer() { }

        /// <summary>Starts the timer if it is enabled and not already running.</summary>
        public static void Start()
        {
            _instance.Value.StartCore();
        }

        /// <summary>Stops the timer and unregisters the hosted object.</summary>
        public static void Stop()
        {
            if (_instance.IsValueCreated) { _instance.Value.StopCore(); }
        }

        private void StartCore()
        {
            lock (_gate)
            {
                if (_timer != null) { return; }

                if (!IsOn("QCU-CRAM_SweepTimerEnabled", true)) { return; }

                int seconds = ReadInt("QCU-CRAM_SweepTimerSeconds", 60, 15);

                if (!_registered)
                {
                    try
                    {
                        HostingEnvironment.RegisterObject(this);
                        _registered = true;
                    }
                    catch (Exception ex)
                    {
                        QCUCRAMErrorLog.Write(ex, "sweep-timer-register", null);
                    }
                }

                // Web Forms StartBackgroundTimer: first tick after 5 seconds, then every period.
                _minGapSeconds = Math.Max(5, seconds / 2);
                TimeSpan period = TimeSpan.FromSeconds(seconds);
                _timer = new Timer(Tick, null, TimeSpan.FromSeconds(5), period);
            }
        }

        private void StopCore()
        {
            lock (_gate)
            {
                if (_timer != null)
                {
                    try { _timer.Dispose(); }
                    catch { /* nothing useful to do */ }
                    _timer = null;
                }

                if (_registered)
                {
                    try { HostingEnvironment.UnregisterObject(this); }
                    catch { /* the host may already be shutting down */ }
                    _registered = false;
                }
            }
        }

        private void Tick(object state)
        {
            // Skip this tick if the previous one is still running (a slow database).
            if (Interlocked.CompareExchange(ref _ticking, 1, 0) != 0) { return; }

            try
            {
                // Same gate as the Web Forms timer (TryRunIfDue(minGap)): skip when the request-driven
                // sweep or an earlier tick ran recently.
                QCUCRAM_MaintenanceService.SweepResult result = QCUCRAM_MaintenanceService.RunIfDue(_minGapSeconds);
                if (result != null && result.ChangedAnything)
                {
                    Trace.TraceInformation("QCU-CRAM sweep (timer): " + result.Summary);
                }
            }
            catch (Exception ex)
            {
                QCUCRAMErrorLog.Write(ex, "sweep-timer", null);
            }
            finally
            {
                Interlocked.Exchange(ref _ticking, 0);
            }
        }

        void IRegisteredObject.Stop(bool immediate)
        {
            // StopCore disposes the timer and unregisters this object (once).
            // Do not call UnregisterObject again here: the runtime already knows
            // this object is going away and a second call is unnecessary.
            StopCore();
        }

        private static bool IsOn(string key, bool fallback)
        {
            string raw = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(raw)) { return fallback; }
            return string.Equals(raw.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }

        private static int ReadInt(string key, int fallback, int minimum)
        {
            string raw = ConfigurationManager.AppSettings[key];
            int value;
            if (!int.TryParse(raw, out value)) { value = fallback; }
            return value < minimum ? minimum : value;
        }
    }
}
