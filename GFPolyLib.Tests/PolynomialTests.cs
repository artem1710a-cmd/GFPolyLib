using System.Numerics;
using GFPolyLib;
using GFPolyLib.Fields;

namespace GFPolyLib.Tests;

/// <summary>Контрольный пример из раздела 2 пояснительной записки (поле GF(7)).</summary>
public class ControlExampleTests
{
    private readonly GaloisField _f = GaloisField.Create("7");

    [Fact]
    public void AllOperationsMatchHandCalculation()
    {
        var a = _f.Parse("3x^4 + 5x^3 + x + 6");
        var b = _f.Parse("2x^2 + 4x + 1");

        Assert.Equal("3x^4 + 5x^3 + 2x^2 + 5x", (a + b).ToAlgebraic());
        Assert.Equal("3x^4 + 5x^3 + 5x^2 + 4x + 5", (a - b).ToAlgebraic());
        Assert.Equal("6x^6 + x^5 + 2x^4 + 2x^2 + 4x + 6", (a * b).ToAlgebraic());
        var (q, r) = a.DivRem(b);
        Assert.Equal("5x^2 + 3x + 2", q.ToAlgebraic());
        Assert.Equal("4x + 4", r.ToAlgebraic());
        Assert.Equal("5", a.Evaluate("2"));
    }

    [Fact]
    public void CoefficientListAndAlgebraicFormsAreEquivalent()
    {
        Assert.Equal(_f.Parse("3x^4 + 5x^3 + x + 6"), _f.Parse("3 5 0 1 6"));
        Assert.Equal(_f.Parse("x^2 - 1"), _f.Parse("1, 0, 6"));
        Assert.Equal("3 5 0 1 6", _f.Parse("6 + x + 5x^3 + 3*x^4").ToCoefficients());
        Assert.True(_f.Parse("x - x").IsZero);
        Assert.Equal(-1, _f.Parse("0").Degree);
    }
}

public class FieldTests
{
    [Fact]
    public void AesFieldIsBuiltWithStandardModulus()
    {
        var f = GaloisField.Create("2", 8);
        Assert.Equal("x^8 + x^4 + x^3 + x + 1", f.Modulus);
        Assert.Equal(new BigInteger(256), f.Order);
        // {53}·{CA} = {01} — пример из стандарта FIPS 197
        var p = f.Parse("(a^6 + a^4 + a + 1)x");
        Assert.Equal("1", p.Evaluate("a^7 + a^6 + a^3 + a"));
    }

    [Fact]
    public void ExtensionFieldAcceptsUserModulus()
    {
        var f = GaloisField.Create("3", 2, "x^2 + 1");
        Assert.Equal("GF(3^2)", f.Name);
        var p = f.Parse("(a)x + 1");
        Assert.Equal("0", p.Evaluate("a")); // a·a + 1 = −1 + 1 = 0
    }

    [Theory]
    [InlineData("7", 1)]
    [InlineData("2^61-1", 1)]
    [InlineData("2^127-1", 1)]
    [InlineData("2^521-1", 1)]
    [InlineData("3", 5)]
    [InlineData("1000003", 3)]
    [InlineData("2^89-1", 2)]
    public void FieldAxiomsHoldForRandomElements(string p, int k)
    {
        var f = GaloisField.Create(p, k);
        for (int seed = 1; seed <= 20; seed++)
        {
            var x = f.Random(0, seed);
            var y = f.Random(0, seed + 100);
            // x / x = 1 при x ≠ 0 (константы — многочлены нулевой степени)
            var (q, r) = x.DivRem(x);
            Assert.Equal(f.Parse("1"), q);
            Assert.True(r.IsZero);
            Assert.Equal(x * y, y * x);
            Assert.Equal(x, x + y - y);
        }
    }

    [Theory]
    [InlineData("1")]
    [InlineData("91")]
    [InlineData("2^64+1")]
    [InlineData("561")]                       // число Кармайкла
    [InlineData("3317044064679887385961981")] // сильное псевдопростое по основаниям 2..41
    public void CompositeCharacteristicIsRejected(string p)
    {
        var ex = Assert.Throws<GfException>(() => GaloisField.Create(p));
        Assert.True(ex.Code is GfError.NotPrime or GfError.OrderOutOfRange);
    }

    [Fact]
    public void ReducibleModulusIsRejected()
    {
        var ex = Assert.Throws<GfException>(() => GaloisField.Create("5", 2, "x^2 + 1")); // = (x − 2)(x + 2)
        Assert.Equal(GfError.InvalidModulus, ex.Code);
        ex = Assert.Throws<GfException>(() => GaloisField.Create("5", 3, "x^2 + 2"));
        Assert.Equal(GfError.InvalidModulus, ex.Code);
    }

    [Fact]
    public void InvalidParametersAreRejected()
    {
        Assert.Equal(GfError.OrderOutOfRange, Assert.Throws<GfException>(() => GaloisField.Create("7", 0)).Code);
        Assert.Equal(GfError.OrderOutOfRange, Assert.Throws<GfException>(() => GaloisField.Create("7", 300)).Code);
        Assert.Equal(GfError.ParseError, Assert.Throws<GfException>(() => GaloisField.Create("7x")).Code);
        Assert.Equal(GfError.ParseError, Assert.Throws<GfException>(() => GaloisField.Create("")).Code);
    }

