using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

internal static class SlideColors
{
    internal static readonly Color Window = Color.FromArgb(32, 32, 32);
    internal static readonly Color Surface = Color.FromArgb(40, 40, 40);
    internal static readonly Color Edge = Color.FromArgb(70, 70, 70);
    internal static readonly Color Text = Color.FromArgb(246, 246, 246);
    internal static readonly Color Muted = Color.FromArgb(180, 180, 180);
    internal static readonly Color Accent = Color.FromArgb(96, 205, 255);

    internal static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) return path;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static void Shape(Graphics graphics, RectangleF bounds, float radius, Color fill, Color edge)
    {
        if (bounds.Width < 1 || bounds.Height < 1) return;
        using (var path = Rounded(bounds, radius))
        using (var brush = new SolidBrush(fill))
        {
            graphics.FillPath(brush, path);
            if (edge.A > 0)
                using (var pen = new Pen(edge)) graphics.DrawPath(pen, path);
        }
    }
}

internal sealed class SlideButton : Button
{
    private bool accent;
    private bool hover;
    internal bool Quiet;

    internal SlideButton(string text)
    {
        Text = text;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }

    internal bool Accent
    {
        get { return accent; }
        set { accent = value; Invalidate(); }
    }

    protected override void OnMouseEnter(EventArgs args) { base.OnMouseEnter(args); hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs args) { base.OnMouseLeave(args); hover = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs args)
    {
        var graphics = args.Graphics;
        graphics.Clear(SlideColors.Window);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color fill = Quiet ? SlideColors.Window : SlideColors.Surface;
        Color edge = Quiet ? Color.Transparent : SlideColors.Edge;
        Color text = Enabled ? SlideColors.Text : Color.FromArgb(126, 126, 126);
        if (Enabled && Accent)
        {
            fill = hover ? Color.FromArgb(136, 219, 255) : SlideColors.Accent;
            edge = Color.Transparent;
            text = Color.FromArgb(15, 34, 43);
        }
        else if (Enabled && hover)
        {
            fill = Color.FromArgb(58, 58, 58);
        }
        if (Focused && ShowFocusCues) edge = SlideColors.Text;
        SlideColors.Shape(graphics, Rectangle.Inflate(ClientRectangle, -1, -1), 5 * graphics.DpiX / 96, fill, edge);
        TextRenderer.DrawText(graphics, Text, Font, ClientRectangle, text, TextFormatFlags.SingleLine |
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

internal sealed class SlideBar : Control
{
    private int minimum, maximum, value;
    private bool dragging;
    internal event EventHandler ValueChanged;

    internal SlideBar()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.OptimizedDoubleBuffer, true);
        AccessibleRole = AccessibleRole.Slider;
        TabStop = true;
        Cursor = Cursors.Hand;
        BackColor = SlideColors.Window;
    }

    internal int Value
    {
        get { return value; }
        set
        {
            int next = Math.Max(minimum, Math.Min(maximum, value));
            if (next == this.value) return;
            this.value = next;
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }
    }

    internal void Reset(int minimum, int maximum, int value)
    {
        this.minimum = minimum;
        this.maximum = maximum;
        this.value = value;
        Invalidate();
    }

    internal void MoveToEdge(bool right) { Value = right ? maximum : minimum; }

    private float MarginX { get { return Height * 0.42f; } }
    private float Fraction { get { return maximum == minimum ? 0 : (value - minimum) / (float)(maximum - minimum); } }

    protected override void OnPaint(PaintEventArgs args)
    {
        var graphics = args.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = graphics.DpiX / 96;
        float x = MarginX + (Width - MarginX * 2) * Fraction;
        float y = Height / 2f;
        SlideColors.Shape(graphics, new RectangleF(MarginX, y - 2 * scale, Width - MarginX * 2, 4 * scale),
            2 * scale, Color.FromArgb(91, 91, 91), Color.Transparent);
        if (!Enabled) return;
        SlideColors.Shape(graphics, new RectangleF(MarginX, y - 2 * scale, x - MarginX, 4 * scale),
            2 * scale, SlideColors.Accent, Color.Transparent);
        float radius = 7 * scale;
        using (var brush = new SolidBrush(Focused ? SlideColors.Accent : SlideColors.Muted))
            graphics.FillEllipse(brush, x - radius, y - radius, radius * 2, radius * 2);
        using (var pen = new Pen(SlideColors.Surface, scale * 1.5f))
            graphics.DrawEllipse(pen, x - radius, y - radius, radius * 2, radius * 2);
    }

    private void MoveTo(int x)
    {
        float range = Math.Max(1, Width - MarginX * 2);
        Value = minimum + (int)Math.Round((x - MarginX) * (maximum - minimum) / range);
    }

    protected override void OnMouseDown(MouseEventArgs args)
    {
        base.OnMouseDown(args);
        if (args.Button != MouseButtons.Left) return;
        Focus();
        Capture = dragging = true;
        MoveTo(args.X);
    }

    protected override void OnMouseMove(MouseEventArgs args)
    {
        base.OnMouseMove(args);
        if (dragging) MoveTo(args.X);
    }

    protected override void OnMouseUp(MouseEventArgs args)
    {
        base.OnMouseUp(args);
        if (args.Button != MouseButtons.Left) return;
        dragging = Capture = false;
    }

    protected override void OnMouseCaptureChanged(EventArgs args)
    {
        base.OnMouseCaptureChanged(args);
        if (!Capture) dragging = false;
    }

    protected override void OnGotFocus(EventArgs args) { base.OnGotFocus(args); Invalidate(); }
    protected override void OnLostFocus(EventArgs args) { base.OnLostFocus(args); Invalidate(); }

    protected override AccessibleObject CreateAccessibilityInstance()
    {
        return new SlideBarAccessibility(this);
    }

    private sealed class SlideBarAccessibility : ControlAccessibleObject
    {
        private readonly SlideBar slider;
        internal SlideBarAccessibility(SlideBar slider) : base(slider) { this.slider = slider; }

        public override string Value
        {
            get { return ((int)Math.Round(slider.Fraction * 100)).ToString() + "%"; }
            set
            {
                int percent;
                if (value != null && int.TryParse(value.TrimEnd('%'), out percent))
                    slider.Value = slider.minimum + (int)Math.Round((slider.maximum - slider.minimum) * (percent / 100.0));
            }
        }
    }
}

internal sealed class DisplayPreview : Control
{
    private DisplayLayout.Display[] screens;
    private Rectangle original, bounds;
    private Point draft, live;
    private bool testing;
    private string message;

