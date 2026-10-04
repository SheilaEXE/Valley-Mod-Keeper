using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace TradutorModsStardew;

internal sealed class GlowButton : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get; init; } = Color.FromArgb(161, 232, 74);
    private bool hovering;

    public GlowButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.FromArgb(30, 91, 120);
        ForeColor = Color.White;
        Font = new Font("Segoe UI Semibold", 9.5f);
        Cursor = Cursors.Hand;
        Padding = new Padding(15, 6, 15, 6);
        AutoSize = true;
        MinimumSize = new Size(0, 38);
        UseVisualStyleBackColor = false;
    }

    protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Color.FromArgb(10, 31, 53));
        var bounds = ClientRectangle;
        bounds.Inflate(-3, -3);
        if (hovering && Enabled)
        {
            using var glow = new Pen(Color.FromArgb(120, AccentColor), 5);
            using var glowPath = Rounded(bounds, 10);
            e.Graphics.DrawPath(glow, glowPath);
        }
        using var path = Rounded(bounds, 8);
        var fill = !Enabled ? Color.FromArgb(47, 69, 86) : hovering ? AccentColor : BackColor;
        using var brush = new SolidBrush(fill);
        e.Graphics.FillPath(brush, path);
        using var border = new Pen(hovering && Enabled ? Color.White : Color.FromArgb(77, 174, 202), 1);
        e.Graphics.DrawPath(border, path);
        var textColor = hovering && Enabled ? Color.FromArgb(12, 41, 61) : ForeColor;
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
