using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
        Focusable = true;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public void StartPreview(string filePath, string mpvExePath, string extraArgs)
    {
        _filePath = filePath;
        _mpvExePath = mpvExePath;
        _mpvExtraArgs = extraArgs;

        _mpvControl?.Dispose();
        _mpvControl = new MpvControl();
        Presenter.Child = _mpvControl;

        Loaded += OnLoaded;
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_mpvControl == null || _mpvControl.IsDisposed) return;

        if (e.Key == Key.Escape)
        {
            // Let QuickLook handle Escape naturally
            return;
        }

        e.Handled = true;

        switch (e.Key)
        {
            case Key.Space:      _mpvControl.SendIpc("{\"command\":[\"cycle\",\"pause\"]}"); break;
            case Key.Left:       _mpvControl.SendIpc("{\"command\":[\"seek\",-5]}"); break;
            case Key.Right:      _mpvControl.SendIpc("{\"command\":[\"seek\",5]}"); break;
            case Key.Up:         _mpvControl.SendIpc("{\"command\":[\"add\",\"volume\",2]}"); break;
            case Key.Down:       _mpvControl.SendIpc("{\"command\":[\"add\",\"volume\",-2]}"); break;
            case Key.OemOpenBrackets: _mpvControl.SendIpc("{\"command\":[\"multiply\",\"speed\",0.5]}"); break;
            case Key.OemCloseBrackets: _mpvControl.SendIpc("{\"command\":[\"multiply\",\"speed\",2.0]}"); break;
            case Key.F:          _mpvControl.SendIpc("{\"command\":[\"cycle\",\"fullscreen\"]}"); break;
            case Key.M:          _mpvControl.SendIpc("{\"command\":[\"cycle\",\"mute\"]}"); break;
            case Key.D9:         _mpvControl.SendIpc("{\"command\":[\"add\",\"volume\",-2]}"); break;
            case Key.D0:         _mpvControl.SendIpc("{\"command\":[\"add\",\"volume\",2]}"); break;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        // Defer to let WinForms control fully initialize its handle inside WindowsFormsHost
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_mpvControl != null && !_mpvControl.IsDisposed)
            {
                if (!_mpvControl.IsHandleCreated)
                    _mpvControl.EnsureHandle();
                _mpvControl.StartPlayback(_filePath, _mpvExePath, _mpvExtraArgs);
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _mpvControl?.Dispose();
        _mpvControl = null;
    }
}
