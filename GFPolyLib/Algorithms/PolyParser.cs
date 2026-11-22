using System.Globalization;
using System.Numerics;
using System.Text;
using GFPolyLib.Fields;

namespace GFPolyLib.Algorithms;

/// <summary>
/// Разбор текстовой записи многочлена. Поддерживаются две формы:
/// <list type="bullet">
/// <item>алгебраическая — «3x^4 + 5x^3 − x + 6», «(a+1)x^2 + a·x + 1»;</item>
/// <item>список коэффициентов от старшего к младшему — «3 5 0 −1 6».</item>
/// </list>
/// Нечисловые коэффициенты (элементы расширения) записываются в круглых скобках.
/// </summary>
internal static class PolyParser
{
    public static T[] Parse<T, TOps>(string text, TOps f, char variable, int maxDegree) where TOps : struct, IFieldOps<T>
    {
        if (text is null) throw new GfException(GfError.ParseError, "Не задана запись многочлена.");
        string s = text.Trim();
        if (s.Length == 0) throw new GfException(GfError.ParseError, "Пустая запись многочлена.");
        return HasTopLevelVariable(s, variable)
            ? ParseAlgebraic<T, TOps>(s, f, variable, maxDegree)
            : ParseList<T, TOps>(s, f, maxDegree);
    }

    private static bool HasTopLevelVariable(string s, char v)
    {
        int depth = 0;
        char up = char.ToUpperInvariant(v);
        foreach (char c in s)
        {
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (depth == 0 && (c == v || c == up)) return true;
        }
        return false;
    }

    // ------------------------------------------------------------ список

