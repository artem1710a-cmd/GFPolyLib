using System.Globalization;
using System.Numerics;
using GFPolyLib.Algorithms;
using GFPolyLib.Fields;

namespace GFPolyLib;

/// <summary>
/// Конечное поле GF(q), q = p^k. Точка входа библиотеки: создаёт поле, разбирает и порождает многочлены.
/// Конкретное представление элементов (64-битные вычеты, длинные целые, расширение) выбирается автоматически.
/// </summary>
public abstract class GaloisField
{
    /// <summary>Версия библиотеки.</summary>
    public static string Version => "2.0.0";

    /// <summary>Наибольшая допустимая степень расширения k.</summary>
    public const int MaxExtensionDegree = 256;

    /// <summary>Наибольшая допустимая степень многочлена.</summary>
    public const int MaxPolynomialDegree = 100_000_000;

    /// <summary>Характеристика поля p.</summary>
    public BigInteger Characteristic { get; }

    /// <summary>Степень расширения k (1 — простое поле).</summary>
    public int ExtensionDegree { get; }

    /// <summary>Порядок поля q = p^k.</summary>
    public BigInteger Order { get; }

    /// <summary>Параметры выбора алгоритмов для операций над многочленами этого поля.</summary>
    public PolyOptions Options { get; } = new();

    /// <summary>Порождающий многочлен расширения (пустая строка для простого поля).</summary>
    public abstract string Modulus { get; }

    /// <summary>Описание внутреннего представления элементов поля.</summary>
    public abstract string Implementation { get; }

    /// <summary>Краткое обозначение поля: GF(7), GF(2^8).</summary>
    public string Name => ExtensionDegree == 1
        ? $"GF({ShortNumber(Characteristic)})"
        : $"GF({ShortNumber(Characteristic)}^{ExtensionDegree})";

    private protected GaloisField(BigInteger p, int k)
    {
        Characteristic = p;
        ExtensionDegree = k;
        Order = BigInteger.Pow(p, k);
    }

    // --------------------------------------------------------- создание поля

    /// <summary>Создаёт поле по текстовой записи характеристики («7», «2^127-1»).</summary>
    /// <param name="p">характеристика — простое число или числовое выражение</param>
    /// <param name="k">степень расширения, 1 ≤ k ≤ 256</param>
    /// <param name="modulus">неприводимый многочлен степени k от x; null — подбирается автоматически</param>
    public static GaloisField Create(string p, int k = 1, string? modulus = null) =>
        Create(NumberTheory.ParseIntegerExpression(p), k, modulus);

    public static GaloisField Create(BigInteger p, int k = 1, string? modulus = null)
    {
        if (p < 2) throw new GfException(GfError.OrderOutOfRange, "Характеристика поля должна быть не меньше 2.");
        if (p.GetBitLength() > NumberTheory.MaxCharacteristicBits)
            throw new GfException(GfError.OrderOutOfRange,
                $"Характеристика поля превышает {NumberTheory.MaxCharacteristicBits} бит.");
        if (k < 1 || k > MaxExtensionDegree)
            throw new GfException(GfError.OrderOutOfRange, $"Степень расширения k должна быть от 1 до {MaxExtensionDegree}.");
        if (!NumberTheory.IsProbablePrime(p))
            throw new GfException(GfError.NotPrime,
                $"Число {PrimeFieldParsing.Short(p)} составное: характеристика поля Галуа должна быть простым числом.");
        if (k == 1 && !string.IsNullOrWhiteSpace(modulus))
            throw new GfException(GfError.InvalidModulus, "Для простого поля (k = 1) порождающий многочлен не задаётся.");

        if (p < PrimeField64.Limit)
        {
            var b = new PrimeField64((ulong)p);
            return k == 1 ? new FieldImpl<ulong, PrimeField64>(b, k, "", "простое поле, 64-битные вычеты")
                : MakeExtension<ulong, PrimeField64>(b, k, modulus, "расширение над 64-битными вычетами");
        }
        var big = new PrimeFieldBig(p);
        return k == 1 ? new FieldImpl<BigInteger, PrimeFieldBig>(big, k, "", "простое поле, длинная арифметика")
            : MakeExtension<BigInteger, PrimeFieldBig>(big, k, modulus, "расширение над длинной арифметикой");
    }

