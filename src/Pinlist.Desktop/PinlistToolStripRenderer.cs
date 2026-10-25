using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Pinlist.Desktop;

internal sealed class PinlistToolStripRenderer : ToolStripProfessionalRenderer
{
    private static readonly Color Surface = Color.FromArgb(27, 29, 34);
    private static readonly Color Hover = Color.FromArgb(39, 42, 49);
    private static readonly Color Border = Color.FromArgb(58, 61, 70);
    private static readonly Color Text = Color.FromArgb(221, 225, 233);
    private static readonly Color MutedText = Color.FromArgb(112, 118, 130);
    private static readonly Color Accent = Color.FromArgb(77, 124, 254);

    public PinlistToolStripRenderer() : base(new PinlistToolStripColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(Surface);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        var bounds = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        using var pen = new Pen(Border);
        e.Graphics.DrawRectangle(pen, bounds);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected) return;
        var bounds = new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2);
        using var brush = new SolidBrush(Hover);
        using var path = RoundedRectangle(bounds, 6);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = !e.Item.Enabled ? MutedText : e.Item.Selected ? Color.White : Text;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item.Enabled ? Color.FromArgb(174, 179, 190) : MutedText;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var y = e.Item.Height / 2;
        using var pen = new Pen(Color.FromArgb(48, 51, 59));
        e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(Surface);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        const int size = 14;
        var bounds = new Rectangle(
            e.ImageRectangle.Left + (e.ImageRectangle.Width - size) / 2,
            e.ImageRectangle.Top + (e.ImageRectangle.Height - size) / 2,
            size,
            size);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var background = new SolidBrush(Accent);
        using var outline = RoundedRectangle(bounds, 4);
        e.Graphics.FillPath(background, outline);
        using var checkPen = new Pen(Color.White, 1.7f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        e.Graphics.DrawLines(checkPen,
        [
            new Point(bounds.Left + 3, bounds.Top + 7),
            new Point(bounds.Left + 6, bounds.Top + 10),
            new Point(bounds.Left + 11, bounds.Top + 4)
        ]);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class PinlistToolStripColorTable : ProfessionalColorTable
{
    private static readonly Color Surface = Color.FromArgb(27, 29, 34);
    private static readonly Color Hover = Color.FromArgb(39, 42, 49);
    private static readonly Color Border = Color.FromArgb(58, 61, 70);

    public override Color ToolStripDropDownBackground => Surface;
    public override Color ImageMarginGradientBegin => Surface;
    public override Color ImageMarginGradientMiddle => Surface;
    public override Color ImageMarginGradientEnd => Surface;
    public override Color MenuBorder => Border;
    public override Color MenuItemBorder => Hover;
    public override Color MenuItemSelected => Hover;
    public override Color MenuItemSelectedGradientBegin => Hover;
    public override Color MenuItemSelectedGradientEnd => Hover;
    public override Color MenuItemPressedGradientBegin => Hover;
    public override Color MenuItemPressedGradientEnd => Hover;
    public override Color SeparatorDark => Color.FromArgb(48, 51, 59);
    public override Color SeparatorLight => Color.FromArgb(48, 51, 59);
    public override Color CheckBackground => Color.FromArgb(77, 124, 254);
    public override Color CheckSelectedBackground => Color.FromArgb(77, 124, 254);
    public override Color CheckPressedBackground => Color.FromArgb(77, 124, 254);
}
