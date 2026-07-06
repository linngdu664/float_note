using System.Drawing;
using System.Windows.Forms;

namespace FloatNote.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public TrayService(Action showWindow, Action hideWindow, Action exitApplication)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示浮签", null, (_, _) => showWindow());
        menu.Items.Add("隐藏浮签", null, (_, _) => hideWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exitApplication());

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "浮签 FloatNote",
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => showWindow();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }

    private static Icon LoadAppIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/floatnote.ico"));
        if (resource is null)
        {
            return SystemIcons.Application;
        }

        using var stream = resource.Stream;
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }
}
