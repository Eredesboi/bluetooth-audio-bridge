using System.ComponentModel;
using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace BluetoothAudioBridge;

/// <summary>Notification-area icon: closing the window hides it, the app keeps running here.</summary>
public sealed class Tray : IDisposable
{
    readonly Window _win;
    readonly Forms.NotifyIcon _icon;
    bool _quit, _tipShown;

    public Tray(Window win)
    {
        _win = win;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => Show());
        menu.Items.Add("Quit", null, (_, _) => Quit());
        _icon = new Forms.NotifyIcon { Icon = MakeIcon(), Text = "Bluetooth Audio Bridge", ContextMenuStrip = menu, Visible = true };
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Show(); };

        win.Closing += OnClosing;
        Application.Current.SessionEnding += (_, _) => _quit = true; // let Windows shut down
    }

    public void SetText(string s)
    {
        var t = "Bluetooth Audio Bridge — " + s; // tooltip is limited to 63 characters
        _icon.Text = t.Length > 63 ? t[..63] : t;
    }

    public void Show()
    {
        _win.Show();
        if (_win.WindowState == WindowState.Minimized) _win.WindowState = WindowState.Normal;
        _win.Activate();
    }

    void Quit() { _quit = true; _win.Close(); Application.Current.Shutdown(); }

    void OnClosing(object? s, CancelEventArgs e)
    {
        if (_quit) return;
        e.Cancel = true;
        _win.Hide();
        if (_tipShown) return;
        _tipShown = true;
        _icon.ShowBalloonTip(3000, "Still running", "Bluetooth Audio Bridge keeps your phone connected from the notification area.", Forms.ToolTipIcon.Info);
    }

    static Drawing.Icon MakeIcon()
    {
        var c = SystemParameters.WindowGlassColor;
        using var bmp = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAlias;
            using var fill = new Drawing.SolidBrush(Drawing.Color.FromArgb(c.R, c.G, c.B));
            g.FillEllipse(fill, 1, 1, 30, 30);
            using var f = new Drawing.Font("Segoe UI", 18, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
            using var sf = new Drawing.StringFormat { Alignment = Drawing.StringAlignment.Center, LineAlignment = Drawing.StringAlignment.Center };
            g.DrawString("B", f, Drawing.Brushes.White, new Drawing.RectangleF(0, 0, 32, 32), sf);
        }
        return Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose() { _icon.Visible = false; _icon.Dispose(); }
}
