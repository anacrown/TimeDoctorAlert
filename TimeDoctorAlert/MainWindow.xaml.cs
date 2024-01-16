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

        private readonly Func<WindowInfo, bool>  _filter = w => w.IsForeground && w.ProcessName == "Time Doctor";

        public MainWindow()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Seq("http://localhost:5341")
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
                cancellationToken.ThrowIfCancellationRequested();

                var windowsCount = _wrapper.UpdateWindowList(_filter);

                if (windowsCount > _windowsCount)
                    await CheckActivityAndPlaySound(cancellationToken);

                if (windowsCount != _windowsCount)
                {
                    Log.Information("Set windows count: {Count}", windowsCount);
                    _windowsCount = windowsCount;
                }

                await Task.Delay(500, cancellationToken);
            }
        }
        private async Task CheckActivityAndPlaySound(CancellationToken cancellationToken)
        {
            var idleThreshold = TimeSpan.FromSeconds(1);

            var counter = 0;

            const int beepDelay = 1000;
            const int maxCounter = 90;

            while (GetIdleTime() > idleThreshold)
            {
                cancellationToken.ThrowIfCancellationRequested();

                SystemSounds.Hand.Play();

                var windowsCount = _wrapper.UpdateWindowList(_filter);
                if (windowsCount <= _windowsCount)
                {
                    Log.Information("Time Doctor window closed, {Count}", windowsCount);
                    break;
                }

                if (counter++ > maxCounter)
                    break;

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