    private static T[] ParseList<T, TOps>(string s, TOps f, int maxDegree) where TOps : struct, IFieldOps<T>
    {
        var coeffs = new List<T>(Math.Max(4, s.Length / 2));
        int i = 0;
        while (true)
        {
            while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == ',' || s[i] == ';')) i++;
            if (i >= s.Length) break;
            int tokenStart = i;
            T c;
            if (s[i] == '(')
            {
                int close = MatchParen(s, i);
                c = f.ParseElement(s.Substring(i + 1, close - i - 1));
                i = close + 1;
            }
            else
            {
                bool neg = false;
                if (s[i] is '-' or '−') { neg = true; i++; }
                else if (s[i] == '+') i++;
                int start = i;
                while (i < s.Length && char.IsAsciiDigit(s[i])) i++;
                if (start == i)
                    throw Error(s, tokenStart, "ожидался коэффициент (целое число или элемент поля в скобках)");
                c = Integer<T, TOps>(s, start, i, neg, f);
            }
            if (i < s.Length && !(char.IsWhiteSpace(s[i]) || s[i] == ',' || s[i] == ';'))
                throw Error(s, i, "коэффициенты разделяются пробелами или запятыми");
            coeffs.Add(c);
            if (coeffs.Count - 1 > maxDegree)
                throw new GfException(GfError.TooLarge, $"Степень многочлена превышает допустимую ({maxDegree}).");
        }
        if (coeffs.Count == 0) throw new GfException(GfError.ParseError, "Пустая запись многочлена.");
        coeffs.Reverse(); // запись от старшего коэффициента к младшему
        return PolyOps<T, TOps>.Trim(coeffs.ToArray(), coeffs.Count, f);
    }

    // ----------------------------------------------------- алгебраическая

    private static T[] ParseAlgebraic<T, TOps>(string s, TOps f, char v, int maxDegree) where TOps : struct, IFieldOps<T>
    {
        T[] acc = PolyOps<T, TOps>.Zeros(16, f);
        int maxDeg = -1;
        int i = 0;
        bool first = true;
        char vu = char.ToUpperInvariant(v);
        while (true)
        {
            SkipSpaces(s, ref i);
            if (i >= s.Length)
            {
                if (first) throw new GfException(GfError.ParseError, "Пустая запись многочлена.");
                throw Error(s, i, "запись оборвана после знака операции");
            }
            bool neg = false;
            if (s[i] is '+' or '-' or '−')
            {
                neg = s[i] != '+';
                i++;
                SkipSpaces(s, ref i);
            }
            else if (!first) throw Error(s, i, "между членами многочлена ожидался знак «+» или «−»");
            first = false;

            // коэффициент
            T coef = f.One;
            bool hasCoef = false;
            if (i < s.Length && s[i] == '(')
            {
                int close = MatchParen(s, i);
                coef = f.ParseElement(s.Substring(i + 1, close - i - 1));
                i = close + 1;
                hasCoef = true;
            }
            else if (i < s.Length && char.IsAsciiDigit(s[i]))
            {
                int start = i;
                while (i < s.Length && char.IsAsciiDigit(s[i])) i++;
                coef = Integer<T, TOps>(s, start, i, false, f);
                hasCoef = true;
            }
            SkipSpaces(s, ref i);
            if (hasCoef && i < s.Length && s[i] is '*' or '·')
            {
                i++;
                SkipSpaces(s, ref i);
                if (i >= s.Length || (s[i] != v && s[i] != vu)) throw Error(s, i, $"после знака умножения ожидалась переменная {v}");
            }

            // одночлен x^n
            int deg = 0;
            if (i < s.Length && (s[i] == v || s[i] == vu))
            {
                i++;
                deg = 1;
                SkipSpaces(s, ref i);
                if (i < s.Length && s[i] == '^')
                {
                    i++;
                    SkipSpaces(s, ref i);
                    int start = i;
                    while (i < s.Length && char.IsAsciiDigit(s[i])) i++;
                    if (start == i) throw Error(s, start, "после «^» ожидался показатель степени");
                    if (i - start > 10 || !int.TryParse(s.AsSpan(start, i - start), NumberStyles.None,
                            CultureInfo.InvariantCulture, out deg) || deg > maxDegree)
                        throw new GfException(GfError.TooLarge, $"Степень многочлена превышает допустимую ({maxDegree}).");
                }
            }
            else if (!hasCoef)
                throw Error(s, i, i < s.Length ? $"недопустимый символ «{s[i]}»" : "ожидался член многочлена");

            if (neg) coef = f.Neg(coef);
            if (deg >= acc.Length)
            {
                int newLen = Math.Max(deg + 1, Math.Min(acc.Length * 2, maxDegree + 1));
                var bigger = PolyOps<T, TOps>.Zeros(newLen, f);
                Array.Copy(acc, bigger, acc.Length);
                acc = bigger;
            }
            acc[deg] = f.Add(acc[deg], coef);
            if (deg > maxDeg) maxDeg = deg;

            SkipSpaces(s, ref i);
            if (i >= s.Length) break;
        }
        return PolyOps<T, TOps>.Trim(acc, maxDeg + 1, f);
    }

    // ---------------------------------------------------------- служебные

    private static T Integer<T, TOps>(string s, int start, int end, bool neg, TOps f) where TOps : struct, IFieldOps<T>
    {
        BigInteger v;
        if (end - start <= 18)
            v = long.Parse(s.AsSpan(start, end - start), NumberStyles.None, CultureInfo.InvariantCulture);
        else
            v = BigInteger.Parse(s.AsSpan(start, end - start), NumberStyles.None, CultureInfo.InvariantCulture);
        if (v >= f.Characteristic)
            throw new GfException(GfError.CoefficientOutOfRange,
                $"Коэффициент {PrimeFieldParsing.Short(v)} (позиция {start + 1}) не принадлежит полю: " +
                $"допустимы целые от 0 до p − 1 = {PrimeFieldParsing.Short(f.Characteristic - 1)}.");
        return f.FromInteger(neg ? -v : v);
    }

    private static int MatchParen(string s, int open)
    {
        int depth = 0;
        for (int j = open; j < s.Length; j++)
        {
            if (s[j] == '(') depth++;
            else if (s[j] == ')' && --depth == 0) return j;
        }
        throw Error(s, open, "не закрыта скобка");
    }

    private static void SkipSpaces(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }

    private static GfException Error(string s, int pos, string what)
    {
        int from = Math.Max(0, pos - 12);
        int len = Math.Min(s.Length - from, 25);
        string near = s.Substring(from, len);
        return new GfException(GfError.ParseError, $"Ошибка в позиции {pos + 1}: {what} (фрагмент «{near}»).");
    }
}

/// <summary>Формирование текстовой записи многочлена.</summary>
internal static class PolyFormatter
{
    public static string Algebraic<T, TOps>(T[] p, TOps f, char v, bool compact) where TOps : struct, IFieldOps<T>
    {
        if (p.Length == 0) return "0";
        var sb = new StringBuilder(p.Length * 8);
        string sep = compact ? "+" : " + ";
        bool first = true;
        for (int d = p.Length - 1; d >= 0; d--)
        {
            T c = p[d];
            if (f.IsZero(c)) continue;
            if (!first) sb.Append(sep);
            first = false;
            if (d == 0 || !f.IsOne(c)) sb.Append(f.Format(c));
            if (d >= 1) sb.Append(v);
            if (d >= 2) sb.Append('^').Append(d.ToString(CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    public static string Coefficients<T, TOps>(T[] p, TOps f) where TOps : struct, IFieldOps<T>
    {
        if (p.Length == 0) return "0";
        var sb = new StringBuilder(p.Length * 4);
        for (int d = p.Length - 1; d >= 0; d--)
        {
            sb.Append(f.Format(p[d]));
            if (d > 0) sb.Append(' ');
        }
        return sb.ToString();
    }
}
