namespace GFPolyLib;

/// <summary>
/// Многочлен над конечным полем. Объект неизменяем: все операции возвращают новые многочлены,
/// поэтому один многочлен можно безопасно использовать из нескольких потоков.
/// </summary>
public sealed class Polynomial : IEquatable<Polynomial>
{
    /// <summary>Поле коэффициентов.</summary>
    public GaloisField Field { get; }

    /// <summary>Нормализованный массив коэффициентов (тип зависит от представления поля).</summary>
    internal object Data { get; }

    internal Polynomial(GaloisField field, object data)
    {
        Field = field;
        Data = data;
    }

    /// <summary>Степень многочлена; у нулевого многочлена −1.</summary>
    public int Degree => Field.Length(this) - 1;

    /// <summary>Является ли многочлен нулевым.</summary>
    public bool IsZero => Field.Length(this) == 0;

    /// <summary>Коэффициент при x^i в текстовой записи.</summary>
    public string this[int i] => Field.Coefficient(this, i);

    public Polynomial Add(Polynomial b) => Field.Add(this, Check(b));
    public Polynomial Subtract(Polynomial b) => Field.Sub(this, Check(b));

    public Polynomial Multiply(Polynomial b, CancellationToken ct = default) => Field.Mul(this, Check(b), ct);

    /// <summary>Деление с остатком: this = b·Q + R, deg R &lt; deg b.</summary>
    public (Polynomial Quotient, Polynomial Remainder) DivRem(Polynomial b, CancellationToken ct = default)
    {
        var (q, r) = Field.DivRem(this, Check(b), ct);
        return (q, r);
    }

    /// <summary>Значение многочлена в точке поля (запись точки — как у коэффициента).</summary>
    public string Evaluate(string point, CancellationToken ct = default) => Field.Evaluate(this, point, ct);

    public static Polynomial operator +(Polynomial a, Polynomial b) => a.Add(b);
    public static Polynomial operator -(Polynomial a, Polynomial b) => a.Subtract(b);
    public static Polynomial operator *(Polynomial a, Polynomial b) => a.Multiply(b);
    public static Polynomial operator /(Polynomial a, Polynomial b) => a.DivRem(b).Quotient;
    public static Polynomial operator %(Polynomial a, Polynomial b) => a.DivRem(b).Remainder;

    /// <summary>Алгебраическая запись: 3x^4 + 5x^3 + x + 6.</summary>
    public string ToAlgebraic() => Field.Algebraic(this);

    /// <summary>Список коэффициентов от старшего к младшему: 3 5 0 1 6.</summary>
    public string ToCoefficients() => Field.Coefficients(this);

    public override string ToString() => ToAlgebraic();

    public bool Equals(Polynomial? other) => other is not null && Field.Equal(this, other);
    public override bool Equals(object? obj) => obj is Polynomial p && Equals(p);
    public override int GetHashCode() => Field.Hash(this);

    private Polynomial Check(Polynomial b)
    {
        ArgumentNullException.ThrowIfNull(b);
        if (!ReferenceEquals(b.Field, Field))
            throw new GfException(GfError.FieldMismatch, "Многочлены заданы над разными полями.");
        return b;
    }
}
