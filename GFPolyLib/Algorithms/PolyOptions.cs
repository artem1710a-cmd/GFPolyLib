namespace GFPolyLib;

/// <summary>Алгоритм умножения многочленов.</summary>
public enum MulAlgorithm
{
    /// <summary>Выбор по размеру операндов.</summary>
    Auto = 0,
    /// <summary>Школьный (по определению свёртки), O(nm).</summary>
    School = 1,
    /// <summary>Карацубы — Офмана, O(n^1,585).</summary>
    Karatsuba = 2,
}

/// <summary>Алгоритм деления многочленов с остатком.</summary>
public enum DivAlgorithm
{
    /// <summary>Выбор по размеру операндов.</summary>
    Auto = 0,
    /// <summary>Деление «уголком», O((n − m + 1)m).</summary>
    Long = 1,
    /// <summary>Через обращение степенного ряда итерацией Ньютона, O(M(n)).</summary>
    Newton = 2,
}

/// <summary>
/// Параметры выбора алгоритмов. Пороги подобраны экспериментально (раздел 6 пояснительной записки).
/// </summary>
public sealed class PolyOptions
{
    private int _karatsubaThreshold = 32;
    private int _newtonThreshold = 2048;
    private int _parallelThreshold = 1024;

    /// <summary>Алгоритм умножения.</summary>
    public MulAlgorithm Multiplication { get; set; } = MulAlgorithm.Auto;

    /// <summary>Алгоритм деления.</summary>
    public DivAlgorithm Division { get; set; } = DivAlgorithm.Auto;

    /// <summary>Длина операнда n0, начиная с которой применяется метод Карацубы (не менее 2).</summary>
    public int KaratsubaThreshold
    {
        get => _karatsubaThreshold;
        set => _karatsubaThreshold = value >= 2 ? value
            : throw new GfException(GfError.InvalidArgument, "Порог метода Карацубы должен быть не меньше 2.");
    }

    /// <summary>Длина делителя и частного, начиная с которой применяется деление Ньютона.</summary>
    public int NewtonThreshold
    {
        get => _newtonThreshold;
        set => _newtonThreshold = value >= 1 ? value
            : throw new GfException(GfError.InvalidArgument, "Порог деления Ньютона должен быть положительным.");
    }

    /// <summary>Длина операнда, начиная с которой ветви метода Карацубы вычисляются параллельно.</summary>
    public int ParallelThreshold
    {
        get => _parallelThreshold;
        set => _parallelThreshold = value >= 16 ? value
            : throw new GfException(GfError.InvalidArgument, "Порог распараллеливания должен быть не меньше 16.");
    }

    /// <summary>Разрешено ли использовать несколько ядер процессора.</summary>
    public bool AllowParallel { get; set; } = true;

    /// <summary>Какой алгоритм умножения будет применён к операндам длины n и m.</summary>
    public MulAlgorithm ResolveMultiplication(int n, int m)
    {
        if (Multiplication != MulAlgorithm.Auto) return Multiplication;
        return Math.Min(n, m) >= KaratsubaThreshold ? MulAlgorithm.Karatsuba : MulAlgorithm.School;
    }

    /// <summary>Какой алгоритм деления будет применён (n, m — длины делимого и делителя).</summary>
    public DivAlgorithm ResolveDivision(int n, int m)
    {
        if (Division != DivAlgorithm.Auto) return Division;
        int qLen = n - m + 1;
        return qLen >= NewtonThreshold && m >= NewtonThreshold / 2 ? DivAlgorithm.Newton : DivAlgorithm.Long;
    }

    public PolyOptions Clone() => (PolyOptions)MemberwiseClone();
}
