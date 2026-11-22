import sys, os
from pptx import Presentation
from pptx.util import Cm, Pt, Emu
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN, MSO_ANCHOR
from pptx.enum.shapes import MSO_SHAPE
from PIL import Image

OUT = sys.argv[1]
SCR = os.path.dirname(os.path.abspath(__file__)) + "/.."
F, S = SCR + "/figs", SCR + "/shots4"

BLUE = RGBColor(0x0B, 0x3D, 0x7A)
LIGHT = RGBColor(0xE8, 0xEF, 0xF8)
GREY = RGBColor(0x55, 0x55, 0x55)
ACC = RGBColor(0xDC, 0x6E, 0x28)
FONT = "Arial"

prs = Presentation()
prs.slide_width, prs.slide_height = Cm(33.867), Cm(19.05)
W, H = prs.slide_width, prs.slide_height
blank = prs.slide_layouts[6]
num = [0]


def text(slide, x, y, w, h, paras, size=20, color=RGBColor(0x22, 0x22, 0x22), bold=False, align=PP_ALIGN.LEFT,
         anchor=MSO_ANCHOR.TOP, bullet=False, spacing=6):
    tb = slide.shapes.add_textbox(x, y, w, h)
    tf = tb.text_frame
    tf.word_wrap = True
    tf.vertical_anchor = anchor
    tf.margin_left = tf.margin_right = Cm(0.1)
    if isinstance(paras, str):
        paras = [paras]
    for i, p in enumerate(paras):
        par = tf.paragraphs[0] if i == 0 else tf.add_paragraph()
        par.alignment = align
        par.space_after = Pt(spacing)
        b = bold
        s = size
        if isinstance(p, tuple):
            p, opts = p
            b = opts.get("bold", b); s = opts.get("size", s)
        r = par.add_run()
        r.text = ("•  " + p) if bullet else p
        r.font.size = Pt(s); r.font.bold = b; r.font.name = FONT; r.font.color.rgb = color
    return tb


def slide(title):
    num[0] += 1
    s = prs.slides.add_slide(blank)
    bar = s.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, W, Cm(2.3))
    bar.fill.solid(); bar.fill.fore_color.rgb = BLUE; bar.line.fill.background()
    text(s, Cm(1.2), Cm(0.3), W - Cm(6), Cm(1.8), title, size=28, color=RGBColor(255, 255, 255), bold=True,
         anchor=MSO_ANCHOR.MIDDLE)
    text(s, W - Cm(4.5), Cm(0.3), Cm(3.6), Cm(1.8), "guap.ru", size=14, color=RGBColor(0xCF, 0xDD, 0xF0),
         align=PP_ALIGN.RIGHT, anchor=MSO_ANCHOR.MIDDLE)
    text(s, W - Cm(3), H - Cm(1.2), Cm(2.4), Cm(0.9), str(num[0]), size=12, color=GREY, align=PP_ALIGN.RIGHT)
    return s


def picture(s, path, x, y, maxw, maxh):
    im = Image.open(path)
    ar = im.width / im.height
    w, h = maxw, int(maxw / ar)
    if h > maxh:
        h, w = maxh, int(maxh * ar)
    return s.shapes.add_picture(path, x + (maxw - w) // 2, y + (maxh - h) // 2, w, h)


def box(s, x, y, w, h, title, lines, size=16):
    sh = s.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, x, y, w, h)
    sh.fill.solid(); sh.fill.fore_color.rgb = LIGHT; sh.line.color.rgb = RGBColor(0xB8, 0xC9, 0xE0)
    sh.adjustments[0] = 0.06
    text(s, x + Cm(0.4), y + Cm(0.25), w - Cm(0.8), Cm(1.0), title, size=size + 2, color=BLUE, bold=True)
    text(s, x + Cm(0.4), y + Cm(1.25), w - Cm(0.8), h - Cm(1.4), lines, size=size, bullet=True, spacing=4)


