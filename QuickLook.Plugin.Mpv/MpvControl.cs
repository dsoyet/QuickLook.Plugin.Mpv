using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace QuickLook.Plugin.Mpv;

/// <summary>
/// Windows Forms control that hosts an embedded mpv video player window.
/// Launches mpv.exe with --wid to embed its video output into this control.
/// </summary>
public class MpvControl : Control
{
    private Process _mpvProcess;
    private nint _mpvWindowHandle;
    private StreamWriter _ipcWriter;
    private static int _instanceCounter;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(nint hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetParent(nint hWndChild, nint hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    private const int GWL_STYLE = -16;
    private const int WS_CHILD = 0x40000000;
    private const int WS_VISIBLE = 0x10000000;

    public MpvControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;

        Resize += OnResize;
    }

    /// <summary>
    /// Resolves the mpv executable path using the configured strategy:
    /// 1. User-configured path in settings
    /// 2. PATH environment variable
    /// 3. Scoop package manager
    /// 4. Chocolatey package manager
    /// 5. Common manual install locations
    /// </summary>
    public static string ResolveMpvPath(string configuredPath)
    {
        // 1. User-configured explicit path
        if (!string.IsNullOrEmpty(configuredPath) && File.Exists(configuredPath))
            return configuredPath;

        // 2. PATH environment variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(';'))
        {
            var mpvExe = Path.Combine(dir.Trim(), "mpv.exe");
            if (File.Exists(mpvExe)) return mpvExe;
            var mpvCom = Path.Combine(dir.Trim(), "mpv.com");
            if (File.Exists(mpvCom)) return mpvCom;
        }

        // 3. Scoop (most common on Windows)
        var scoopMpv = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "scoop", "apps", "mpv", "current", "mpv.exe");
        if (File.Exists(scoopMpv)) return scoopMpv;

        // 4. Chocolatey
        var chocoDir = @"C:\ProgramData\chocolatey\lib";
        if (Directory.Exists(chocoDir))
        {
            foreach (var dir in Directory.GetDirectories(chocoDir, "mpv*"))
            {
                var chocoMpv = Path.Combine(dir, "tools", "mpv.exe");
                if (File.Exists(chocoMpv)) return chocoMpv;
            }
        }

        // 5. Common manual install paths
        var commonPaths = new[]
        {
            @"C:\Program Files\mpv\mpv.exe",
            @"C:\Program Files (x86)\mpv\mpv.exe",
            @"C:\mpv\mpv.exe",
        };
        foreach (var p in commonPaths)
        {
            if (File.Exists(p)) return p;
        }

        return null;
    }

    /// <summary>
    /// Starts mpv and embeds it into this control.
    /// </summary>
    public void StartPlayback(string filePath, string mpvPath, string extraArgs)
    {
        StopPlayback();

        _instanceCounter++;
        var pipeName = $"ql-mpv-{Process.GetCurrentProcess().Id}-{_instanceCounter}";

        var psi = new ProcessStartInfo
        {
            FileName = mpvPath,
            Arguments = $"--wid={Handle.ToInt64()} --no-border --input-ipc-server=\\\\.\\pipe\\{pipeName} {extraArgs} \"{filePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        _mpvProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _mpvProcess.Start();

        // Track child process for auto-kill on parent exit
        try
        {
            ChildProcessTracer.Default.AddChildProcess(_mpvProcess.Handle);
        }
        catch { /* best-effort */ }

        // Wait for mpv to create its window — poll for up to 3 seconds
        var sw = Stopwatch.StartNew();
        while (_mpvWindowHandle == IntPtr.Zero && sw.ElapsedMilliseconds < 3000 && !_mpvProcess.HasExited)
        {
            _mpvProcess.Refresh();
            if (_mpvProcess.MainWindowHandle != IntPtr.Zero)
            {
                _mpvWindowHandle = _mpvProcess.MainWindowHandle;
            }
            Application.DoEvents();
            System.Threading.Thread.Sleep(50);
        }

        if (_mpvWindowHandle != IntPtr.Zero)
        {
            // Reparent mpv window to fill this control
            SetParent(_mpvWindowHandle, Handle);
            int style = GetWindowLong(_mpvWindowHandle, GWL_STYLE);
            SetWindowLong(_mpvWindowHandle, GWL_STYLE, style | WS_CHILD | WS_VISIBLE);
            FitToControl();
        }

        // Connect IPC pipe (mpv takes a moment to create it)
        ConnectIpc(pipeName);
    }

    private void ConnectIpc(string pipeName)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3000)
        {
            try
            {
                var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
                pipe.Connect(500);
                _ipcWriter = new StreamWriter(pipe, Encoding.UTF8, 4096) { AutoFlush = true };
                return;
            }
            catch
            {
                System.Threading.Thread.Sleep(100);
            }
        }
    }

    public void SendIpc(string json)
    {
        if (_ipcWriter == null) return;
        try
        {
            _ipcWriter.Write(json + "\n");
        }
        catch { }
    }

    /// <summary>Ensures the WinForms handle is created (required before StartPlayback).</summary>
    public void EnsureHandle()
    {
        if (!IsHandleCreated)
            CreateHandle();
    }

    /// <summary>
    /// Stops mpv and cleans up.
    /// </summary>
    public void StopPlayback()
    {
        if (_ipcWriter != null)
        {
            try { _ipcWriter.Dispose(); } catch { }
            _ipcWriter = null;
        }

        if (_mpvProcess != null && !_mpvProcess.HasExited)
        {
            try
            {
                _mpvProcess.Kill();
                _mpvProcess.WaitForExit(2000);
            }
            catch { }
        }

        _mpvProcess?.Dispose();
        _mpvProcess = null;
        _mpvWindowHandle = IntPtr.Zero;
    }

    private void OnResize(object sender, EventArgs e)
    {
        FitToControl();
    }

    private void FitToControl()
    {
        if (_mpvWindowHandle != IntPtr.Zero && Width > 0 && Height > 0)
        {
            MoveWindow(_mpvWindowHandle, 0, 0, Width, Height, true);
        }
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopPlayback();
        }
        base.Dispose(disposing);
    }
}
