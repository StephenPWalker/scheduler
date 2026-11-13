using System.Drawing.Drawing2D;

namespace Scheduler;

internal static class Palette
{
    public static readonly Color Background = Color.FromArgb(250, 249, 246);
    public static readonly Color Ink = Color.FromArgb(43, 53, 49);
    public static readonly Color Muted = Color.FromArgb(124, 130, 123);
    public static readonly Color Accent = Color.FromArgb(54, 91, 70);
    public static readonly Color Line = Color.FromArgb(228, 230, 222);
    public static readonly (string Name, string Hex)[] Colours =
    [
        ("Sage", "#E3EDDF"), ("Sky", "#DFEAF5"), ("Lavender", "#EBE3F4"),
        ("Peach", "#F7E3D5"), ("Rose", "#F4DFE4"), ("Butter", "#F5EDCF")
    ];

    public static GraphicsPath Rounded(Rectangle rectangle, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height));
        if (diameter <= 0) return path;

        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal enum ButtonSymbol { None, Plus, Check, Delete }

internal sealed class PillButton : Button
{
    private bool hovered;
    private bool pressed;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color Fill { get; set; } = Palette.Accent;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color TextColour { get; set; } = Color.White;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public ButtonSymbol Symbol { get; set; }

    public PillButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Palette.Background;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.SupportsTransparentBackColor, false);
    }

    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnMouseCaptureChanged(EventArgs e) { pressed = false; Invalidate(); base.OnMouseCaptureChanged(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) pressed = true; Invalidate(); base.OnKeyDown(e); }
    protected override void OnKeyUp(KeyEventArgs e) { pressed = false; Invalidate(); base.OnKeyUp(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        // Paint every pixel, including corners. Native transparent button painting
        // can leave black rectangles around the custom rounded shape on screen.
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = DeviceDpi / 96f;
        var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
        using var path = Palette.Rounded(bounds, Height / 2);
        var colour = !Enabled ? Color.FromArgb(232, 235, 228) : pressed ? ControlPaint.Dark(Fill, 0.16f) : hovered ? ControlPaint.Dark(Fill, 0.07f) : Fill;
        using var brush = new SolidBrush(colour);
        e.Graphics.FillPath(brush, path);
        if (FlatAppearance.BorderSize > 0)
        {
            using var outline = new Pen(FlatAppearance.BorderColor, scale * FlatAppearance.BorderSize);
            e.Graphics.DrawPath(outline, path);
        }

        var ink = Enabled ? TextColour : Palette.Muted;
        var textBounds = bounds;
        if (Symbol != ButtonSymbol.None)
        {
            var iconSize = 13 * scale;
            var gap = 7 * scale;
            var textWidth = string.IsNullOrEmpty(Text) ? 0 : TextRenderer.MeasureText(e.Graphics, Text, Font, Size.Empty, TextFormatFlags.NoPadding).Width;
            var totalWidth = iconSize + (textWidth == 0 ? 0 : gap + textWidth);
            var left = (Width - totalWidth) / 2f;
            DrawSymbol(e.Graphics, new RectangleF(left, (Height - iconSize) / 2f, iconSize, iconSize), ink, scale);
            textBounds.X = (int)Math.Round(left + iconSize + gap);
            textBounds.Width = textWidth;
        }

        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            
        if (Focused && ShowFocusCues)
        {
            var inset = (int)Math.Round(4 * scale);
            using var focus = Palette.Rounded(Rectangle.Inflate(bounds, -inset, -inset), Height / 2);
            using var pen = new Pen(ink, scale) { DashStyle = DashStyle.Dot };
            e.Graphics.DrawPath(pen, focus);
        }
    }

    private void DrawSymbol(Graphics graphics, RectangleF area, Color colour, float scale)
    {
        using var pen = new Pen(colour, 1.5f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        PointF At(float x, float y) => new(area.Left + x * area.Width, area.Top + y * area.Height);
        if (Symbol == ButtonSymbol.Plus)
        {
            graphics.DrawLine(pen, At(.15f, .5f), At(.85f, .5f));
            graphics.DrawLine(pen, At(.5f, .15f), At(.5f, .85f));
        }
        else if (Symbol == ButtonSymbol.Check)
        {
            graphics.DrawLines(pen, [At(.1f, .5f), At(.4f, .8f), At(.9f, .2f)]);
        }
        else if (Symbol == ButtonSymbol.Delete)
        {
            graphics.DrawLine(pen, At(.15f, .25f), At(.85f, .25f));
            graphics.DrawLines(pen, [At(.35f, .25f), At(.35f, .1f), At(.65f, .1f), At(.65f, .25f)]);
            graphics.DrawLines(pen, [At(.25f, .35f), At(.3f, .9f), At(.7f, .9f), At(.75f, .35f)]);
            graphics.DrawLine(pen, At(.43f, .43f), At(.43f, .73f));
            graphics.DrawLine(pen, At(.57f, .43f), At(.57f, .73f));
        }
    }
}