def table(s, x, y, w, rows, colw, size=13, rowh=Cm(1.0)):
    t = s.shapes.add_table(len(rows), len(rows[0]), x, y, w, rowh * len(rows)).table
    for j, cw in enumerate(colw):
        t.columns[j].width = cw
    for i, row in enumerate(rows):
        for j, val in enumerate(row):
            c = t.cell(i, j)
            c.text = val
            p = c.text_frame.paragraphs[0]
            p.runs[0].font.size = Pt(size); p.runs[0].font.name = FONT
            p.runs[0].font.bold = i == 0
            p.runs[0].font.color.rgb = RGBColor(255, 255, 255) if i == 0 else RGBColor(0x22, 0x22, 0x22)
            c.fill.solid(); c.fill.fore_color.rgb = BLUE if i == 0 else (LIGHT if i % 2 else RGBColor(255, 255, 255))
            c.margin_left = c.margin_right = Cm(0.2)
    return t


# 1 ------------------------------------------------------------------ титул
num[0] += 1
s = prs.slides.add_slide(blank)
bg = s.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, W, H); bg.fill.solid(); bg.fill.fore_color.rgb = BLUE; bg.line.fill.background()
text(s, Cm(2), Cm(1.2), W - Cm(4), Cm(1.5), "ГУАП · Кафедра технологий защиты информации (33)", size=16,
     color=RGBColor(0xCF, 0xDD, 0xF0))
text(s, Cm(2), Cm(4.0), W - Cm(4), Cm(5.5),
     ["Разработка динамической библиотеки операций над многочленами в поле Галуа с характеристикой q"],
     size=36, color=RGBColor(255, 255, 255), bold=True)
text(s, Cm(2), Cm(9.8), W - Cm(4), Cm(1.5), "Курсовой проект по дисциплине «Технологии и методы программирования»",
     size=18, color=RGBColor(0xCF, 0xDD, 0xF0))
text(s, W - Cm(14), Cm(12.6), Cm(12), Cm(5),
     ["Выполнил: студент гр. 3431", ("А. Ю. Машир", {"bold": True}), "Руководитель: профессор, к.т.н.",
      ("С. Г. Фомичева", {"bold": True})], size=18, color=RGBColor(255, 255, 255), align=PP_ALIGN.RIGHT, spacing=2)
text(s, Cm(2), H - Cm(2.2), Cm(10), Cm(1), "Санкт-Петербург, 2026", size=16, color=RGBColor(0xCF, 0xDD, 0xF0))

# 2 ------------------------------------------------------------- актуальность
s = slide("Актуальность")
text(s, Cm(1.2), Cm(3.0), Cm(19), Cm(14), [
    "Арифметика многочленов над GF(q) лежит в основе кодов Рида – Соломона, БЧХ, схемы разделения секрета Шамира, "
    "алгоритма Берлекэмпа – Мэсси",
    "Поля нужны очень разные: GF(2⁸) в AES и «Кузнечике», простые поля с p ≈ 2²⁵⁵…2⁵²¹ в эллиптической криптографии",
    "Готовые системы (NTL, FLINT) громоздки, а «самописный» код повторяет одни и те же ошибки: переполнение, "
    "нулевые старшие коэффициенты, деление на нуль",
    "Результаты длительных вычислений обычно не сохраняются — их нельзя проверить и повторить"],
    size=19, bullet=True, spacing=12)
box(s, Cm(21.2), Cm(3.2), Cm(11.4), Cm(13.5), "Нужна библиотека, которая", [
    "работает с полями любого размера и расширениями GF(pᵏ)",
    "обрабатывает многочлены большой степени",
    "сообщает об ошибках кодами, а не падает",
    "сохраняет данные в реляционном хранилище"], size=17)

