using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;

namespace TimeDoctorAlert;

public class ApiWrapper
{
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; }
        public string ProcessName { get; set; }
        public RECT Rect { get; set; }
        public string ClassName { get; set; }
        public bool IsForeground { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc enumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public static List<WindowInfo> GetAllWindows()
    {
        var windows = new List<WindowInfo>();
        EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
        {
            try
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                var windowInfo = new WindowInfo
                {
                    Handle = hWnd
                };

                var sb = new StringBuilder(GetWindowTextLength(hWnd) + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                windowInfo.Title = sb.ToString();

                GetWindowRect(hWnd, out RECT rect);
                windowInfo.Rect = rect;

                GetWindowThreadProcessId(hWnd, out var processId);

                var process = Process.GetProcessById((int)processId);
                windowInfo.ProcessName = process.ProcessName;

                var className = new StringBuilder(256);
                GetClassName(hWnd, className, className.Capacity);
                windowInfo.ClassName = className.ToString();

                windowInfo.IsForeground = (hWnd == GetForegroundWindow());

                windows.Add(windowInfo);
            }
            catch (Exception)
            {
                // ignored
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }
    public static void LogWindow(string action, WindowInfo window)
    {
        var size = $"{window.Rect.Right - window.Rect.Left}x{window.Rect.Bottom - window.Rect.Top}";
        var position = $"{window.Rect.Left};{window.Rect.Top}";

        Log.Information("[{Action}]  Handle: {Handle}, Title: {Title}, Process: {ProcessName}, Size: {Size}, Position: {Position}, Class: {ClassName}, IsForeground: {IsForeground}",
            action, window.Handle, window.Title, window.ProcessName, size, position, window.ClassName, window.IsForeground);
    }

    private List<WindowInfo> _windows = new();
    public int UpdateWindowList(Func<WindowInfo, bool> query)
    {
        var windows = GetAllWindows();

        var snapshot = windows
            .Where(query)
            .ToList();

        var newWindows = snapshot
            .Where(w => _windows.All(w2 => w2.Handle != w.Handle))
            .ToList();

        var closedWindows = _windows
            .Where(w => snapshot.All(w2 => w2.Handle != w.Handle))
            .ToList();

        var changedWindows = snapshot
            .Where(w => _windows.Any(w2 => w2.Handle == w.Handle && w2.IsForeground != w.IsForeground))
            .ToList();

        if (newWindows.Count != 0 || closedWindows.Count != 0 || changedWindows.Count != 0)
        {
            Console.WriteLine();
            Console.WriteLine(DateTime.Now.ToLongTimeString());

            foreach (var window in newWindows)
                LogWindow("OPEN", window);

            foreach (var window in closedWindows)
                LogWindow("CLOSE", window);

            foreach (var window in changedWindows)
                LogWindow("CHANGE", window);

            _windows = snapshot;

            Log.Information("Windows count: {Count}", _windows.Count);
        }

        return _windows.Count;
    }
    public static TimeSpan GetIdleTime()
    {
        var lastInputInfo = new LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO))
        };

        if (!GetLastInputInfo(ref lastInputInfo))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        return TimeSpan.FromMilliseconds(Environment.TickCount - lastInputInfo.dwTime);
    }
}