    internal DisplayPreview() { DoubleBuffered = true; BackColor = SlideColors.Window; }

    internal void SetLayout(Rectangle laptop, DisplayLayout.Display[] externalScreens, int left, int right)
    {
        original = laptop;
        screens = externalScreens;
        int lowest = screens.Max(screen => screen.Bounds.Bottom);
        bounds = Rectangle.FromLTRB(Math.Min(left, screens.Min(screen => screen.Bounds.Left)),
            Math.Min(laptop.Top, screens.Min(screen => screen.Bounds.Top)),
            Math.Max(right + laptop.Width, screens.Max(screen => screen.Bounds.Right)),
            Math.Max(laptop.Bottom, lowest + laptop.Height));
        message = null;
    }

    internal void ShowPosition(Point candidate, Point actual, bool isTesting)
    {
        draft = candidate;
        live = actual;
        testing = isTesting;
        Invalidate();
    }

    internal void ShowMessage(string value) { message = value; Invalidate(); }

    protected override void OnPaint(PaintEventArgs args)
    {
        base.OnPaint(args);
        var graphics = args.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float dpi = graphics.DpiX / 96;
        SlideColors.Shape(graphics, Rectangle.Inflate(ClientRectangle, -1, -1), 6 * dpi,
            Color.FromArgb(25, 25, 25), Color.FromArgb(55, 55, 55));
        if (message != null)
        {
            TextRenderer.DrawText(graphics, message, Font, Rectangle.Inflate(ClientRectangle, -12, -12),
                SlideColors.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }
        if (screens == null) return;

        float scaleY = Math.Min((Width - 28 * dpi) / bounds.Width, (Height - 16 * dpi) / bounds.Height);
        float scaleX = Math.Min((Width - 28 * dpi) / bounds.Width, scaleY * 1.25f);
        float left = (Width - bounds.Width * scaleX) / 2;
        float top = (Height - bounds.Height * scaleY) / 2;
        Func<Rectangle, RectangleF> convert = rect => new RectangleF(left + (rect.X - bounds.X) * scaleX,
            top + (rect.Y - bounds.Y) * scaleY, rect.Width * scaleX, rect.Height * scaleY);
        foreach (var screen in screens) DrawMonitor(graphics, convert(screen.Bounds), screen.Name, dpi);

        Point ghost = draft == live ? original.Location : live;
        if (draft != ghost)
        {
            RectangleF outline = RectangleF.Inflate(convert(new Rectangle(ghost, original.Size)), -1, -1);
            using (var path = SlideColors.Rounded(outline, 3 * dpi))
            using (var pen = new Pen(Color.FromArgb(107, 107, 107)) { DashStyle = DashStyle.Dash })
                graphics.DrawPath(pen, path);
        }
        DrawLaptop(graphics, convert(new Rectangle(draft, original.Size)), dpi);
    }

    private void DrawMonitor(Graphics graphics, RectangleF screen, string name, float dpi)
    {
        screen.Inflate(-1, -1);
        SlideColors.Shape(graphics, screen, 3 * dpi, Color.FromArgb(51, 52, 54), Color.FromArgb(117, 118, 120));
        RectangleF display = RectangleF.Inflate(screen, -3 * dpi, -3 * dpi);
        DrawScreenSurface(graphics, display, Color.FromArgb(47, 53, 64), Color.FromArgb(36, 39, 44), dpi);
        DrawName(graphics, name, display, SlideColors.Text);
    }

    private void DrawLaptop(Graphics graphics, RectangleF laptop, float dpi)
    {
        laptop.Inflate(-1, -1);
        bool active = testing && draft == live;
        Color edge = active ? SlideColors.Accent : Color.FromArgb(74, 163, 213);
        var lid = new RectangleF(laptop.X + 2 * dpi, laptop.Y, laptop.Width - 4 * dpi, laptop.Height - 4 * dpi);
        SlideColors.Shape(graphics, lid, 4 * dpi, Color.FromArgb(16, 42, 59), edge);
        RectangleF display = RectangleF.Inflate(lid, -3 * dpi, -3 * dpi);
        DrawScreenSurface(graphics, display, Color.FromArgb(34, 90, 126), Color.FromArgb(20, 54, 77), dpi);
        DrawName(graphics, "Laptop", display, Color.FromArgb(212, 242, 255));
        var keyboard = new RectangleF(laptop.X, lid.Bottom, laptop.Width, 4 * dpi);
        SlideColors.Shape(graphics, keyboard, 2 * dpi, active ? SlideColors.Accent : Color.FromArgb(62, 131, 171), Color.Transparent);
    }

    private static void DrawScreenSurface(Graphics graphics, RectangleF display, Color start, Color end, float dpi)
    {
        if (display.Width < 1 || display.Height < 1) return;
        using (var surface = SlideColors.Rounded(display, 2 * dpi))
        using (var fill = new LinearGradientBrush(display, start, end, 55f))
            graphics.FillPath(fill, surface);
    }

    private void DrawName(Graphics graphics, string name, RectangleF display, Color text)
    {
        const TextFormatFlags style = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        if (TextRenderer.MeasureText(graphics, name, Font, Size.Empty, style).Width > display.Width)
            name = name.Split(' ')[0];
        using (var small = new Font(Font.FontFamily, Font.Size * 0.89f, FontStyle.Bold))
            TextRenderer.DrawText(graphics, name, small, Rectangle.Round(display), text,
                style | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
