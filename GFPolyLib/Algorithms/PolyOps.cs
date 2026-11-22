using System.Numerics;
using GFPolyLib.Fields;

namespace GFPolyLib.Algorithms;

/// <summary>
/// Алгоритмы над многочленами в плотном представлении: массив коэффициентов,
/// элемент с индексом i — коэффициент при x^i. Нормализованный многочлен не содержит
/// нулевых старших коэффициентов; нулевой многочлен — пустой массив.
/// Входные массивы никогда не изменяются.
/// </summary>
internal static class PolyOps<T, TOps> where TOps : struct, IFieldOps<T>
{
    private static readonly bool NeedsFill = default(T) is null;

    // ------------------------------------------------------------ служебные

    public static T[] Zeros(int n, in TOps f)
    {
        var r = new T[n];
        if (NeedsFill && n > 0) Array.Fill(r, f.Zero);
        return r;
    }

    /// <summary>Нормализация: отбрасывает нулевые старшие коэффициенты среди первых <paramref name="len"/>.</summary>
    public static T[] Trim(T[] a, int len, in TOps f)
    {
        int n = Math.Min(len, a.Length);
        while (n > 0 && f.IsZero(a[n - 1])) n--;
        if (n == a.Length) return a;
        if (n == 0) return Array.Empty<T>();
        var r = new T[n];
        Array.Copy(a, r, n);
        return r;
    }

