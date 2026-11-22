using System.Drawing.Drawing2D;

namespace GFPolyApp.UI;

/// <summary>Простой линейный график «размер — время» для вкладки производительности.</summary>
public sealed class SimpleChart : Control
{
    private static readonly Color[] Palette =
    {
        Color.FromArgb(42, 120, 200), Color.FromArgb(220, 110, 40), Color.FromArgb(60, 160, 90), Color.FromArgb(170, 60, 160),
    };

    private readonly List<(string Name, List<PointF> Points)> _series = new();

    public string XTitle { get; set; } = "Степень многочлена n";
    public string YTitle { get; set; } = "Время, мс";

    public SimpleChart()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9f);
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public void Clear()
    {
        _series.Clear();
        Invalidate();
    }

    public void AddPoint(string series, float x, float y)
    {
        var s = _series.FirstOrDefault(t => t.Name == series);
        if (s.Points is null)
        {
            s = (series, new List<PointF>());
            _series.Add(s);
        }
        s.Points.Add(new PointF(x, y));
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var area = new Rectangle(70, 20, Math.Max(10, Width - 100), Math.Max(10, Height - 75));
        using var axisPen = new Pen(Color.FromArgb(90, 90, 90));
        using var gridPen = new Pen(Color.FromArgb(225, 225, 225));
        using var textBrush = new SolidBrush(Color.FromArgb(50, 50, 50));

        var all = _series.SelectMany(s => s.Points).ToList();
        if (all.Count == 0)
        {
            g.DrawString("Запустите замер, чтобы построить график", Font, Brushes.Gray, area.Left + 10, area.Top + 10);
            g.DrawRectangle(axisPen, area);
            return;
        }
        float maxX = Math.Max(1, all.Max(p => p.X));
        float maxY = Math.Max(0.001f, all.Max(p => p.Y)) * 1.08f;

        for (int i = 0; i <= 5; i++)
        {
            float y = area.Bottom - area.Height * i / 5f;
            g.DrawLine(gridPen, area.Left, y, area.Right, y);
            string label = (maxY * i / 5f).ToString(maxY < 10 ? "0.00" : "0");
            var size = g.MeasureString(label, Font);
            g.DrawString(label, Font, textBrush, area.Left - size.Width - 4, y - size.Height / 2);

            float x = area.Left + area.Width * i / 5f;
            g.DrawLine(gridPen, x, area.Top, x, area.Bottom);
            string xl = (maxX * i / 5f).ToString("0");
            var xs = g.MeasureString(xl, Font);
            g.DrawString(xl, Font, textBrush, x - xs.Width / 2, area.Bottom + 4);
        }
        g.DrawRectangle(axisPen, area);
        var xt = g.MeasureString(XTitle, Font);
        g.DrawString(XTitle, Font, textBrush, area.Left + (area.Width - xt.Width) / 2, area.Bottom + 22);
        g.DrawString(YTitle, Font, textBrush, 4, 2);

        int idx = 0;
        float legendY = area.Top + 8;
        foreach (var (name, points) in _series)
        {
            var color = Palette[idx++ % Palette.Length];
            using var pen = new Pen(color, 2.2f);
            using var brush = new SolidBrush(color);
            var pts = points.OrderBy(p => p.X)
                .Select(p => new PointF(area.Left + area.Width * p.X / maxX, area.Bottom - area.Height * p.Y / maxY))
                .ToArray();
            if (pts.Length > 1) g.DrawLines(pen, pts);
            foreach (var p in pts) g.FillEllipse(brush, p.X - 3, p.Y - 3, 6, 6);
            g.FillRectangle(brush, area.Left + 12, legendY + 5, 18, 4);
            g.DrawString(name, Font, textBrush, area.Left + 36, legendY - 2);
            legendY += 20;
        }
    }
}
