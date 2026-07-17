using QuickLook.Common.Helpers;
using QuickLook.Common.Plugin;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace QuickLook.Plugin.Mpv;

public class Plugin : IViewer
{
    private string _mpvExePath;
    private string _mpvExtraArgs;

    public int Priority => 10;

    public void Init()
    {
        try
        {
            var configuredPath = SettingHelper.Get("MpvPath", string.Empty, "QuickLook.Plugin.Mpv");
            _mpvExePath = MpvControl.ResolveMpvPath(configuredPath);
            _mpvExtraArgs = SettingHelper.Get("MpvArgs", "--no-osc --no-input-default-bindings --loop-file=no --keep-open=no", "QuickLook.Plugin.Mpv");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuickLook.Plugin.Mpv] Init failed: {ex}");
            _mpvExePath = null;
        }
    }

    public bool CanHandle(string path)
    {
        if (Directory.Exists(path))
            return false;

        if (string.IsNullOrEmpty(_mpvExePath))
            return false;

        return path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".avi", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".mov", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".wmv", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".flv", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".m4v", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".mpg", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".mpeg", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".m2ts", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".ogv", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".3gp", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".rmvb", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".asf", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".vob", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".divx", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".mts", StringComparison.OrdinalIgnoreCase);
    }

    public void Prepare(string path, ContextObject context)
    {
        context.SetPreferredSizeFit(new Size(1280, 720), 0.9d);
        context.Title = Path.GetFileName(path);
    }

    public void View(string path, ContextObject context)
    {
        try
        {
            var panel = new MpvPanel();
            panel.StartPreview(path, _mpvExePath, _mpvExtraArgs);
            context.ViewerContent = panel;
            context.IsBusy = false;
        }
        catch (Exception ex)
        {
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