    public static bool Equal(T[] a, T[] b, in TOps f)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (!f.Equal(a[i], b[i])) return false;
        return true;
    }

    /// <summary>Коэффициенты в обратном порядке, дополненные нулями до длины <paramref name="len"/>.</summary>
    public static T[] Reverse(T[] a, int len, in TOps f)
    {
        var r = Zeros(len, f);
        int n = Math.Min(a.Length, len);
        for (int i = 0; i < n; i++) r[len - 1 - i] = a[i];
        return r;
    }

    // ------------------------------------------------- сложение и вычитание

    public static T[] Add(T[] a, T[] b, in TOps f)
    {
        if (a.Length < b.Length) (a, b) = (b, a);
        var r = new T[a.Length];
        for (int i = 0; i < b.Length; i++) r[i] = f.Add(a[i], b[i]);
        Array.Copy(a, b.Length, r, b.Length, a.Length - b.Length);
        return Trim(r, r.Length, f);
    }

    public static T[] Sub(T[] a, T[] b, in TOps f)
    {
        int n = Math.Max(a.Length, b.Length);
        var r = new T[n];
        int common = Math.Min(a.Length, b.Length);
        for (int i = 0; i < common; i++) r[i] = f.Sub(a[i], b[i]);
        for (int i = common; i < a.Length; i++) r[i] = a[i];
        for (int i = common; i < b.Length; i++) r[i] = f.Neg(b[i]);
        return Trim(r, n, f);
    }

    public static T[] Neg(T[] a, in TOps f)
    {
        var r = new T[a.Length];
        for (int i = 0; i < a.Length; i++) r[i] = f.Neg(a[i]);
        return r;
    }

    public static T[] Scale(T[] a, T c, in TOps f)
    {
        if (f.IsZero(c)) return Array.Empty<T>();
        var r = new T[a.Length];
        for (int i = 0; i < a.Length; i++) r[i] = f.Mul(a[i], c);
        return r;
    }

    // ---------------------------------------------------------- умножение

    public static T[] Mul(T[] a, T[] b, in TOps f, PolyOptions o, CancellationToken ct)
    {
        if (a.Length == 0 || b.Length == 0) return Array.Empty<T>();
        MulAlgorithm alg = o.ResolveMultiplication(a.Length, b.Length);
        T[] r;
        try
        {
            r = alg == MulAlgorithm.School ? MulSchool(a, b, f, ct) : MulKaratsuba(a, b, f, o, ct);
        }
        catch (AggregateException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.Flatten().InnerException!).Throw();
            throw;
        }
        return Trim(r, r.Length, f);
    }

    /// <summary>Школьное умножение по формуле свёртки c_k = Σ a_i b_(k−i).</summary>
    public static T[] MulSchool(T[] a, T[] b, in TOps f, CancellationToken ct)
    {
        var r = Zeros(a.Length + b.Length - 1, f);
        if (a.Length < b.Length) (a, b) = (b, a);
        for (int j = 0; j < b.Length; j++)
        {
            if ((j & 63) == 0) ct.ThrowIfCancellationRequested();
            T bj = b[j];
            if (f.IsZero(bj)) continue;
            for (int i = 0; i < a.Length; i++)
                r[i + j] = f.Add(r[i + j], f.Mul(a[i], bj));
        }
        return r;
    }

    private static void SchoolAcc(T[] a, int ao, int an, T[] b, int bo, int bn, T[] r, int ro, in TOps f)
    {
        for (int i = 0; i < an; i++)
        {
            T ai = a[ao + i];
            if (f.IsZero(ai)) continue;
            int k = ro + i;
            for (int j = 0; j < bn; j++)
                r[k + j] = f.Add(r[k + j], f.Mul(ai, b[bo + j]));
        }
    }

    private static int ParallelDepth(PolyOptions o)
    {
        if (!o.AllowParallel || Environment.ProcessorCount < 2) return 0;
        // три ветви на уровень: глубины log3(P) + 1 достаточно, чтобы загрузить все ядра
        return (int)Math.Ceiling(Math.Log(Environment.ProcessorCount, 3)) + 1;
    }

    /// <summary>Умножение методом Карацубы с поддержкой операндов разной длины.</summary>
    public static T[] MulKaratsuba(T[] a, T[] b, in TOps f, PolyOptions o, CancellationToken ct)
    {
        if (a.Length < b.Length) (a, b) = (b, a);
        int n = a.Length, m = b.Length;
        int t = o.KaratsubaThreshold;
        int depth = ParallelDepth(o);
        if (m < t) return MulSchool(a, b, f, ct);
        if (n == m) return KMul(a, 0, b, 0, n, f, t, depth, o.ParallelThreshold, ct);

        // Несбалансированный случай: длинный операнд режется на блоки длины m,
        // каждый блок умножается на b методом Карацубы. Блоки с номерами одной чётности
        // не перекрываются в результате, поэтому обрабатываются параллельно.
        var r = Zeros(n + m - 1, f);
        int blocks = (n + m - 1) / m;
        var fc = f;
        for (int parity = 0; parity < 2; parity++)
        {
            int count = (blocks - parity + 1) / 2;
            void Block(int idx)
            {
                int s = (2 * idx + parity) * m;
                int len = Math.Min(m, n - s);
                T[] prod;
                if (len == m) prod = KMul(a, s, b, 0, m, fc, t, depth > 0 ? depth - 1 : 0, o.ParallelThreshold, ct);
                else
                {
                    var part = new T[len];
                    Array.Copy(a, s, part, 0, len);
                    prod = len < t ? MulSchool(part, b, fc, ct) : MulKaratsuba(b, part, fc, o, ct);
                }
                for (int i = 0; i < prod.Length; i++) r[s + i] = fc.Add(r[s + i], prod[i]);
            }
            if (depth > 0 && count > 1 && (long)n * m >= (long)o.ParallelThreshold * o.ParallelThreshold)
                Parallel.For(0, count, new ParallelOptions { CancellationToken = ct }, Block);
            else
                for (int idx = 0; idx < count; idx++) Block(idx);
        }
        return r;
    }

    /// <summary>
    /// Рекурсивный шаг Карацубы для операндов одинаковой длины n:
    /// A·B = A1B1·x^(2h) + [(A0+A1)(B0+B1) − A0B0 − A1B1]·x^h + A0B0.
    /// Возвращает массив длины 2n − 1.
    /// </summary>
    private static T[] KMul(T[] a, int ao, T[] b, int bo, int n, TOps f, int t, int depth, int parThreshold,
        CancellationToken ct)
    {
        if (n <= t)
        {
            var res = Zeros(2 * n - 1, f);
            SchoolAcc(a, ao, n, b, bo, n, res, 0, f);
            return res;
        }
        ct.ThrowIfCancellationRequested();

        int h = n / 2, hi = n - h;
        var sa = new T[hi];
        var sb = new T[hi];
        for (int i = 0; i < hi; i++)
        {
            sa[i] = i < h ? f.Add(a[ao + i], a[ao + h + i]) : a[ao + h + i];
            sb[i] = i < h ? f.Add(b[bo + i], b[bo + h + i]) : b[bo + h + i];
        }

        T[] z0 = null!, z1 = null!, z2 = null!;
        if (depth > 0 && n >= parThreshold)
        {
            Parallel.Invoke(new ParallelOptions { CancellationToken = ct },
                () => z0 = KMul(a, ao, b, bo, h, f, t, depth - 1, parThreshold, ct),
                () => z2 = KMul(a, ao + h, b, bo + h, hi, f, t, depth - 1, parThreshold, ct),
                () => z1 = KMul(sa, 0, sb, 0, hi, f, t, depth - 1, parThreshold, ct));
        }
        else
        {
            z0 = KMul(a, ao, b, bo, h, f, t, 0, parThreshold, ct);
            z2 = KMul(a, ao + h, b, bo + h, hi, f, t, 0, parThreshold, ct);
            z1 = KMul(sa, 0, sb, 0, hi, f, t, 0, parThreshold, ct);
        }

        for (int i = 0; i < z0.Length; i++) z1[i] = f.Sub(z1[i], z0[i]);
        for (int i = 0; i < z2.Length; i++) z1[i] = f.Sub(z1[i], z2[i]);

        var r = new T[2 * n - 1];
        Array.Copy(z0, 0, r, 0, z0.Length);   // [0, 2h − 2]
        r[2 * h - 1] = f.Zero;                // единственная позиция между z0 и z2
        Array.Copy(z2, 0, r, 2 * h, z2.Length); // [2h, 2n − 2]
        for (int i = 0; i < z1.Length; i++) r[h + i] = f.Add(r[h + i], z1[i]);
        return r;
    }

    /// <summary>Произведение, усечённое до первых <paramref name="len"/> коэффициентов (mod x^len).</summary>
    public static T[] MulLow(T[] a, T[] b, int len, in TOps f, PolyOptions o, CancellationToken ct)
    {
        T[] aa = a.Length > len ? a[..len] : a;
        T[] bb = b.Length > len ? b[..len] : b;
        T[] r = Mul(Trim(aa, aa.Length, f), Trim(bb, bb.Length, f), f, o, ct);
        return Trim(r, len, f);
    }

    // ------------------------------------------------------------- деление

    public static (T[] Q, T[] R) DivRem(T[] a, T[] b, in TOps f, PolyOptions o, CancellationToken ct)
    {
        if (b.Length == 0) throw new GfException(GfError.DivisionByZero, "Деление на нулевой многочлен.");
        if (a.Length < b.Length) return (Array.Empty<T>(), a);
        return o.ResolveDivision(a.Length, b.Length) == DivAlgorithm.Newton
            ? DivRemNewton(a, b, f, o, ct)
            : DivRemLong(a, b, f, ct);
    }

    /// <summary>Деление «уголком»: на каждом шаге исключается старший член остатка.</summary>
    public static (T[] Q, T[] R) DivRemLong(T[] a, T[] b, in TOps f, CancellationToken ct)
    {
        if (b.Length == 0) throw new GfException(GfError.DivisionByZero, "Деление на нулевой многочлен.");
        if (a.Length < b.Length) return (Array.Empty<T>(), a);
        int m = b.Length - 1;
        T lead = b[m];
        bool monic = f.IsOne(lead);
        T inv = monic ? lead : f.Inv(lead);
        var r = (T[])a.Clone();
        var q = Zeros(a.Length - m, f);
        for (int k = a.Length - 1 - m; k >= 0; k--)
        {
            if ((k & 255) == 0) ct.ThrowIfCancellationRequested();
            T top = r[k + m];
            if (f.IsZero(top)) continue;
            T c = monic ? top : f.Mul(top, inv);
            q[k] = c;
            for (int j = 0; j < m; j++) r[k + j] = f.Sub(r[k + j], f.Mul(c, b[j]));
            r[k + m] = f.Zero;
        }
        return (Trim(q, q.Length, f), Trim(r, m, f));
    }

    /// <summary>Обратный степенной ряд: g такой, что s·g ≡ 1 (mod x^len); требуется s_0 ≠ 0.</summary>
    public static T[] InverseSeries(T[] s, int len, in TOps f, PolyOptions o, CancellationToken ct)
    {
        T[] g = { f.Inv(s[0]) };
        int cur = 1;
        while (cur < len)
        {
            cur = Math.Min(2 * cur, len);
            // g ← g·(2 − s·g) mod x^cur
            T[] e = MulLow(s, g, cur, f, o, ct);
            var t = Zeros(Math.Max(e.Length, 1), f);
            for (int i = 0; i < e.Length; i++) t[i] = f.Neg(e[i]);
            t[0] = f.Add(t[0], f.Add(f.One, f.One));
            g = MulLow(g, Trim(t, t.Length, f), cur, f, o, ct);
        }
        return g;
    }

    /// <summary>
    /// Деление через обращение ряда: rev(Q) = rev(A)·rev(B)^(−1) mod x^(n−m+1), R = A − B·Q.
    /// </summary>
    public static (T[] Q, T[] R) DivRemNewton(T[] a, T[] b, in TOps f, PolyOptions o, CancellationToken ct)
    {
        if (b.Length == 0) throw new GfException(GfError.DivisionByZero, "Деление на нулевой многочлен.");
        if (a.Length < b.Length) return (Array.Empty<T>(), a);
        int k = a.Length - b.Length + 1; // длина частного
        T[] ra = Trim(Reverse(a, a.Length, f), k, f);
        T[] rb = Reverse(b, b.Length, f);
        T[] inv = InverseSeries(Trim(rb, Math.Min(k, rb.Length), f), k, f, o, ct);
        T[] qr = MulLow(ra, inv, k, f, o, ct);
        T[] q = Trim(Reverse(qr, k, f), k, f);
        T[] bq = Mul(b, q, f, o, ct);
        T[] r = Sub(a, bq, f);
        return (q, Trim(r, b.Length - 1, f));
    }

    // ----------------------------------------------------- значение в точке

    /// <summary>Схема Горнера: A(c) = (…(a_n·c + a_(n−1))·c + …)·c + a_0.</summary>
    public static T Evaluate(T[] a, T x, in TOps f, CancellationToken ct)
    {
        T acc = f.Zero;
        for (int i = a.Length - 1; i >= 0; i--)
        {
            if ((i & 4095) == 0) ct.ThrowIfCancellationRequested();
            acc = f.Add(f.Mul(acc, x), a[i]);
        }
        return acc;
    }

    // --------------------------------------- НОД и арифметика по модулю

    public static T[] MakeMonic(T[] a, in TOps f)
    {
        if (a.Length == 0 || f.IsOne(a[^1])) return a;
        return Scale(a, f.Inv(a[^1]), f);
    }

    public static T[] Gcd(T[] a, T[] b, in TOps f)
    {
        while (b.Length > 0)
        {
            var (_, r) = DivRemLong(a, b, f, CancellationToken.None);
            (a, b) = (b, r);
        }
        return MakeMonic(a, f);
    }

    /// <summary>Обратный к a по модулю m (расширенный алгоритм Евклида); null, если НОД(a, m) ≠ 1.</summary>
    public static T[]? InverseMod(T[] a, T[] m, in TOps f)
    {
        T[] r0 = m, r1 = DivRemLong(a, m, f, CancellationToken.None).R;
        T[] s0 = Array.Empty<T>(), s1 = { f.One };
        var none = new PolyOptions { Multiplication = MulAlgorithm.School, AllowParallel = false };
        while (r1.Length > 0)
        {
            var (q, r2) = DivRemLong(r0, r1, f, CancellationToken.None);
            (r0, r1) = (r1, r2);
            (s0, s1) = (s1, Sub(s0, Mul(q, s1, f, none, CancellationToken.None), f));
        }
        if (r0.Length != 1) return null;
        return Scale(s0, f.Inv(r0[0]), f);
    }

    public static T[] MulMod(T[] a, T[] b, T[] m, in TOps f)
    {
        var o = new PolyOptions { AllowParallel = false };
        return DivRemLong(Mul(a, b, f, o, CancellationToken.None), m, f, CancellationToken.None).R;
    }

    public static T[] PowMod(T[] a, BigInteger e, T[] m, in TOps f)
    {
        T[] result = { f.One };
        result = DivRemLong(result, m, f, CancellationToken.None).R;
        T[] bs = DivRemLong(a, m, f, CancellationToken.None).R;
        int bits = (int)e.GetBitLength();
        for (int i = bits - 1; i >= 0; i--)
        {
            result = MulMod(result, result, m, f);
            if (!(e >> i).IsEven) result = MulMod(result, bs, m, f);
        }
        return result;
    }

    /// <summary>
    /// Тест неприводимости Бен-Ора: f степени k неприводим над GF(p) тогда и только тогда,
    /// когда НОД(x^(p^i) − x, f) = 1 для всех i = 1, …, ⌊k/2⌋.
    /// </summary>
    public static bool IsIrreducible(T[] poly, in TOps f)
    {
        int k = poly.Length - 1;
        if (k < 1) return false;
        if (k == 1) return true;
        T[] mon = MakeMonic(poly, f);
        T[] x = { f.Zero, f.One };
        T[] h = x;
        for (int i = 1; i <= k / 2; i++)
        {
            h = PowMod(h, f.Characteristic, mon, f);
            T[] g = Gcd(mon, Sub(h, x, f), f);
            if (g.Length > 1) return false;
        }
        return true;
    }
}
