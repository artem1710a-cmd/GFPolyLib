using System.Globalization;
using System.Numerics;

namespace GFPolyLib.Fields;

/// <summary>Теоретико-числовые функции: проверка простоты, обращение по модулю, разбор числовых выражений.</summary>
public static class NumberTheory
{
    /// <summary>Наибольшая допустимая битовая длина характеристики поля.</summary>
    public const int MaxCharacteristicBits = 8192;

    private static readonly int[] SmallPrimes =
        { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97 };

    // Для n < 3,3·10^24 проверка по основаниям 2..41 даёт точный ответ.
    private static readonly int[] DeterministicBases = { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41 };

    /// <summary>
    /// Тест простоты Миллера — Рабина. Для n &lt; 3,3·10^24 результат детерминирован,
    /// для больших n дополнительно выполняется <paramref name="extraRounds"/> раундов со случайными основаниями
    /// (вероятность ошибки не более 4^−extraRounds).
    /// </summary>
    public static bool IsProbablePrime(BigInteger n, int extraRounds = 32)
    {
        if (n < 2) return false;
        foreach (int sp in SmallPrimes)
        {
            if (n == sp) return true;
            if (n % sp == 0) return false;
        }

        BigInteger d = n - 1;
        int s = 0;
        while (d.IsEven)
        {
            d >>= 1;
            s++;
        }

        foreach (int a in DeterministicBases)
            if (!MillerRabinRound(n, a, d, s)) return false;

        if (n < BigInteger.Parse("3317044064679887385961981", CultureInfo.InvariantCulture)) return true;

        var rng = new Random(unchecked((int)(n & int.MaxValue)));
        int bytes = n.GetByteCount(isUnsigned: true);
        var buf = new byte[bytes + 1];
        for (int i = 0; i < extraRounds; i++)
        {
            rng.NextBytes(buf);
            buf[^1] = 0;
            BigInteger a = new BigInteger(buf) % (n - 3) + 2;
            if (!MillerRabinRound(n, a, d, s)) return false;
        }
        return true;
    }

    private static bool MillerRabinRound(BigInteger n, BigInteger a, BigInteger d, int s)
    {
        BigInteger x = BigInteger.ModPow(a, d, n);
        if (x.IsOne || x == n - 1) return true;
        for (int r = 1; r < s; r++)
        {
            x = BigInteger.ModPow(x, 2, n);
            if (x == n - 1) return true;
            if (x.IsOne) return false;
        }
        return false;
    }

    /// <summary>Обратный элемент по модулю m расширенным алгоритмом Евклида.</summary>
    public static BigInteger ModInverse(BigInteger a, BigInteger m)
    {
        a %= m;
        if (a.Sign < 0) a += m;
        if (a.IsZero) throw new GfException(GfError.DivisionByZero, "Нулевой элемент поля не имеет обратного.");
        BigInteger r0 = m, r1 = a, t0 = 0, t1 = 1;
        while (!r1.IsZero)
        {
            BigInteger q = BigInteger.DivRem(r0, r1, out BigInteger r2);
            (r0, r1) = (r1, r2);
            (t0, t1) = (t1, t0 - q * t1);
        }
        if (!r0.IsOne) throw new GfException(GfError.DivisionByZero, "Элемент необратим по заданному модулю.");
        return t0.Sign < 0 ? t0 + m : t0;
    }

    /// <summary>Обратный элемент по модулю m &lt; 2^63 расширенным алгоритмом Евклида.</summary>
    public static ulong ModInverse(ulong a, ulong m)
    {
        a %= m;
        if (a == 0) throw new GfException(GfError.DivisionByZero, "Нулевой элемент поля не имеет обратного.");
        long r0 = (long)m, r1 = (long)a, t0 = 0, t1 = 1;
        while (r1 != 0)
        {
            long q = r0 / r1;
            (r0, r1) = (r1, r0 - q * r1);
            (t0, t1) = (t1, t0 - q * t1);
        }
        if (r0 != 1) throw new GfException(GfError.DivisionByZero, "Элемент необратим по заданному модулю.");
        return (ulong)(t0 < 0 ? t0 + (long)m : t0);
    }