# 3 --------------------------------------------------------------- цель
s = slide("Цель, объект и предмет исследования")
box(s, Cm(1.2), Cm(3.0), Cm(31.4), Cm(3.3), "Цель", [
    "Создание динамической библиотеки GFPolyLib для операций над многочленами в GF(q), q = pᵏ, и демонстрационного "
    "приложения с реляционным хранилищем"], size=17)
box(s, Cm(1.2), Cm(6.7), Cm(15.4), Cm(4.0), "Объект", ["Арифметика многочленов над конечным полем GF(q)"], size=17)
box(s, Cm(17.2), Cm(6.7), Cm(15.4), Cm(4.0), "Предмет", [
    "Алгоритмы операций, их оформление в DLL, хранение данных и результатов"], size=17)
box(s, Cm(1.2), Cm(11.1), Cm(31.4), Cm(6.6), "Задачи", [
    "формализовать задачу и выбрать алгоритмы для произвольно больших q и степеней",
    "построить модель IDEF0 (2 уровня декомпозиции) и DFD хранилища, спроектировать БД",
    "реализовать библиотеку, приложение и тесты; оценить связность и сцепление модулей",
    "провести тестирование корректности, ошибок и производительности"], size=16)

# 4 ----------------------------------------------------- назначение/функции
s = slide("Назначение и функции системы")
sh = s.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, Cm(1.2), Cm(3.0), Cm(31.4), Cm(2.6))
sh.fill.solid(); sh.fill.fore_color.rgb = LIGHT; sh.line.color.rgb = ACC; sh.line.width = Pt(2)
text(s, Cm(1.7), Cm(3.1), Cm(30.4), Cm(2.4),
     "Система GFPolyLib предназначена для выполнения арифметических операций над многочленами с коэффициентами "
     "из конечного поля GF(q).", size=20, bold=True, color=BLUE, anchor=MSO_ANCHOR.MIDDLE)
funcs = ["A1  Задание поля GF(q), q = pᵏ, и проверка его параметров",
         "A2  Ввод и проверка исходных многочленов и точки c",
         "A3  Выполнение операций: +, −, ×, деление с остатком, A(c)",
         "A4  Ведение хранилища: поля, многочлены, журнал, замеры",
         "A5  Вывод результатов на экран, в файл и на принтер"]
text(s, Cm(1.2), Cm(6.4), Cm(18.5), Cm(11), funcs, size=19, spacing=14)
box(s, Cm(20.4), Cm(6.4), Cm(12.2), Cm(11.2), "Уточнения после ПК-1", [
    "нет ограничений на размер данных",
    "поле не обязательно двоичное, p — сколь угодно большое (до 8192 бит)",
    "степень расширения k — любая (1…256)",
    "реляционное хранилище, обоснованное DFD"], size=16)

# 5 ------------------------------------------------------ постановка задачи
s = slide("Математическая модель и выбор алгоритмов")
table(s, Cm(1.2), Cm(3.0), Cm(31.4), [
    ["Операция", "Выбранный алгоритм", "Сложность"],
    ["Арифметика GF(p)", "64-бит / 128-бит / BigInteger — по размеру p", "O(1) при p < 2⁶³"],
    ["Проверка простоты p", "Миллер – Рабин (вместо пробного деления)", "O(t·log p)"],
    ["Расширение GF(pᵏ)", "умножение mod f, проверка f — тест Бен-Ора", "O(k²)"],
    ["Сложение, вычитание", "покоэффициентно", "O(n)"],
    ["Умножение", "школьный при n ≤ n0, иначе Карацуба + параллельно", "O(n^1,585)"],
    ["Деление с остатком", "«уголком» или итерации Ньютона (по порогу N0)", "O(min(nm, M(n)))"],
    ["Значение A(c)", "схема Горнера (оптимальна)", "n умн. + n слож."]],
    [Cm(7.5), Cm(16.4), Cm(7.5)], size=15, rowh=Cm(1.35))
