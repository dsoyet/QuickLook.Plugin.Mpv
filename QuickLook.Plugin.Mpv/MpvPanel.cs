using System.Windows;
using System.Windows.Controls;

namespace QuickLook.Plugin.Mpv;

public partial class MpvPanel : UserControl
{
    private MpvControl _mpvControl;
    private string _filePath;
    private string _mpvExePath;
    private string _mpvExtraArgs;

    public MpvPanel()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    public void StartPreview(string filePath, string mpvExePath, string extraArgs)
    {
        _filePath = filePath;
        _mpvExePath = mpvExePath;
        _mpvExtraArgs = extraArgs;

        _mpvControl?.Dispose();
        _mpvControl = new MpvControl();
        Presenter.Child = _mpvControl;

        // Defer actual playback start to Loaded so the control has a valid handle
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _mpvControl?.StartPlayback(_filePath, _mpvExePath, _mpvExtraArgs);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _mpvControl?.Dispose();
        _mpvControl = null;
    }
}
