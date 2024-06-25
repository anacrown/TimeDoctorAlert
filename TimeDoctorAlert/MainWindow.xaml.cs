using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using NAudio.Wave;
using Serilog;
using static TimeDoctorAlert.ApiWrapper;
using Application = System.Windows.Application;
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
                try
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
                }
                catch (Exception e)
                {
                    Log.Error(e, "MonitorWindow: Error");
                }

                await Task.Delay(500, cancellationToken);
            }
        }

        private async Task CheckActivityAndPlaySound(CancellationToken cancellationToken)
        {
            Log.Information("CheckActivityAndPlaySound start");

            var cancellationTokenSource = new CancellationTokenSource();
            var playSirenTask = Play(cancellationTokenSource.Token);

            try
            {
                var start = DateTime.Now;
                while (true)
                {
                    if (_wrapper.UpdateWindowList(_filter) <= _windowsCount)
                    {
                        Log.Information("Time Doctor window closed");
                        break;
                    }

                    // if (GetIdleTime() < TimeSpan.FromMilliseconds(400))
                    // {
                    //     Log.Information("User is active");
                    //     break;
                    // }

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
        public class NoteSequence
        {
            public List<(int Frequency, int Duration)> Notes { get; set; } = [];
            public int RepeatCount { get; set; }
            public int PauseAfter { get; set; }
        }

        private async Task Play(CancellationToken cancellationToken)
        {
            using WaveStream waveStream = new Mp3FileReader(new MemoryStream(Properties.Resources.imperskij_marsh_8bit));
            using WaveOutEvent waveOut = new WaveOutEvent();
            waveOut.Init(waveStream);
            waveOut.Play();

            while (cancellationToken.IsCancellationRequested == false && waveOut.PlaybackState == PlaybackState.Playing) 
                await Task.Delay(100, cancellationToken);
        }

        private static async Task PlaySirenAsync(CancellationToken cancellationToken)
        {
            var sequences = new List<NoteSequence>
            {
                new()
                {
                    Notes =
                    [
                        (440, 500), (440, 500), (440, 500), (349, 350), (523, 150),
                        (440, 500), (349, 350), (523, 150), (440, 1000)
                    ],
                    RepeatCount = 1,
                    PauseAfter = 0
                },
                new()
                {
                    Notes =
                    [
                        (659, 500), (659, 500), (659, 500), (698, 350), (523, 150),
                        (415, 500), (349, 350), (523, 150), (440, 1000)
                    ],
                    RepeatCount = 1,
                    PauseAfter = 0
                },
                new()
                {
                    Notes =
                    [
                        (880, 500), (440, 350), (440, 150), (880, 500), (830, 250),
                        (784, 250), (740, 125), (698, 125), (740, 250)
                    ],
                    RepeatCount = 1,
                    PauseAfter = 250
                },
                new()
                {
                    Notes =
                    [
                        (455, 250), (622, 500), (587, 250), (554, 250), (523, 125),
                        (466, 125), (523, 250)
                    ],
                    RepeatCount = 1,
                    PauseAfter = 250
                },
                new()
                {
                    Notes =
                    [
                        (349, 125), (415, 500), (349, 375), (440, 125),
                        (523, 500), (440, 375), (523, 125), (659, 1000)
                    ],
                    RepeatCount = 1,
                    PauseAfter = 0
                },
                new()
                {
                    Notes =
                    [
                        (880, 500), (440, 350), (440, 150), (880, 500), (830, 250),
                        (784, 250), (740, 125), (698, 125), (740, 250)
                    ],
                    RepeatCount = 1,
                    PauseAfter = 250
                },
                new()
                {
                    Notes =
                    [
                        (455, 250), (622, 500), (587, 250), (554, 250), (523, 125),
                        (466, 125), (523, 250)
                    ],
                    RepeatCount = 1,
                    PauseAfter = 250
                },
                new()
                {
                    Notes =
                    [
                        (349, 250), (415, 500), (349, 375), (523, 125),
                        (440, 500), (349, 375), (261, 125), (440, 1000)
                    ],
                    RepeatCount = 1,
                    PauseAfter = 100
                }
            };

            while (cancellationToken.IsCancellationRequested == false)
                foreach (var sequence in sequences)
                    for (var i = 0; i < sequence.RepeatCount; i++)
                    {
                        foreach (var note in sequence.Notes)
                            await BeepAsync(note.Frequency, note.Duration, cancellationToken);
                        if (sequence.PauseAfter > 0)
                            await Task.Delay(sequence.PauseAfter, cancellationToken);
                    }

            cancellationToken.ThrowIfCancellationRequested();
        }

        private static async Task BeepAsync(int frequency, int duration, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Console.Beep(frequency, duration);
            }, cancellationToken);
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