using System.Diagnostics;
using System.Globalization;
using System.Text;
using GFPolyLib;

// Консольная утилита GFPolyLib: пакетные вычисления над файлом исходных данных и замеры производительности.
// Пример вызова из другой программы показывает, что библиотека не зависит от графического интерфейса.

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

try
{
    return args.Length == 0 ? Usage() : args[0] switch
    {
        "calc" => Calc(args.Skip(1).ToArray()),
        "bench" => Bench(args.Skip(1).ToArray()),
        _ => Usage(),
    };
}
catch (GfException ex)
{
    Console.Error.WriteLine($"Ошибка {(int)ex.Code} ({ex.Code}): {ex.Message}");
    return 2;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
{
    Console.Error.WriteLine("Ошибка: " + ex.Message);
    return 3;
}

static int Usage()
{
    Console.WriteLine("""
        gfpoly calc <файл.gfp> [--out <результат.txt>]
            выполнить все операции над A, B и c из файла исходных данных
        gfpoly bench [--p <p>] [--k <k>] [--op mul|div] [--sizes 64,128,...] [--csv <файл>] [--threads 1|all]
            замер времени алгоритмов умножения или деления
        """);
    return 1;
}

static string? Opt(string[] a, string name, string? def = null)
{
    int i = Array.IndexOf(a, name);
    return i >= 0 && i + 1 < a.Length ? a[i + 1] : def;
}

static int Calc(string[] a)
{
    if (a.Length == 0) return Usage();
    var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (string raw in File.ReadLines(a[0], Encoding.UTF8))
    {
        string line = raw.Trim();
        if (line.Length == 0 || line.StartsWith('#')) continue;
        int eq = line.IndexOf('=');
        if (eq <= 0) throw new FormatException($"Строка «{line}»: ожидалась запись «ключ = значение».");
        data[line[..eq].Trim()] = line[(eq + 1)..].Trim();
    }
    var field = GaloisField.Create(data.GetValueOrDefault("p", ""), int.Parse(data.GetValueOrDefault("k", "1")),
        data.GetValueOrDefault("modulus") is { Length: > 0 } m ? m : null);
    var A = field.Parse(data["A"]);
    var B = field.Parse(data["B"]);
    var sb = new StringBuilder();
    sb.AppendLine($"Поле: {field.Name}" + (field.ExtensionDegree > 1 ? $", f(x) = {field.Modulus}" : ""));
    sb.AppendLine($"A = {A}");
    sb.AppendLine($"B = {B}");
    sb.AppendLine($"A + B = {A + B}");
    sb.AppendLine($"A - B = {A - B}");
    sb.AppendLine($"A * B = {A * B}");
    var (q, r) = A.DivRem(B);
    sb.AppendLine($"Q = {q}");
    sb.AppendLine($"R = {r}");
    if (data.TryGetValue("c", out string? c) && c.Length > 0) sb.AppendLine($"A({c}) = {A.Evaluate(c)}");
    string? outFile = Opt(a, "--out");
    if (outFile is null) Console.Write(sb);
    else
    {
        File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(true));
        Console.WriteLine($"Результаты записаны в {outFile}");
    }
    return 0;
}

static int Bench(string[] a)
{
    var field = GaloisField.Create(Opt(a, "--p", "998244353")!, int.Parse(Opt(a, "--k", "1")!));
    string op = Opt(a, "--op", "mul")!;
    int[] sizes = Opt(a, "--sizes", "64,128,256,512,1024,2048,4096,8192")!.Split(',').Select(int.Parse).ToArray();
    field.Options.AllowParallel = Opt(a, "--threads", "all") != "1";
    if (Opt(a, "--kt") is string kt) field.Options.KaratsubaThreshold = int.Parse(kt);
    string? csv = Opt(a, "--csv");
    var lines = new List<string> { "field;op;algorithm;n;ms" };
    Console.WriteLine($"Поле {field.Name} ({field.Implementation}), операция {op}, потоки: {(field.Options.AllowParallel ? "все" : "1")}");
    foreach (int n in sizes)
    {
        var A = field.Random(op == "mul" ? n : 2 * n, n);
        var B = field.Random(n, n + 7);
        var algs = op == "mul"
            ? new (string, Action)[]
            {
                ("school", () => { field.Options.Multiplication = MulAlgorithm.School; _ = A * B; }),
                ("karatsuba", () => { field.Options.Multiplication = MulAlgorithm.Karatsuba; _ = A * B; }),
            }
            : new (string, Action)[]
            {
                ("long", () => { field.Options.Division = DivAlgorithm.Long; _ = A.DivRem(B); }),
                ("newton", () => { field.Options.Multiplication = MulAlgorithm.Auto; field.Options.Division = DivAlgorithm.Newton; _ = A.DivRem(B); }),
            };
        var row = new StringBuilder($"n = {n,7}:");
        foreach (var (name, action) in algs)
        {
            if (name == "school" && (long)n * n > 3_000_000_000L / Math.Max(1, field.Characteristic.GetBitLength() / 16))
            {
                row.Append($"  {name} = —");
                continue;
            }
            action();
            double best = double.MaxValue;
            int reps = n <= 2048 ? 5 : 3;
            for (int i = 0; i < reps; i++)
            {
                var sw = Stopwatch.StartNew();
                action();
                best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
            }
            row.Append($"  {name} = {best,10:F3} мс");
            lines.Add($"{field.Name};{op};{name};{n};{best:F4}");
        }
        Console.WriteLine(row);
    }
    if (csv is not null) File.WriteAllLines(csv, lines, new UTF8Encoding(true));
    return 0;
}