    private static GaloisField MakeExtension<TB, TBOps>(TBOps b, int k, string? modulusText, string impl)
        where TBOps : struct, IFieldOps<TB>
    {
        TB[] f;
        if (string.IsNullOrWhiteSpace(modulusText))
            f = FindIrreducible<TB, TBOps>(b, k);
        else
        {
            f = PolyParser.Parse<TB, TBOps>(modulusText, b, 'x', MaxExtensionDegree);
            if (f.Length - 1 != k)
                throw new GfException(GfError.InvalidModulus,
                    $"Степень порождающего многочлена ({f.Length - 1}) не совпадает со степенью расширения k = {k}.");
            f = PolyOps<TB, TBOps>.MakeMonic(f, b);
            if (!PolyOps<TB, TBOps>.IsIrreducible(f, b))
                throw new GfException(GfError.InvalidModulus,
                    "Порождающий многочлен приводим над GF(p): фактор-кольцо по нему не является полем.");
        }
        var ext = new ExtensionField<TB, TBOps>(b, f);
        return new FieldImpl<TB[], ExtensionField<TB, TBOps>>(ext, k, ext.ModulusText(), impl);
    }

    /// <summary>
    /// Подбор неприводимого унитарного многочлена степени k: при малом p — перебор в порядке возрастания
    /// (даёт, например, x^8 + x^4 + x^3 + x + 1 для GF(2^8)), при большом — случайный поиск с фиксированным зерном.
    /// </summary>
    internal static TB[] FindIrreducible<TB, TBOps>(TBOps b, int k) where TBOps : struct, IFieldOps<TB>
    {
        var c = new TB[k + 1];
        if (b.Characteristic <= 1000)
        {
            int p = (int)b.Characteristic;
            var digits = new int[k];
            while (true)
            {
                // следующий набор младших коэффициентов (число в p-ичной системе)
                int i = 0;
                while (i < k && ++digits[i] == p) digits[i++] = 0;
                if (i == k) break;
                if (digits[0] == 0) continue; // свободный член 0 — делится на x
                for (int j = 0; j < k; j++) c[j] = b.FromInteger(digits[j]);
                c[k] = b.One;
                if (PolyOps<TB, TBOps>.IsIrreducible(c, b)) return (TB[])c.Clone();
            }
        }
        var rng = new Random(12345);
        for (int attempt = 0; attempt < 100_000; attempt++)
        {
            for (int j = 0; j < k; j++) c[j] = b.Random(rng);
            c[k] = b.One;
            if (!b.IsZero(c[0]) && PolyOps<TB, TBOps>.IsIrreducible(c, b)) return (TB[])c.Clone();
        }
        throw new GfException(GfError.InvalidModulus, "Не удалось подобрать неприводимый многочлен.");
    }

    // ------------------------------------------------- работа с многочленами

    /// <summary>Разбор многочлена в алгебраической записи или списком коэффициентов (от старшего).</summary>
    public abstract Polynomial Parse(string text);

    /// <summary>Многочлен заданной степени со случайными коэффициентами (старший — ненулевой).</summary>
    public abstract Polynomial Random(int degree, int seed);

    /// <summary>Нулевой многочлен.</summary>
    public abstract Polynomial Zero { get; }

    /// <summary>Проверка записи элемента поля (точки вычисления значения); возвращает нормализованную запись.</summary>
    public abstract string NormalizeElement(string text);

    internal abstract int Length(Polynomial a);
    internal abstract Polynomial Add(Polynomial a, Polynomial b);
    internal abstract Polynomial Sub(Polynomial a, Polynomial b);
    internal abstract Polynomial Mul(Polynomial a, Polynomial b, CancellationToken ct);
    internal abstract (Polynomial Q, Polynomial R) DivRem(Polynomial a, Polynomial b, CancellationToken ct);
    internal abstract string Evaluate(Polynomial a, string point, CancellationToken ct);
    internal abstract bool Equal(Polynomial a, Polynomial b);
    internal abstract int Hash(Polynomial a);
    internal abstract string Algebraic(Polynomial a);
    internal abstract string Coefficients(Polynomial a);
    internal abstract string Coefficient(Polynomial a, int degree);

    public override string ToString() => Name;

    /// <summary>Сокращённая запись длинного числа: 12345678…87654321 (39 цифр).</summary>
    public static string ShortNumber(BigInteger n)
    {
        string s = n.ToString(CultureInfo.InvariantCulture);
        return s.Length <= 24 ? s : $"{s[..8]}…{s[^8..]} ({s.Length} цифр)";
    }
}

/// <summary>Реализация поля для конкретного представления элементов.</summary>
internal sealed class FieldImpl<T, TOps> : GaloisField where TOps : struct, IFieldOps<T>
{
    private readonly TOps _f;
    private readonly string _modulus;
    private readonly string _impl;

    public FieldImpl(TOps f, int k, string modulus, string impl) : base(f.Characteristic, k)
    {
        _f = f;
        _modulus = modulus;
        _impl = impl;
        // Пороги переключения алгоритмов подобраны по замерам (раздел 6 пояснительной записки):
        // чем дороже операция в поле, тем раньше окупаются асимптотически быстрые методы.
        (Options.KaratsubaThreshold, Options.NewtonThreshold) =
            typeof(T) == typeof(ulong) ? (32, 2048) : typeof(T) == typeof(BigInteger) ? (8, 512) : (4, 256);
    }

