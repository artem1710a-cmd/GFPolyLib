namespace GFPolyLib;

/// <summary>Коды ошибок библиотеки GFPolyLib.</summary>
public enum GfError
{
    /// <summary>Ошибок нет.</summary>
    Ok = 0,
    /// <summary>Недопустимое значение аргумента.</summary>
    InvalidArgument = 1,
    /// <summary>Характеристика поля не является простым числом.</summary>
    NotPrime = 2,
    /// <summary>Порядок поля или степень расширения вне допустимого диапазона.</summary>
    OrderOutOfRange = 3,
    /// <summary>Порождающий многочлен расширения приводим или имеет неверную степень.</summary>
    InvalidModulus = 4,
    /// <summary>Синтаксическая ошибка в записи многочлена, элемента или числа.</summary>
    ParseError = 5,
    /// <summary>Коэффициент не принадлежит полю.</summary>
    CoefficientOutOfRange = 6,
    /// <summary>Деление на нулевой многочлен или обращение нулевого элемента.</summary>
    DivisionByZero = 7,
    /// <summary>Операнды заданы над разными полями.</summary>
    FieldMismatch = 8,
    /// <summary>Степень многочлена превышает допустимую.</summary>
    TooLarge = 9,
    /// <summary>Недостаточно оперативной памяти для выполнения операции.</summary>
    OutOfMemory = 10,
    /// <summary>Операция прервана пользователем.</summary>
    Cancelled = 11,
}

/// <summary>Исключение, которым библиотека сообщает о недопустимых входных данных.</summary>
public sealed class GfException : Exception
{
    /// <summary>Код ошибки.</summary>
    public GfError Code { get; }

    public GfException(GfError code, string message) : base(message) => Code = code;

    public GfException(GfError code, string message, Exception inner) : base(message, inner) => Code = code;

    /// <summary>Краткое описание кода ошибки на русском языке.</summary>
    public static string Describe(GfError code) => code switch
    {
        GfError.Ok => "ошибок нет",
        GfError.InvalidArgument => "недопустимый аргумент",
        GfError.NotPrime => "характеристика поля должна быть простым числом",
        GfError.OrderOutOfRange => "параметр поля вне допустимого диапазона",
        GfError.InvalidModulus => "недопустимый порождающий многочлен расширения",
        GfError.ParseError => "ошибка в записи",
        GfError.CoefficientOutOfRange => "коэффициент не принадлежит полю",
        GfError.DivisionByZero => "деление на нуль",
        GfError.FieldMismatch => "операнды заданы над разными полями",
        GfError.TooLarge => "слишком большая степень многочлена",
        GfError.OutOfMemory => "недостаточно оперативной памяти",
        GfError.Cancelled => "операция прервана",
        _ => "неизвестная ошибка",
    };
}
