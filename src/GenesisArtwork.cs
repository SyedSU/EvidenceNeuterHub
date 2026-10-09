namespace EvidenceHub;
// Original lightweight vector/pixel illustration, not a game screenshot.
public sealed class GenesisArtwork : Control
{
    public GenesisArtwork()
    {
        Height = 144; Width = 620; DoubleBuffered = true;
        AccessibleName = "Illustration of a quiet companion workshop";
        AccessibleRole = AccessibleRole.Graphic;
        Margin = new Padding(0, 0, 0, 24);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Color.FromArgb(27, 55, 44));
        void Block(Color c, int x, int y, int w, int h)
        { using var brush = new SolidBrush(c); g.FillRectangle(brush, x, y, w, h); }
        var ivory = Color.FromArgb(231, 218, 180);
        var brass = Color.FromArgb(169, 140, 81);
        var green = Color.FromArgb(77, 103, 71);
        int left = Math.Max(240, Width - 310);
        Block(green, left, 28, 70, 76); Block(ivory, left+6, 34, 58, 64);
        Block(green, left+32, 34, 6, 64); Block(green, left+6, 62, 58, 6);
        Block(brass, left+92, 86, 120, 8); Block(brass, left+100, 94, 8, 36); Block(brass, left+194, 94, 8, 36);
        Block(ivory, left+114, 69, 27, 17); Block(green, left+141, 73, 25, 13);
        Block(green, left+230, 45, 32, 20); Block(green, left+224, 63, 44, 15); Block(brass, left+241, 78, 9, 35);
        Block(ivory, left+43, 108, 20, 19); Block(brass, left+47, 98, 12, 10);
        Block(Color.FromArgb(20, 37, 30), left+47, 111, 3, 3); Block(Color.FromArgb(20, 37, 30), left+57, 111, 3, 3);
        using var title = new Font("Segoe UI", 18, FontStyle.Bold);
        using var detail = new Font("Segoe UI", 10);
        using var text = new SolidBrush(ivory);
        g.DrawString("A little world.\nRoom to grow.", title, text, 22, 25);
        g.DrawString("GENESIS / CONCEPT ART", detail, text, 24, 108);
    }
}