    public TOps Ops => _f;
    public override string Modulus => _modulus;
    public override string Implementation => _impl;

    private T[] C(Polynomial p)
    {
        if (!ReferenceEquals(p.Field, this))
            throw new GfException(GfError.FieldMismatch, "Многочлены заданы над разными полями.");
        return (T[])p.Data;
    }

    private Polynomial Wrap(T[] c) => new(this, c);

    public override Polynomial Zero => Wrap(Array.Empty<T>());

    public override Polynomial Parse(string text) => Wrap(PolyParser.Parse<T, TOps>(text, _f, 'x', MaxPolynomialDegree));

    public override Polynomial Random(int degree, int seed)
    {
        if (degree < 0 || degree > MaxPolynomialDegree)
            throw new GfException(GfError.TooLarge, $"Степень должна быть от 0 до {MaxPolynomialDegree}.");
        var rng = new Random(seed);
        var c = Guard(() => new T[degree + 1]);
        for (int i = 0; i < degree; i++) c[i] = _f.Random(rng);
        T lead;
        do lead = _f.Random(rng);
        while (_f.IsZero(lead));
        c[degree] = lead;
        return Wrap(c);
    }

    public override string NormalizeElement(string text) => _f.Format(ParsePoint(text));

    private T ParsePoint(string text)
    {
        string s = (text ?? "").Trim();
        if (s.Length == 0) throw new GfException(GfError.ParseError, "Не задана точка вычисления значения.");
        if (s.StartsWith('(') && s.EndsWith(')')) s = s[1..^1];
        return _f.ParseElement(s);
    }

    internal override int Length(Polynomial a) => C(a).Length;
    internal override Polynomial Add(Polynomial a, Polynomial b) => Wrap(Guard(() => PolyOps<T, TOps>.Add(C(a), C(b), _f)));
    internal override Polynomial Sub(Polynomial a, Polynomial b) => Wrap(Guard(() => PolyOps<T, TOps>.Sub(C(a), C(b), _f)));

    internal override Polynomial Mul(Polynomial a, Polynomial b, CancellationToken ct)
    {
        T[] x = C(a), y = C(b);
        if ((long)x.Length + y.Length - 1 > MaxPolynomialDegree + 1L)
            throw new GfException(GfError.TooLarge, $"Степень произведения превышает {MaxPolynomialDegree}.");
        return Wrap(Guard(() => PolyOps<T, TOps>.Mul(x, y, _f, Options, ct)));
    }

    internal override (Polynomial Q, Polynomial R) DivRem(Polynomial a, Polynomial b, CancellationToken ct)
    {
        T[] x = C(a), y = C(b);
        var (q, r) = Guard(() => PolyOps<T, TOps>.DivRem(x, y, _f, Options, ct));
        return (Wrap(q), Wrap(r));
    }

    internal override string Evaluate(Polynomial a, string point, CancellationToken ct)
    {
        T[] x = C(a);
        T c = ParsePoint(point);
        return _f.Format(Guard(() => PolyOps<T, TOps>.Evaluate(x, c, _f, ct)));
    }

    internal override bool Equal(Polynomial a, Polynomial b) =>
        ReferenceEquals(a.Field, b.Field) && PolyOps<T, TOps>.Equal(C(a), C(b), _f);

    internal override int Hash(Polynomial a)
    {
        var h = new HashCode();
        T[] c = C(a);
        h.Add(c.Length);
        for (int i = 0; i < Math.Min(c.Length, 64); i++) h.Add(_f.Hash(c[i]));
        return h.ToHashCode();
    }

    internal override string Algebraic(Polynomial a) => PolyFormatter.Algebraic<T, TOps>(C(a), _f, 'x', compact: false);
    internal override string Coefficients(Polynomial a) => PolyFormatter.Coefficients<T, TOps>(C(a), _f);

    internal override string Coefficient(Polynomial a, int degree)
    {
        T[] c = C(a);
        return degree >= 0 && degree < c.Length ? _f.Format(c[degree]) : "0";
    }

    /// <summary>Преобразует нехватку памяти и отмену в коды ошибок библиотеки.</summary>
    private static TR Guard<TR>(Func<TR> action)
    {
        try
        {
            return action();
        }
        catch (OutOfMemoryException ex)
        {
            throw new GfException(GfError.OutOfMemory, "Недостаточно оперативной памяти для выполнения операции.", ex);
        }
        catch (OperationCanceledException ex)
        {
            throw new GfException(GfError.Cancelled, "Операция прервана пользователем.", ex);
        }
    }
}
