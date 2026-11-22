using System.Numerics;

namespace GFPolyLib.Fields;

/// <summary>
/// Арифметика конечного поля над элементами типа <typeparamref name="T"/>.
/// Реализуется структурами, поэтому обобщённые алгоритмы над многочленами
/// специализируются JIT-компилятором для каждого представления поля без виртуальных вызовов.
/// </summary>
public interface IFieldOps<T>
{
    /// <summary>Характеристика поля p.</summary>
    BigInteger Characteristic { get; }

    T Zero { get; }
    T One { get; }

    bool IsZero(T a);
    bool IsOne(T a);
    bool Equal(T a, T b);
    int Hash(T a);

    T Add(T a, T b);
    T Sub(T a, T b);
    T Neg(T a);
    T Mul(T a, T b);

    /// <summary>Обратный элемент; для нуля — <see cref="GfError.DivisionByZero"/>.</summary>
    T Inv(T a);

    /// <summary>Образ целого числа в поле (приведение по модулю p).</summary>
    T FromInteger(BigInteger v);

    /// <summary>Равномерно распределённый случайный элемент поля.</summary>
    T Random(Random rng);

    /// <summary>Разбор записи элемента поля (без внешних скобок).</summary>
    T ParseElement(string text);

    /// <summary>Запись элемента: целое число или выражение в скобках.</summary>
    string Format(T a);

    /// <summary>Является ли элемент элементом простого подполя (записывается целым числом).</summary>
    bool IsPrimeSubfield(T a);
}
