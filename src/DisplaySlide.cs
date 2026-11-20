using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class DisplaySlide
{
    [STAThread]
    private static void Main()
    {
        bool firstInstance;
        using (var instance = new Mutex(true, @"Local\DisplaySlide", out firstInstance))
        {
            if (!firstInstance)
            {
                ShortcutWindow.ShowExisting();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var app = new TrayApp())
            {
                Application.Idle += app.OpenOnLaunch;
                Application.Run(app);
            }
        }
    }
}

internal sealed class ShortcutWindow : NativeWindow, IDisposable
{
    private const string Caption = "Display Slide Shortcut Window";
    private const int HotKeyMessage = 0x0312;
    private static readonly uint ShowMessage = RegisterWindowMessage("DisplaySlide.ShowPopup");
    private readonly Action open;
    internal readonly bool Registered;

    internal ShortcutWindow(Action open)
    {
        this.open = open;
        CreateHandle(new CreateParams { Caption = Caption });
        Registered = RegisterHotKey(Handle, 1, 0x4000 | 0x0002 | 0x0001, (uint)Keys.L);
    }

    internal static void ShowExisting()
    {
        IntPtr window = FindWindow(null, Caption);
        if (window != IntPtr.Zero) PostMessage(window, ShowMessage, IntPtr.Zero, IntPtr.Zero);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == HotKeyMessage || message.Msg == ShowMessage) open();
        base.WndProc(ref message);
    }

    public void Dispose()
    {
        if (Registered) UnregisterHotKey(Handle, 1);
        DestroyHandle();
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string caption);
    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}

internal sealed class TrayApp : ApplicationContext
{
    private readonly ShortcutWindow shortcut;
    private readonly Icon icon;
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu;
    private SlidePopup popup;
    private bool disposed;

    internal TrayApp()
    {
        shortcut = new ShortcutWindow(Open);
        icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        menu = new ContextMenuStrip();
        menu.Items.Add(shortcut.Registered ? "Open (Ctrl+Alt+L)" : "Open", null, (sender, args) => Open());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (sender, args) => Exit());
        tray = new NotifyIcon { Icon = icon, Text = "Display Slide", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (sender, args) => Open();
    }

    internal void OpenOnLaunch(object sender, EventArgs args)
    {
        Application.Idle -= OpenOnLaunch;
        Open();
    }

    private void Exit()
    {
        if (popup != null) popup.Close();
        if (popup == null) ExitThread();
    }

    private void Open()
    {
        if (popup != null)
        {
            popup.Activate();
            return;
        }
        popup = new SlidePopup(shortcut.Registered);
        popup.Icon = icon;
        popup.FormClosed += (sender, args) => popup = null;
        popup.Show();
        popup.Activate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            if (popup != null) popup.Dispose();
            tray.Dispose();
            menu.Dispose();
            icon.Dispose();
            shortcut.Dispose();
        }
        base.Dispose(disposing);
    }
}