    [Fact]
    public void MillerRabinAgreesWithTrialDivision()
    {
        for (int n = 0; n < 20000; n++)
        {
            bool trial = n >= 2;
            for (int d = 2; d * d <= n && trial; d++) trial = n % d != 0;
            Assert.Equal(trial, NumberTheory.IsProbablePrime(n));
        }
    }
}

public class ErrorHandlingTests
{
    private readonly GaloisField _f = GaloisField.Create("7");

    [Theory]
    [InlineData("3x^^2", GfError.ParseError)]
    [InlineData("3x^2 +", GfError.ParseError)]
    [InlineData("3x^2 4x", GfError.ParseError)]
    [InlineData("7x + 1", GfError.CoefficientOutOfRange)]
    [InlineData("1 2 9", GfError.CoefficientOutOfRange)]
    [InlineData("x^999999999999", GfError.TooLarge)]
    [InlineData("y^2", GfError.ParseError)]
    [InlineData("(1", GfError.ParseError)]
    public void BadInputGivesErrorCode(string text, GfError expected)
    {
        var ex = Assert.Throws<GfException>(() => _f.Parse(text));
        Assert.Equal(expected, ex.Code);
    }

    [Fact]
    public void DivisionByZeroPolynomial()
    {
        var ex = Assert.Throws<GfException>(() => _f.Parse("x + 1").DivRem(_f.Parse("0")));
        Assert.Equal(GfError.DivisionByZero, ex.Code);
    }

    [Fact]
    public void OperandsOverDifferentFields()
    {
        var other = GaloisField.Create("7");
        var ex = Assert.Throws<GfException>(() => _f.Parse("x") + other.Parse("x"));
        Assert.Equal(GfError.FieldMismatch, ex.Code);
    }

    [Fact]
    public void CancelledOperation()
    {
        var a = _f.Random(20000, 1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var ex = Assert.Throws<GfException>(() => a.Multiply(a, cts.Token));
        Assert.Equal(GfError.Cancelled, ex.Code);
    }

    [Fact]
    public void BadEvaluationPoint()
    {
        var a = _f.Parse("x + 1");
        Assert.Equal(GfError.CoefficientOutOfRange, Assert.Throws<GfException>(() => a.Evaluate("8")).Code);
        Assert.Equal(GfError.ParseError, Assert.Throws<GfException>(() => a.Evaluate("")).Code);
        Assert.Equal("0", a.Evaluate("-1"));
    }
}

/// <summary>Перекрёстная проверка алгоритмов на случайных многочленах.</summary>
public class AlgorithmCrossCheckTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var (p, k) in new[] { ("2", 1), ("7", 1), ("65537", 1), ("2^61-1", 1), ("2^127-1", 1), ("3", 4), ("2", 8), ("101", 2) })
            foreach (var (n, m) in new[] { (0, 0), (5, 3), (40, 40), (100, 37), (257, 255), (700, 129), (1500, 700) })
                yield return new object[] { p, k, n, m };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AllAlgorithmsAgree(string p, int k, int n, int m)
    {
        var f = GaloisField.Create(p, k);
        if (k > 1 && n > 300) return; // расширения медленнее — ограничиваем размер
        var a = f.Random(n, n * 31 + m);
        var b = f.Random(m, m * 17 + 5);

        f.Options.Multiplication = MulAlgorithm.School;
        var school = a * b;
        f.Options.Multiplication = MulAlgorithm.Karatsuba;
        f.Options.KaratsubaThreshold = 4;
        var kara = a * b;
        Assert.Equal(school, kara);
        Assert.Equal(n + m, kara.Degree);

        f.Options.Division = DivAlgorithm.Long;
        var (q1, r1) = a.DivRem(b);
        f.Options.Division = DivAlgorithm.Newton;
        var (q2, r2) = a.DivRem(b);
        Assert.Equal(q1, q2);
        Assert.Equal(r1, r2);
        Assert.True(r1.Degree < b.Degree);
        Assert.Equal(a, b * q1 + r1);

        // Значение произведения в точке равно произведению значений
        string c = f.Random(0, 7).ToCoefficients();
        var va = f.Parse(a.Evaluate(c));
        var vb = f.Parse(b.Evaluate(c));
        Assert.Equal(va * vb, f.Parse(kara.Evaluate(c)));
        Assert.Equal(va + vb, f.Parse((a + b).Evaluate(c)));
    }

    [Fact]
    public void LargeDegreeParallelKaratsuba()
    {
        var f = GaloisField.Create("998244353");
        var a = f.Random(60000, 1);
        var b = f.Random(60000, 2);
        f.Options.AllowParallel = true;
        var par = a * b;
        f.Options.AllowParallel = false;
        Assert.Equal(par, a * b);
        var (q, r) = (par + f.Parse("x + 1")).DivRem(b);
        Assert.Equal(a, q);
        Assert.Equal(f.Parse("x + 1"), r);
    }

    [Fact]
    public void UnbalancedOperands()
    {
        var f = GaloisField.Create("1000003");
        var a = f.Random(10000, 3);
        var b = f.Random(70, 4);
        f.Options.Multiplication = MulAlgorithm.School;
        var s = a * b;
        f.Options.Multiplication = MulAlgorithm.Karatsuba;
        f.Options.KaratsubaThreshold = 16;
        Assert.Equal(s, a * b);
    }
}
