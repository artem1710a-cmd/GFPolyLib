using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace GFPolyLib.Fields;

/// <summary>
/// Простое поле GF(p) при p &lt; 2^63: элементы хранятся в 64-битных беззнаковых целых.
/// При p &lt; 2^32 произведение вычетов помещается в 64 бита, иначе используется 128-битное произведение.
/// </summary>
public readonly struct PrimeField64 : IFieldOps<ulong>
{
    /// <summary>Граница применимости 64-битного представления.</summary>
    public static readonly BigInteger Limit = BigInteger.One << 63;

    private readonly ulong _p;
    private readonly bool _small;

    public PrimeField64(ulong p)
    {
        _p = p;
        _small = p <= uint.MaxValue;
    }

    public ulong P => _p;
    public BigInteger Characteristic => _p;
    public ulong Zero => 0;
    public ulong One => 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsZero(ulong a) => a == 0;

    public bool IsOne(ulong a) => a == 1;
    public bool Equal(ulong a, ulong b) => a == b;
    public int Hash(ulong a) => a.GetHashCode();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Add(ulong a, ulong b)
    {
        ulong s = a + b; // a, b < p < 2^63, переполнения нет
        return s >= _p ? s - _p : s;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Sub(ulong a, ulong b) => a >= b ? a - b : a + (_p - b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Neg(ulong a) => a == 0 ? 0 : _p - a;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Mul(ulong a, ulong b)
    {
        if (_small) return a * b % _p;
        return (ulong)((UInt128)a * b % _p);
    }

    public ulong Inv(ulong a) => NumberTheory.ModInverse(a, _p);

    public ulong FromInteger(BigInteger v)
    {
        BigInteger r = v % _p;
        if (r.Sign < 0) r += _p;
        return (ulong)r;
    }

    public ulong Random(Random rng) => (ulong)rng.NextInt64(0, (long)_p);

    public ulong ParseElement(string text) => FromInteger(PrimeFieldParsing.ParseInteger(text, Characteristic));

    public string Format(ulong a) => a.ToString(CultureInfo.InvariantCulture);
    public bool IsPrimeSubfield(ulong a) => true;
}

/// <summary>Простое поле GF(p) произвольного размера (p ≥ 2^63): элементы — <see cref="BigInteger"/>.</summary>
public readonly struct PrimeFieldBig : IFieldOps<BigInteger>
{
    private readonly BigInteger _p;
    private readonly int _bytes;

    public PrimeFieldBig(BigInteger p)
    {
        _p = p;
        _bytes = p.GetByteCount(isUnsigned: true) + 8;
    }

    public BigInteger Characteristic => _p;
    public BigInteger Zero => BigInteger.Zero;
    public BigInteger One => BigInteger.One;
    public bool IsZero(BigInteger a) => a.IsZero;
    public bool IsOne(BigInteger a) => a.IsOne;
    public bool Equal(BigInteger a, BigInteger b) => a == b;
    public int Hash(BigInteger a) => a.GetHashCode();

    public BigInteger Add(BigInteger a, BigInteger b)
    {
        BigInteger s = a + b;
        return s >= _p ? s - _p : s;
    }

    public BigInteger Sub(BigInteger a, BigInteger b)
    {
        BigInteger s = a - b;
        return s.Sign < 0 ? s + _p : s;
    }

    public BigInteger Neg(BigInteger a) => a.IsZero ? a : _p - a;
    public BigInteger Mul(BigInteger a, BigInteger b) => a * b % _p;
    public BigInteger Inv(BigInteger a) => NumberTheory.ModInverse(a, _p);

    public BigInteger FromInteger(BigInteger v)
    {
        BigInteger r = v % _p;
        return r.Sign < 0 ? r + _p : r;
    }

    public BigInteger Random(Random rng)
    {
        var buf = new byte[_bytes];
        rng.NextBytes(buf);
        return new BigInteger(buf, isUnsigned: true) % _p;
    }

    public BigInteger ParseElement(string text) => FromInteger(PrimeFieldParsing.ParseInteger(text, _p));
    public string Format(BigInteger a) => a.ToString(CultureInfo.InvariantCulture);
    public bool IsPrimeSubfield(BigInteger a) => true;
}

internal static class PrimeFieldParsing
{
    /// <summary>Разбор целого со знаком; модуль числа должен быть меньше p.</summary>
    public static BigInteger ParseInteger(string text, BigInteger p)
    {
        ReadOnlySpan<char> s = text.AsSpan().Trim();
        bool neg = false;
        if (s.Length > 0 && (s[0] == '-' || s[0] == '−'))
        {
            neg = true;
            s = s[1..].TrimStart();
        }
        else if (s.Length > 0 && s[0] == '+') s = s[1..].TrimStart();
        if (s.Length == 0) throw new GfException(GfError.ParseError, "Пустая запись элемента поля.");
        foreach (char c in s)
            if (!char.IsAsciiDigit(c))
                throw new GfException(GfError.ParseError, $"Недопустимый символ «{c}» в записи элемента поля «{text.Trim()}».");
        BigInteger v = BigInteger.Parse(s, NumberStyles.None, CultureInfo.InvariantCulture);
        if (v >= p)
            throw new GfException(GfError.CoefficientOutOfRange,
                $"Коэффициент {Short(v)} не принадлежит полю: допустимы значения от 0 до p − 1 = {Short(p - 1)}.");
        return neg ? -v : v;
    }

    public static string Short(BigInteger v)
    {
        string s = v.ToString(CultureInfo.InvariantCulture);
        return s.Length <= 30 ? s : $"{s[..10]}…{s[^10..]}";
    }
}
