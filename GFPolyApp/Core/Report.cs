using System.Drawing.Printing;
using System.Text;

namespace GFPolyApp.Core;

/// <summary>Результат одной операции для отображения, сохранения и печати.</summary>
public sealed class OperationResult
{
    public required string Title { get; init; }
    public required string FieldDescription { get; init; }
    public required IReadOnlyList<(string Name, string Value)> Inputs { get; init; }
    public required IReadOnlyList<(string Name, string Value)> Outputs { get; init; }
    public required string Algorithm { get; init; }
    public required double ElapsedMs { get; init; }
    public DateTime Time { get; init; } = DateTime.Now;
}

public static class ReportBuilder
{
    public static string Build(IEnumerable<OperationResult> results, int maxValueLength = int.MaxValue)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ОТЧЁТ О ВЫЧИСЛЕНИЯХ GFPolyLib");
        sb.AppendLine($"Версия библиотеки: {GFPolyLib.GaloisField.Version}");
        sb.AppendLine($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        sb.AppendLine(new string('=', 70));
        int n = 0;
        foreach (var r in results)
        {
            n++;
            sb.AppendLine();
            sb.AppendLine($"{n}. {r.Title}   ({r.Time:dd.MM.yyyy HH:mm:ss})");
            sb.AppendLine($"   Поле: {r.FieldDescription}");
            foreach (var (name, value) in r.Inputs) sb.AppendLine($"   {name} = {Cut(value, maxValueLength)}");
            sb.AppendLine("   Результат:");
            foreach (var (name, value) in r.Outputs) sb.AppendLine($"   {name} = {Cut(value, maxValueLength)}");
            sb.AppendLine($"   Алгоритм: {r.Algorithm}; время: {r.ElapsedMs:F3} мс");
        }
        if (n == 0) sb.AppendLine("Операции ещё не выполнялись.");
        return sb.ToString();
    }

    public static string Cut(string s, int max) =>
        s.Length <= max ? s : s[..max] + $" … [показано {max} из {s.Length} символов]";
}

/// <summary>Постраничная печать текстового отчёта с переносом длинных строк.</summary>
public sealed class TextPrinter
{
    private readonly string[] _lines;
    private int _line;
    private int _page;
    private readonly Font _font = new("Consolas", 9.5f);

    public PrintDocument Document { get; }

    public TextPrinter(string text, string title)
    {
        _lines = text.Replace("\r\n", "\n").Split('\n');
        Document = new PrintDocument { DocumentName = title };
        Document.BeginPrint += (_, _) => { _line = 0; _page = 0; };
        Document.PrintPage += OnPrintPage;
    }

    /// <summary>Оценка числа страниц (для предупреждения пользователя перед печатью).</summary>
    public static int EstimatePages(string text, int charsPerLine = 95, int linesPerPage = 64)
    {
        long lines = 0;
        foreach (var l in text.Split('\n')) lines += Math.Max(1, (l.Length + charsPerLine - 1) / charsPerLine);
        return (int)Math.Min(int.MaxValue, (lines + linesPerPage - 1) / linesPerPage);
    }

    private List<string>? _wrapped;

    private void OnPrintPage(object? sender, PrintPageEventArgs e)
    {
        var g = e.Graphics!;
        var area = e.MarginBounds;
        if (_line == 0 && _page == 0) _wrapped = Wrap(g, area.Width); // перенос строк по ширине области печати
        var lines = _wrapped!;
        float lineHeight = _font.GetHeight(g);
        float y = area.Top;
        _page++;
        using var footerFont = new Font("Segoe UI", 8f);
        g.DrawString($"GFPolyLib — {Document.DocumentName} — стр. {_page}", footerFont, Brushes.Gray, area.Left,
            area.Bottom + 10);
        while (_line < lines.Count && y + lineHeight <= area.Bottom)
        {
            g.DrawString(lines[_line], _font, Brushes.Black, area.Left, y);
            y += lineHeight;
            _line++;
        }
        e.HasMorePages = _line < lines.Count;
    }

    private List<string> Wrap(Graphics g, float width)
    {
        float charWidth = g.MeasureString("0000000000", _font).Width / 10f;
        int perLine = Math.Max(10, (int)(width / charWidth) - 1);
        var result = new List<string>();
        foreach (var line in _lines)
        {
            if (line.Length == 0) result.Add(string.Empty);
            for (int i = 0; i < line.Length; i += perLine)
                result.Add(line.Substring(i, Math.Min(perLine, line.Length - i)));
        }
        return result;
    }
}
