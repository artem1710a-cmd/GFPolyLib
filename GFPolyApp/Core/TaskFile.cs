using System.Text;

namespace GFPolyApp.Core;

/// <summary>
/// Исходные данные задачи и их файловое представление (*.gfp).
/// Формат — текст UTF-8, по одному параметру в строке «ключ = значение»:
/// <code>
/// # комментарий
/// p = 7
/// k = 1
/// modulus =
/// A = 3x^4 + 5x^3 + x + 6
/// B = 2 4 1
/// c = 2
/// </code>
/// Обязателен только параметр p; k по умолчанию равно 1.
/// </summary>
public sealed class TaskData
{
    public string P { get; set; } = "";
    public int K { get; set; } = 1;
    public string Modulus { get; set; } = "";
    public string A { get; set; } = "";
    public string B { get; set; } = "";
    public string C { get; set; } = "";
}

public sealed class TaskFileException : Exception
{
    public TaskFileException(string message) : base(message) { }
}

public static class TaskFile
{
    public const string Filter = "Исходные данные GFPolyLib (*.gfp)|*.gfp|Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";
    private const long MaxFileSize = 512L * 1024 * 1024;

    public static TaskData Load(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new TaskFileException($"Файл «{path}» не найден.");
        if (info.Length > MaxFileSize) throw new TaskFileException("Файл слишком велик (более 512 МБ).");

        var data = new TaskData();
        bool hasP = false;
        int lineNo = 0;
        foreach (string raw in File.ReadLines(path, Encoding.UTF8))
        {
            lineNo++;
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0)
                throw new TaskFileException($"Строка {lineNo}: ожидалась запись вида «ключ = значение».");
            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();
            switch (key.ToLowerInvariant())
            {
                case "p":
                    data.P = value;
                    hasP = value.Length > 0;
                    break;
                case "k":
                    if (!int.TryParse(value, out int k) || k < 1 || k > 256)
                        throw new TaskFileException($"Строка {lineNo}: степень расширения k должна быть целым числом от 1 до 256.");
                    data.K = k;
                    break;
                case "modulus":
                case "f":
                    data.Modulus = value;
                    break;
                case "a": data.A = value; break;
                case "b": data.B = value; break;
                case "c": data.C = value; break;
                default:
                    throw new TaskFileException($"Строка {lineNo}: неизвестный параметр «{key}». Допустимы p, k, modulus, A, B, c.");
            }
        }
        if (!hasP) throw new TaskFileException("В файле не задана характеристика поля (строка «p = …»).");
        return data;
    }

    public static void Save(string path, TaskData data)
    {
        using var w = new StreamWriter(path, false, new UTF8Encoding(false));
        w.WriteLine("# Исходные данные GFPolyLib");
        w.WriteLine($"# Сохранено {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        w.WriteLine($"p = {data.P}");
        w.WriteLine($"k = {data.K}");
        w.WriteLine($"modulus = {data.Modulus}");
        w.WriteLine($"A = {data.A}");
        w.WriteLine($"B = {data.B}");
        w.WriteLine($"c = {data.C}");
    }
}