    /// <summary>
    /// Разбор неотрицательного целочисленного выражения: десятичные числа, скобки,
    /// операции +, −, *, ^ (например «2^127-1», «10^9+7»).
    /// </summary>
    public static BigInteger ParseIntegerExpression(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new GfException(GfError.ParseError, "Не задано значение характеристики поля.");
        var p = new ExprParser(text);
        BigInteger v = p.ParseSum();
        p.SkipSpaces();
        if (!p.End) throw new GfException(GfError.ParseError, $"Недопустимый символ «{text[p.Pos]}» в позиции {p.Pos + 1}.");
        return v;
    }

    private sealed class ExprParser
    {
        private readonly string _s;
        public int Pos;

        public ExprParser(string s) => _s = s;

        public bool End => Pos >= _s.Length;

        public void SkipSpaces()
        {
            while (Pos < _s.Length && char.IsWhiteSpace(_s[Pos])) Pos++;
        }

        private char Peek()
        {
            SkipSpaces();
            return Pos < _s.Length ? _s[Pos] : '\0';
        }

        public BigInteger ParseSum()
        {
            BigInteger v = ParseProduct();
            while (true)
            {
                char c = Peek();
                if (c == '+') { Pos++; v += ParseProduct(); }
                else if (c is '-' or '−') { Pos++; v -= ParseProduct(); }
                else return Check(v);
            }
        }

        private BigInteger ParseProduct()
        {
            BigInteger v = ParsePower();
            while (Peek() is '*' or '·' or '×')
            {
                Pos++;
                v = Check(v * ParsePower());
            }
            return v;
        }

        private BigInteger ParsePower()
        {
            BigInteger b = ParseAtom();
            if (Peek() != '^') return b;
            Pos++;
            BigInteger e = ParsePower();
            if (e.Sign < 0) throw new GfException(GfError.ParseError, "Отрицательный показатель степени недопустим.");
            long bits = (long)Math.Ceiling(BigInteger.Abs(b).IsZero ? 0 : BigInteger.Log(BigInteger.Abs(b), 2));
            if (e > MaxCharacteristicBits * 2 || bits * (double)e > MaxCharacteristicBits * 2)
                throw new GfException(GfError.OrderOutOfRange,
                    $"Значение превышает {MaxCharacteristicBits} бит — слишком большая характеристика поля.");
            return BigInteger.Pow(b, (int)e);
        }

        private BigInteger ParseAtom()
        {
            char c = Peek();
            if (c == '(')
            {
                Pos++;
                BigInteger v = ParseSum();
                if (Peek() != ')') throw new GfException(GfError.ParseError, "Не закрыта скобка в числовом выражении.");
                Pos++;
                return v;
            }
            int start = Pos;
            while (Pos < _s.Length && char.IsAsciiDigit(_s[Pos])) Pos++;
            if (start == Pos)
                throw new GfException(GfError.ParseError,
                    End ? "Числовое выражение оборвано." : $"Ожидалось число в позиции {start + 1}.");
            if (Pos - start > MaxCharacteristicBits / 3 + 10)
                throw new GfException(GfError.OrderOutOfRange, "Слишком длинная запись числа.");
            return BigInteger.Parse(_s.AsSpan(start, Pos - start), NumberStyles.None, CultureInfo.InvariantCulture);
        }

        private static BigInteger Check(BigInteger v)
        {
            if (v.GetBitLength() > MaxCharacteristicBits * 2)
                throw new GfException(GfError.OrderOutOfRange,
                    $"Значение превышает {MaxCharacteristicBits} бит — слишком большая характеристика поля.");
            return v;
        }
    }
}
