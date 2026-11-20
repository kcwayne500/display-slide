using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class SlidePopup : Form
{
    private readonly SlideBar slider;
    private readonly Label header;
    private readonly Label status;
    private readonly Button test;
    private readonly Button apply;
    private readonly DisplayPreview preview;
    private readonly bool shortcutRegistered;
    private DisplayLayout live;
    private DisplayLayout.Display laptop;
    private DisplayLayout.Display[] externals;
    private Point livePosition;

    internal SlidePopup(bool shortcutRegistered)
    {
        this.shortcutRegistered = shortcutRegistered;
        Text = "Display Slide";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9);
        ClientSize = new Size(418, 248);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        BackColor = SlideColors.Window;
        ForeColor = SlideColors.Text;
        DoubleBuffered = true;

        header = new Label { Text = "Display Slide", Bounds = new Rectangle(15, 6, 325, 25),
            TextAlign = ContentAlignment.MiddleLeft, Font = new Font(Font, FontStyle.Bold) };
        header.MouseDown += DragWindow;
        Controls.Add(header);
        var close = new SlideButton("\u00D7") { Bounds = new Rectangle(375, 5, 33, 25), TabStop = false,
            AccessibleName = "Close and cancel", Quiet = true };
        close.Click += (sender, args) => Close();
        Controls.Add(close);

        preview = new DisplayPreview { Bounds = new Rectangle(12, 35, 394, 132),
            AccessibleName = "Display arrangement preview", AccessibleRole = AccessibleRole.Graphic };
        Controls.Add(preview);
        slider = new SlideBar { Bounds = new Rectangle(17, 173, 384, 24), TabIndex = 0,
            AccessibleName = "Laptop horizontal position" };
        slider.ValueChanged += (sender, args) => UpdatePreview();
        Controls.Add(slider);
        status = new Label { Bounds = new Rectangle(15, 206, 170, 32), ForeColor = SlideColors.Muted,
            TextAlign = ContentAlignment.MiddleLeft };
        Controls.Add(status);

        var cancel = new SlideButton("Cancel") { Bounds = new Rectangle(185, 207, 69, 29), TabIndex = 1 };
        var testButton = new SlideButton("Test") { Bounds = new Rectangle(261, 207, 69, 29), TabIndex = 2 };
        var applyButton = new SlideButton("Apply") { Bounds = new Rectangle(337, 207, 69, 29), TabIndex = 3 };
        cancel.Click += (sender, args) => Close();
        testButton.Click += (sender, args) => ChangePosition(false);
        applyButton.Click += (sender, args) => ChangePosition(true);
        test = testButton;
        apply = applyButton;
        Controls.AddRange(new Control[] { cancel, test, apply });
        CancelButton = cancel;

        LoadLayout();
        ActiveControl = slider.Enabled ? (Control)slider : cancel;
    }

    private void LoadLayout()
    {
        try
        {
            var layout = DisplayLayout.Read();
            var internalScreens = layout.Displays.Where(display => display.Internal).ToArray();
            if (internalScreens.Length != 1 || layout.Displays.Length < 2 ||
                layout.Displays.Select(display => display.SourceIndex).Distinct().Count() != layout.Displays.Length)
                throw new InvalidOperationException("Connect an external screen and use Extend.");
            if (internalScreens[0].Bounds.Location == Point.Empty)
                throw new InvalidOperationException("Set an external monitor as the main display.");

            live = layout;
            laptop = internalScreens[0];
            livePosition = laptop.Bounds.Location;
            externals = layout.Displays.Where(display => !display.Internal).ToArray();
            int width = laptop.Bounds.Width;
            int left = Math.Min(laptop.Bounds.Left, externals.Min(display =>
                display.Bounds.Left + Math.Min(0, (display.Bounds.Width - width) / 2)));
            int right = Math.Max(laptop.Bounds.Left, externals.Max(display =>
                display.Bounds.Right - width + Math.Max(0, (width - display.Bounds.Width) / 2)));
            preview.SetLayout(laptop.Bounds, externals, left, right);
            slider.Enabled = left != right;
            slider.Reset(left, right, laptop.Bounds.Left);
            UpdatePreview();
        }
        catch (Win32Exception error) { ShowLayoutError(error.Message); }
        catch (InvalidOperationException error) { ShowLayoutError(error.Message); }
    }

    private void ShowLayoutError(string message)
    {
        slider.Enabled = test.Enabled = apply.Enabled = false;
        preview.ShowMessage(message);
        status.Text = "";
    }

    private Point? Candidate()
    {
        int x = slider.Value;
        if (x == laptop.Bounds.Left) return laptop.Bounds.Location;
        var above = externals.Where(display => display.Bounds.Left < x + laptop.Bounds.Width && display.Bounds.Right > x);
        if (!above.Any()) return null;
        return new Point(x, above.Max(display => display.Bounds.Bottom));
    }

    private bool Testing { get { return laptop != null && livePosition != laptop.Bounds.Location; } }

    private void UpdatePreview()
    {
        Point? candidate = Candidate();
        preview.ShowPosition(candidate ?? livePosition, livePosition, Testing);
        test.Enabled = candidate.HasValue && candidate.Value != livePosition;
        apply.Enabled = candidate.HasValue && candidate.Value == livePosition && Testing;
        ((SlideButton)test).Accent = test.Enabled;
        ((SlideButton)apply).Accent = apply.Enabled;
        AcceptButton = test.Enabled ? test : (apply.Enabled ? apply : null);
        status.Text = Testing ? "Test active \u00B7 Esc reverts" :
            (!shortcutRegistered ? "Shortcut unavailable" : "\u2190 / \u2192  Move");
    }

    private void ChangePosition(bool save)
    {
        Point? position = Candidate();
        if (!position.HasValue) return;
        try
        {
            DisplayLayout current = DisplayLayout.Read();
            if (!live.SharesLayout(current))
            {
                if (Testing) RestoreOriginal(current);
                LoadLayout();
                if (slider.Enabled) status.Text = "Display layout refreshed";
                return;
            }
            live = current.SetPosition(current.Displays.Single(laptop.IsSameScreen), position.Value, save);
            livePosition = save ? laptop.Bounds.Location : position.Value;
            if (save)
            {
                Close();
                return;
            }
            UpdatePreview();
            slider.Focus();
        }
        catch (Win32Exception error) { ShowChangeError(error.Message); }
        catch (InvalidOperationException error) { ShowChangeError(error.Message); }
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }
        if (slider.Enabled && (keyData == Keys.Left || keyData == Keys.Right))
        {
            slider.Focus();
            slider.Value += keyData == Keys.Left ? -16 : 16;
            return true;
        }
        if (slider.Enabled && (keyData == Keys.Home || keyData == Keys.End))
        {
            slider.Focus();
            slider.MoveToEdge(keyData == Keys.End);
            return true;
        }
        return base.ProcessCmdKey(ref message, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs args)
    {
        base.OnFormClosing(args);
        if (args.Cancel || !Testing) return;
        try
        {
            RestoreOriginal(DisplayLayout.Read());
        }
        catch (Win32Exception error) { HandleRevertError(args, error.Message); }
        catch (InvalidOperationException error) { HandleRevertError(args, error.Message); }
    }

    private void RestoreOriginal(DisplayLayout current)
    {
        var activeLaptop = current.Displays.SingleOrDefault(laptop.IsSameScreen);
        if (activeLaptop == null || activeLaptop.Bounds.Location != livePosition) return;

        if (!live.SharesLayout(current))
        {
            var otherScreens = current.Displays.Where(screen => screen != activeLaptop).ToArray();
            Rectangle restored = new Rectangle(laptop.Bounds.Location, activeLaptop.Bounds.Size);
            bool fits = !otherScreens.Any(screen => screen.Bounds.IntersectsWith(restored)) &&
                otherScreens.Any(screen => Touches(screen.Bounds, restored));
            if (!fits || activeLaptop.Bounds.Location == Point.Empty)
                throw new InvalidOperationException("The original laptop position no longer fits the connected displays.");
        }
        live = current.SetPosition(activeLaptop, laptop.Bounds.Location, false);
        livePosition = laptop.Bounds.Location;
    }

    private static bool Touches(Rectangle first, Rectangle second)
    {
        return ((first.Bottom == second.Top || second.Bottom == first.Top) && first.Left < second.Right && second.Left < first.Right)
            || ((first.Right == second.Left || second.Right == first.Left) && first.Top < second.Bottom && second.Top < first.Bottom);
    }

    private void HandleRevertError(FormClosingEventArgs args, string message)
    {
        if (args.CloseReason != CloseReason.WindowsShutDown)
        {
            args.Cancel = MessageBox.Show(this, message + "\n\nClose anyway?", "Could not restore the laptop position",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No;
        }
    }

    private void ShowChangeError(string message)
    {
        MessageBox.Show(this, message, "Could not move the laptop screen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    protected override void OnMouseDown(MouseEventArgs args)
    {
        base.OnMouseDown(args);
        if (args.Y < header.Bottom) DragWindow(this, args);
    }

    private void DragWindow(object sender, MouseEventArgs args)
    {
        if (args.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
    }

    protected override void OnSizeChanged(EventArgs args)
    {
        base.OnSizeChanged(args);
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        using (var outline = SlideColors.Rounded(ClientRectangle, ScaledCorner))
            Region = new Region(outline);
    }

    protected override void OnPaint(PaintEventArgs args)
    {
        base.OnPaint(args);
        args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var outline = SlideColors.Rounded(Rectangle.Inflate(ClientRectangle, -1, -1), ScaledCorner))
        using (var edge = new Pen(SlideColors.Edge))
            args.Graphics.DrawPath(edge, outline);
    }

    private float ScaledCorner { get { return Math.Max(6, ClientSize.Width / 53f); } }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
