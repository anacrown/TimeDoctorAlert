using System.Media;
using System.Windows;
using System.Windows.Forms;
using Serilog;
using static TimeDoctorAlert.ApiWrapper;
using Window = System.Windows.Window;


namespace TimeDoctorAlert
{
    public partial class MainWindow : Window
    {
        private readonly NotifyIcon _trayIcon;
        private readonly ApiWrapper _wrapper = new();

        private int _windowsCount;

        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private readonly Func<WindowInfo, bool> _filter = w => w.ProcessName == "Time Doctor" &&
                                                                w.Rect.Right - w.Rect.Left > 550 &&
                                                                w.Rect.Bottom - w.Rect.Top > 100;

        public MainWindow()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Seq(Properties.Resources.SeqUrl)
                .CreateLogger();

            InitializeComponent();
            Hide();

            _trayIcon = new NotifyIcon
            {
                Icon = Properties.Resources.logo,
                Visible = true
            };

            _trayIcon.DoubleClick += (sender, args) =>
            {
                Show();
                WindowState = WindowState.Normal;
            };

            Closed += MainWindow_Closed;

            Task.Run(() => MonitorWindow(_cancellationTokenSource.Token));
        }

        private void MainWindow_Closed(object sender, EventArgs e) => _cancellationTokenSource.Cancel();

        private async Task MonitorWindow(CancellationToken cancellationToken)
        {
            while (cancellationToken.IsCancellationRequested == false)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Log.Information("MonitorWindow: Cancellation requested");
                    break;
                }

                var windowsCount = _wrapper.UpdateWindowList(_filter);

                if (windowsCount > _windowsCount)
                    await CheckActivityAndPlaySound(cancellationToken);

                if (_windowsCount != windowsCount)
                {
                    Log.Information($"Windows count changed: {_windowsCount} -> {windowsCount}");
                    _windowsCount = windowsCount;
                }

                await Task.Delay(500, cancellationToken);
            }
        }
        private async Task CheckActivityAndPlaySound(CancellationToken cancellationToken)
        {
            Log.Information("CheckActivityAndPlaySound start");

            var counter = 0;

            const int beepDelay = 1000;
            const int maxCounter = 90;

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Log.Information("CheckActivityAndPlaySound: Cancellation requested");
                    break;
                }

                Log.Information("Beep");
                SystemSounds.Hand.Play();

                if (_wrapper.UpdateWindowList(_filter) == 0)
                {
                    Log.Information("Time Doctor window closed");
                    break;
                }

                if (GetIdleTime() < TimeSpan.FromSeconds(1))
                {
                    Log.Information("User is active");
                    break;
                }

                if (counter++ > maxCounter)
                {
                    Log.Information("Max counter reached");
                    break;
                }

                await Task.Delay(beepDelay, cancellationToken);
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized) Hide();
            base.OnStateChanged(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _trayIcon.Dispose();
            base.OnClosed(e);
        }
    }
}