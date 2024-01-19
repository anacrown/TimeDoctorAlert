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
            
            var cancellationTokenSource = new CancellationTokenSource();
            var playSirenTask = PlaySirenAsync(cancellationTokenSource.Token);

            try
            {
                var start = DateTime.Now;
                while (true)
                {
                    if (_wrapper.UpdateWindowList(_filter) <=  _windowsCount)
                    {
                        Log.Information("Time Doctor window closed");
                        break;
                    }

                    if (GetIdleTime() < TimeSpan.FromMilliseconds(400))
                    {
                        Log.Information("User is active");
                        break;
                    }

                    if (DateTime.Now - start > TimeSpan.FromMinutes(1))
                    {
                        Log.Information("User is inactive for 1 minute");
                        break;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            finally
            {
                cancellationTokenSource.Cancel();
                try
                {
                    await playSirenTask;
                }
                catch (OperationCanceledException)
                {
                    Log.Information("Siren playback was successfully stopped");
                }
            }
        }

        private async Task PlaySirenAsync(CancellationToken cancellationToken)
        {
            var highFreq = 1000;
            var duration = 1000;

            while (!cancellationToken.IsCancellationRequested)
            {
                Console.Beep(highFreq, 100);
                await Task.Delay(duration, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
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