text(s, Cm(1.2), Cm(14.6), Cm(31.4), Cm(3),
     "A·B = A₁B₁x²ʰ + [(A₀+A₁)(B₀+B₁) − A₀B₀ − A₁B₁]xʰ + A₀B₀  — три умножения половинной длины вместо четырёх",
     size=17, color=BLUE, align=PP_ALIGN.CENTER)

# 6 --------------------------------------------------------------- IDEF0 A0
s = slide("Функциональная модель IDEF0: декомпозиция A0")
picture(s, F + "/idef0_A0.png", Cm(1.0), Cm(2.6), Cm(31.8), Cm(16.0))

# 7 --------------------------------------------------------------- IDEF0 A3
s = slide("IDEF0: второй уровень — блок A3")
picture(s, F + "/idef0_A3.png", Cm(1.0), Cm(2.6), Cm(23.5), Cm(16.0))
text(s, Cm(24.8), Cm(3.4), Cm(8.3), Cm(14), [
    "Второй уровень построен для A1, A3, A4",
    "A1: разбор p → Миллер – Рабин → Бен-Ор → выбор представления",
    "A3: выбор алгоритма по порогам → операция",
    "A4: поле → многочлены без дублей → журнал → выборка"], size=15, bullet=True, spacing=12)

# 8 --------------------------------------------------------------------- DFD
s = slide("Обоснование хранилища: диаграмма потоков данных")
picture(s, F + "/dfd.png", Cm(1.0), Cm(2.6), Cm(21.5), Cm(16.0))
text(s, Cm(22.8), Cm(3.2), Cm(10.3), Cm(14.5), [
    "DFD выявила 4 накопителя: поля, многочлены, журнал операций, замеры",
    "Между ними — связи «один ко многим»",
    "Регистрация операции должна быть атомарной → транзакции",
    "Вывод: реляционная модель, встраиваемая СУБД SQLite (без сервера, один файл, ACID)"],
    size=15, bullet=True, spacing=12)

# 9 ---------------------------------------------------------------------- ER
s = slide("Логическая модель базы данных")
picture(s, F + "/er.png", Cm(1.0), Cm(2.6), Cm(22.5), Cm(15.5))
text(s, Cm(23.8), Cm(3.2), Cm(9.3), Cm(14.5), [
    "4 таблицы, третья нормальная форма",
    "Внешние ключи с каскадным удалением, CHECK-ограничения",
    "Многочлены не дублируются: UNIQUE (field_id, SHA-256 коэффициентов)",
    "Операция записывается в одной транзакции",
    "Параметризованные запросы — без SQL-инъекций"], size=15, bullet=True, spacing=10)

# 10 ------------------------------------------------------------ архитектура
s = slide("Архитектура и иерархия классов")
picture(s, F + "/classes.png", Cm(1.0), Cm(2.6), Cm(20.5), Cm(16.0))
text(s, Cm(21.8), Cm(3.0), Cm(11.3), Cm(15), [
    "C# 12, .NET 8, Visual Studio 2022, Windows Forms, SQLite, xUnit",
    "GFPolyLib.dll не зависит от интерфейса и БД",
    "Обобщённые алгоритмы PolyOps<T, TOps> специализируются для 64-бит, BigInteger и GF(pᵏ) без потери скорости",
    "Связность модулей библиотеки — 9,2 из 10, сцепление — 2,1",
    "11 кодов ошибок, отмена длительных операций"], size=15, bullet=True, spacing=9)

# 11 -------------------------------------------------------------- программа
s = slide("Результаты работы программы")
picture(s, S + "/01_calc_gf7.png", Cm(1.0), Cm(2.8), Cm(15.6), Cm(11.0))
picture(s, S + "/04_calc_big.png", Cm(17.2), Cm(2.8), Cm(15.6), Cm(11.0))
text(s, Cm(1.0), Cm(14.0), Cm(15.6), Cm(4), ["Контрольный пример в GF(7): совпадает с ручным расчётом",
                                             "Q = 5x² + 3x + 2, R = 4x + 4, A(2) = 5"], size=15, align=PP_ALIGN.CENTER)
