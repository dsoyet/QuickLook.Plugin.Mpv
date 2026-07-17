using QuickLook.Common.Helpers;
using QuickLook.Common.Plugin;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace QuickLook.Plugin.Mpv;

public class Plugin : IViewer
{
    private static string _mpvExePath;
    private static string _mpvExtraArgs;
    private static bool _initialized;
    private static readonly string LogPath = Path.Combine(
        Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".",
        "QuickLook.Plugin.Mpv.log");

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} [{AppDomain.CurrentDomain.FriendlyName}] {msg}\n"); }
        catch { /* best effort */ }
    }

    public int Priority => 10;

    public void Init()
    {
        if (_initialized)
            return;

        try
        {
            Log("Init() called");
            var configuredPath = SettingHelper.Get("MpvPath", string.Empty, "QuickLook.Plugin.Mpv");
            Log($"Configured path: '{configuredPath}'");
            _mpvExePath = MpvControl.ResolveMpvPath(configuredPath);
            Log($"Resolved mpv: '{_mpvExePath ?? "NULL"}'");
            _mpvExtraArgs = SettingHelper.Get("MpvArgs", "--no-osc --no-input-default-bindings --loop-file=no --keep-open=no", "QuickLook.Plugin.Mpv");
            Log($"Extra args: '{_mpvExtraArgs}'");
        }
        catch (Exception ex)
        {
            Log($"Init FAILED: {ex}");
            _mpvExePath = null;
        }

        _initialized = true;
    }

    public bool CanHandle(string path)
    {
        Log($"CanHandle('{path}') mpvPath='{_mpvExePath ?? "NULL"}'");

        if (Directory.Exists(path))
        {
            Log("-> false (directory)");
            return false;
        }

        if (string.IsNullOrEmpty(_mpvExePath))
        {
            Log("-> false (no mpv)");
            return false;
        }

        var ext = Path.GetExtension(path);
        var result = ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".avi", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".webm", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".wmv", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".flv", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".m4v", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".mpg", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".mpeg", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".m2ts", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".ogv", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".3gp", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".rmvb", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".asf", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".vob", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".divx", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".mts", StringComparison.OrdinalIgnoreCase);
        Log($"-> {result}");
        return result;
    }

    public void Prepare(string path, ContextObject context)
    {
        context.SetPreferredSizeFit(new Size(1280, 720), 0.9d);
        context.Title = Path.GetFileName(path);
    }

    public void View(string path, ContextObject context)
    {
        Log($"View('{path}') mpvPath='{_mpvExePath ?? "NULL"}'");
        try
        {
            var panel = new MpvPanel();
            panel.StartPreview(path, _mpvExePath, _mpvExtraArgs);
            context.ViewerContent = panel;
            context.IsBusy = false;
            Log("View OK");
        }
        catch (Exception ex)
        {
            Log($"View FAILED: {ex}");
            context.ViewerContent = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Content = $"Failed to start video preview:\n{ex.Message}",
                Foreground = System.Windows.Media.Brushes.White,
            };
            context.IsBusy = false;
        }
    }

    public void Cleanup()
    {
    }
}
