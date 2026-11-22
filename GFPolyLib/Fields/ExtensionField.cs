using System.Numerics;
using GFPolyLib.Algorithms;

namespace GFPolyLib.Fields;

/// <summary>
/// Расширение GF(p^k) = GF(p)[a]/(f(a)), где f — неприводимый унитарный многочлен степени k.
/// Элемент поля хранится массивом из k коэффициентов базового поля (многочлен от a степени меньше k).
/// </summary>
public readonly struct ExtensionField<TB, TBOps> : IFieldOps<TB[]> where TBOps : struct, IFieldOps<TB>
{
    /// <summary>Обозначение порождающего элемента расширения в записи элементов.</summary>
    public const char Generator = 'a';

    private readonly TBOps _b;
    private readonly TB[] _f;   // унитарный модуль, длина k + 1
    private readonly int _k;
    private readonly TB[] _zero;
    private readonly TB[] _one;

    public ExtensionField(TBOps baseField, TB[] monicModulus)
    {
        _b = baseField;
        _f = monicModulus;
        _k = monicModulus.Length - 1;
        _zero = new TB[_k];
        Array.Fill(_zero, baseField.Zero);
        _one = (TB[])_zero.Clone();
        _one[0] = baseField.One;
    }

    public TBOps BaseField => _b;
    public int K => _k;
    public TB[] Modulus => _f;
    public BigInteger Characteristic => _b.Characteristic;
    public TB[] Zero => _zero;
    public TB[] One => _one;

    public bool IsZero(TB[] a)
    {
        foreach (TB c in a)
            if (!_b.IsZero(c)) return false;
        return true;
    }

    public bool IsOne(TB[] a)
    {
        if (!_b.IsOne(a[0])) return false;
        for (int i = 1; i < a.Length; i++)
            if (!_b.IsZero(a[i])) return false;
        return true;
    }

    public bool Equal(TB[] a, TB[] b)
    {
        for (int i = 0; i < _k; i++)
            if (!_b.Equal(a[i], b[i])) return false;
        return true;
    }

    public int Hash(TB[] a)
    {
        var h = new HashCode();
        foreach (TB c in a) h.Add(_b.Hash(c));
        return h.ToHashCode();
    }

    public TB[] Add(TB[] a, TB[] b)
    {
        var r = new TB[_k];
        for (int i = 0; i < _k; i++) r[i] = _b.Add(a[i], b[i]);
        return r;
    }

    public TB[] Sub(TB[] a, TB[] b)
    {
        var r = new TB[_k];
        for (int i = 0; i < _k; i++) r[i] = _b.Sub(a[i], b[i]);
        return r;
    }

    public TB[] Neg(TB[] a)
    {
        var r = new TB[_k];
        for (int i = 0; i < _k; i++) r[i] = _b.Neg(a[i]);
        return r;
    }

    /// <summary>Произведение многочленов от a с последующим приведением по модулю f(a).</summary>
    public TB[] Mul(TB[] a, TB[] b)
    {
        int k = _k;
        var t = new TB[2 * k - 1];
        Array.Fill(t, _b.Zero);
        for (int i = 0; i < k; i++)
        {
            TB ai = a[i];
            if (_b.IsZero(ai)) continue;
            for (int j = 0; j < k; j++) t[i + j] = _b.Add(t[i + j], _b.Mul(ai, b[j]));
        }
        for (int i = 2 * k - 2; i >= k; i--)
        {
            TB c = t[i];
            if (_b.IsZero(c)) continue;
            // a^k = −(f_(k−1) a^(k−1) + … + f_0)
            for (int j = 0; j < k; j++) t[i - k + j] = _b.Sub(t[i - k + j], _b.Mul(c, _f[j]));
        }
        var r = new TB[k];
        Array.Copy(t, r, k);
        return r;
    }

    public TB[] Inv(TB[] a)
    {
        if (IsZero(a)) throw new GfException(GfError.DivisionByZero, "Нулевой элемент поля не имеет обратного.");
        TB[] inv = PolyOps<TB, TBOps>.InverseMod(PolyOps<TB, TBOps>.Trim(a, a.Length, _b), _f, _b)
                   ?? throw new GfException(GfError.InvalidModulus, "Элемент необратим: модуль расширения приводим.");
        return Pad(inv);
    }

    private TB[] Pad(TB[] p)
    {
        var r = new TB[_k];
        Array.Fill(r, _b.Zero);
        Array.Copy(p, r, Math.Min(p.Length, _k));
        return r;
    }

    public TB[] FromInteger(BigInteger v)
    {
        var r = (TB[])_zero.Clone();
        r[0] = _b.FromInteger(v);
        return r;
    }

    public TB[] Random(Random rng)
    {
        var r = new TB[_k];
        for (int i = 0; i < _k; i++) r[i] = _b.Random(rng);
        return r;
    }

    /// <summary>Разбор элемента: целое число или многочлен от a, например «a^2 + 3a + 1».</summary>
    public TB[] ParseElement(string text)
    {
        TB[] p = PolyParser.Parse<TB, TBOps>(text, _b, Generator, int.MaxValue);
        if (p.Length > _k) p = PolyOps<TB, TBOps>.DivRemLong(p, _f, _b, CancellationToken.None).R;
        return Pad(p);
    }

    public string Format(TB[] a)
    {
        TB[] p = PolyOps<TB, TBOps>.Trim(a, a.Length, _b);
        if (p.Length == 0) return "0";
        if (p.Length == 1) return _b.Format(p[0]);
        return "(" + PolyFormatter.Algebraic<TB, TBOps>(p, _b, Generator, compact: true) + ")";
    }

    public bool IsPrimeSubfield(TB[] a)
    {
        for (int i = 1; i < a.Length; i++)
            if (!_b.IsZero(a[i])) return false;
        return true;
    }

    /// <summary>Запись модуля расширения в виде многочлена от x.</summary>
    public string ModulusText() => PolyFormatter.Algebraic<TB, TBOps>(_f, _b, 'x', compact: false);
}