text(s, Cm(17.2), Cm(14.0), Cm(15.6), Cm(4), ["p = 2¹²⁷ − 1, степени 3000 и 1200:",
                                              "автоматически выбраны Карацуба и деление Ньютона"], size=15, align=PP_ALIGN.CENTER)

# 12 ------------------------------------------------------- хранилище и ошибки
s = slide("Хранилище и защита от ошибочных действий")
picture(s, S + "/05_storage.png", Cm(1.0), Cm(2.8), Cm(17.5), Cm(13.0))
text(s, Cm(19.0), Cm(3.0), Cm(14), Cm(15), [
    "Недопустимые символы не вводятся",
    "Ошибка записи подсвечивается с номером позиции",
    "Кнопки доступны только при корректном состоянии",
    "Составное p, приводимый f(x), деление на нуль, коэффициент ≥ p — понятные сообщения",
    "Подтверждение удаления, очистки, выхода во время расчёта",
    "Журнал: повтор любой операции, переход к нужному полю"], size=15, bullet=True, spacing=9)

# 13 --------------------------------------------------------- производительность
s = slide("Производительность")
picture(s, F + "/bench_mul64.png", Cm(0.8), Cm(2.8), Cm(16.0), Cm(10.2))
picture(s, F + "/bench_parallel.png", Cm(17.0), Cm(2.8), Cm(16.0), Cm(10.2))
text(s, Cm(1.0), Cm(13.3), Cm(31.8), Cm(5), [
    "Карацуба при n = 16384: 0,16 с вместо 1,80 с (×11)",
    "Распараллеливание на 20 потоках: ещё ×11–12,5 (131072 коэффициента — 0,37 с)",
    "Деление Ньютона при n = 32768: 0,22 с вместо 7,25 с (×33); пороги подобраны по замерам"],
    size=16, bullet=True, spacing=6)

# 14 ------------------------------------------------------------ тестирование
s = slide("Тестирование и выводы")
box(s, Cm(1.2), Cm(3.0), Cm(15.4), Cm(14.6), "Тестирование", [
    "89 модульных тестов xUnit — все пройдены",
    "контрольный пример и пример AES {53}·{CA} = {01}",
    "поля до 2⁵²¹ − 1 и расширения GF(3⁵), GF(2⁸), GF((2⁸⁹−1)²)",
    "перекрёстная проверка: школьный = Карацуба, «уголком» = Ньютон, A = BQ + R",
    "отклонение составных p и приводимых f(x)"], size=15)
box(s, Cm(17.2), Cm(3.0), Cm(15.4), Cm(14.6), "Итоги", [
    "библиотека работает с полями любого размера и расширениями",
    "многочлены степени 10⁵ и выше обрабатываются за доли секунды",
    "реализовано обоснованное DFD реляционное хранилище (3НФ)",
    "Windows 11, i7-12700F; требуется .NET 8 или self-contained сборка",
    "код, тесты, readme и руководство пользователя — в репозитории"], size=15)

# 15 ------------------------------------------------------------------- финал
num[0] += 1
s = prs.slides.add_slide(blank)
bg = s.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, W, H); bg.fill.solid(); bg.fill.fore_color.rgb = BLUE; bg.line.fill.background()
text(s, Cm(2), Cm(6.5), W - Cm(4), Cm(3), "Спасибо за внимание!", size=44, bold=True,
     color=RGBColor(255, 255, 255), align=PP_ALIGN.CENTER)
text(s, Cm(2), Cm(10), W - Cm(4), Cm(2), "Демонстрация программы GFPolyApp", size=22,
     color=RGBColor(0xCF, 0xDD, 0xF0), align=PP_ALIGN.CENTER)
prs.save(OUT)
print("slides:", len(prs.slides))
