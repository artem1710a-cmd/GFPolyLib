import os, sys
sys.path.insert(0, os.path.dirname(__file__))
from docgen import Builder, render, fmt, add_inline, add_field, FONT
from docx.shared import Pt, Cm
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
import content1, content2

ROOT = sys.argv[1]
OUTDOC = sys.argv[2]
SCR = os.path.dirname(os.path.abspath(__file__)) + "/.."
F = os.path.dirname(os.path.abspath(__file__)) + "/рисунки"
S = os.path.dirname(os.path.abspath(__file__)) + "/скриншоты"

SOURCES = {
    "lidl": "Лидл, Р. Конечные поля : в 2 т. / Р. Лидл, Г. Нидеррайтер ; пер. с англ. – Москва : Мир, 1988. – 822 с.",
    "gluhov": "Глухов, М. М. Алгебра : учебник / М. М. Глухов, В. П. Елизаров, А. А. Нечаев. – 2-е изд., испр. и доп. – Санкт-Петербург : Лань, 2015. – 608 с.",
    "reed": "Reed, I. S. Polynomial codes over certain finite fields / I. S. Reed, G. Solomon // Journal of the Society for Industrial and Applied Mathematics. – 1960. – Vol. 8, No. 2. – P. 300–304. – DOI: 10.1137/0108018.",
    "mullen": "Mullen, G. L. Handbook of Finite Fields / G. L. Mullen, D. Panario. – Boca Raton : CRC Press, 2013. – 1068 p.",
    "shamir": "Shamir, A. How to share a secret / A. Shamir // Communications of the ACM. – 1979. – Vol. 22, No. 11. – P. 612–613. – DOI: 10.1145/359168.359176.",
    "fips197": "FIPS 197-upd1. Advanced Encryption Standard (AES) / National Institute of Standards and Technology. – Gaithersburg, 2023. – 41 p. – DOI: 10.6028/NIST.FIPS.197-upd1.",
    "gost3412": "ГОСТ 34.12-2018. Информационная технология. Криптографическая защита информации. Блочные шифры : межгосударственный стандарт : введён 2019-06-01. – Москва : Стандартинформ, 2018. – 21 с.",
    "fomicheva": "Fomicheva, S. G. Modification of the Berlekamp-Massey algorithm for explicable knowledge extraction by SIEM-agents / S. G. Fomicheva, S. V. Bezzateev // Journal of Physics: Conference Series. – 2022. – Vol. 2373. – Art. 052033. – DOI: 10.1088/1742-6596/2373/5/052033.",
    "sp800186": "Chen, L. Recommendations for Discrete Logarithm-based Cryptography: Elliptic Curve Domain Parameters : NIST Special Publication 800-186 / L. Chen, D. Moody, K. Randall [et al.]. – Gaithersburg : NIST, 2023. – 74 p. – DOI: 10.6028/NIST.SP.800-186.",
    "fips1865": "FIPS 186-5. Digital Signature Standard (DSS) / National Institute of Standards and Technology. – Gaithersburg, 2023. – 72 p. – DOI: 10.6028/NIST.FIPS.186-5.",
    "ntl": "Shoup, V. NTL: A Library for doing Number Theory [Электронный ресурс]. – Режим доступа: https://libntl.org (дата обращения: 15.11.2026).",
    "flint": "FLINT: Fast Library for Number Theory [Электронный ресурс]. – Режим доступа: https://flintlib.org (дата обращения: 15.11.2026).",
    "harvey2022": "Harvey, D. Polynomial multiplication over finite fields in time O(n log n) / D. Harvey, J. van der Hoeven // Journal of the ACM. – 2022. – Vol. 69, No. 2. – Art. 12. – DOI: 10.1145/3505584.",
    "msbiginteger": "BigInteger Структура (System.Numerics) [Электронный ресурс] // Microsoft Learn. – Режим доступа: https://learn.microsoft.com/ru-ru/dotnet/api/system.numerics.biginteger (дата обращения: 15.11.2026).",
    "barrett": "Barrett, P. Implementing the Rivest Shamir and Adleman public key encryption algorithm on a standard digital signal processor / P. Barrett // Advances in Cryptology – CRYPTO’86. Lecture Notes in Computer Science. Vol. 263. – Berlin : Springer, 1987. – P. 311–323.",
    "knuth": "Кнут, Д. Э. Искусство программирования. Т. 2. Получисленные алгоритмы / Д. Э. Кнут ; пер. с англ. – 3-е изд. – Москва : Вильямс, 2000. – 832 с.",
    "vzg": "von zur Gathen, J. Modern Computer Algebra / J. von zur Gathen, J. Gerhard. – 3rd ed. – Cambridge : Cambridge University Press, 2013. – 795 p.",
    "rabin": "Rabin, M. O. Probabilistic algorithm for testing primality / M. O. Rabin // Journal of Number Theory. – 1980. – Vol. 12, No. 1. – P. 128–138. – DOI: 10.1016/0022-314X(80)90084-0.",
    "shoup": "Shoup, V. A Computational Introduction to Number Theory and Algebra / V. Shoup. – 2nd ed. – Cambridge : Cambridge University Press, 2009. – 580 p.",
    "benor": "Ben-Or, M. Probabilistic algorithms in finite fields / M. Ben-Or // 22nd Annual Symposium on Foundations of Computer Science (SFCS 1981). – Nashville : IEEE, 1981. – P. 394–398. – DOI: 10.1109/SFCS.1981.37.",
    "cormen": "Кормен, Т. Х. Алгоритмы: построение и анализ / Т. Х. Кормен, Ч. И. Лейзерсон, Р. Л. Ривест, К. Штайн ; пер. с англ. – 3-е изд. – Москва : Вильямс, 2013. – 1328 с.",
    "karatsuba": "Карацуба, А. А. Умножение многозначных чисел на автоматах / А. А. Карацуба, Ю. П. Офман // Доклады Академии наук СССР. – 1962. – Т. 145, № 2. – С. 293–294.",
    "mstpl": "Библиотека параллельных задач (TPL) [Электронный ресурс] // Microsoft Learn. – Режим доступа: https://learn.microsoft.com/ru-ru/dotnet/standard/parallel-programming/task-parallel-library-tpl (дата обращения: 15.11.2026).",
    "gao": "Gao, S. Additive fast Fourier transforms over finite fields / S. Gao, T. Mateer // IEEE Transactions on Information Theory. – 2010. – Vol. 56, No. 12. – P. 6265–6272. – DOI: 10.1109/TIT.2010.2079016.",
    "bernstein": "Bernstein, D. J. Fast multiplication and its applications / D. J. Bernstein // Algorithmic Number Theory. MSRI Publications. Vol. 44. – Cambridge : Cambridge University Press, 2008. – P. 325–384.",
    "ostrowski": "Ostrowski, A. M. On two problems in abstract algebra connected with Horner’s rule / A. M. Ostrowski // Studies in Mathematics and Mechanics presented to Richard von Mises. – New York : Academic Press, 1954. – P. 40–48.",
    "pan": "Пан, В. Я. О способах вычисления значений многочленов / В. Я. Пан // Успехи математических наук. – 1966. – Т. 21, вып. 1 (127). – С. 103–134.",
    "mslibrary": "Руководство. Создание библиотеки классов .NET с помощью Visual Studio [Электронный ресурс] // Microsoft Learn. – Режим доступа: https://learn.microsoft.com/ru-ru/dotnet/core/tutorials/library-with-visual-studio (дата обращения: 15.11.2026).",
    "price": "Price, M. J. C# 12 and .NET 8 – Modern Cross-Platform Development Fundamentals / M. J. Price. – 8th ed. – Birmingham : Packt Publishing, 2023. – 828 p.",
    "albahari": "Albahari, J. C# 12 in a Nutshell: The Definitive Reference / J. Albahari. – Sebastopol : O’Reilly Media, 2023. – 1083 p.",
    "idef0": "Р 50.1.028-2001. Информационные технологии поддержки жизненного цикла продукции. Методология функционального моделирования : рекомендации по стандартизации : введены 2002-07-01. – Москва : Изд-во стандартов, 2001. – 49 с.",
    "gane": "Gane, C. Structured Systems Analysis: Tools and Techniques / C. Gane, T. Sarson. – Englewood Cliffs : Prentice Hall, 1979. – 241 p.",
    "date": "Date, C. J. Database Design and Relational Theory: Normal Forms and All That Jazz / C. J. Date. – 2nd ed. – Berkeley : Apress, 2019. – 438 p.",
    "kleppmann": "Клеппман, М. Высоконагруженные приложения. Программирование, масштабирование, поддержка / М. Клеппман ; пер. с англ. – Санкт-Петербург : Питер, 2018. – 640 с.",
    "sqlite": "Appropriate Uses For SQLite [Электронный ресурс] // SQLite. – Режим доступа: https://www.sqlite.org/whentouse.html (дата обращения: 15.11.2026).",
    "mssqlite": "Обзор Microsoft.Data.Sqlite [Электронный ресурс] // Microsoft Learn. – Режим доступа: https://learn.microsoft.com/ru-ru/dotnet/standard/data/sqlite/ (дата обращения: 15.11.2026).",
    "troelsen": "Троелсен, Э. Язык программирования C# 9 и платформа .NET 5: основные принципы и практики программирования : в 2 т. / Э. Троелсен, Ф. Джепикс ; пер. с англ. – 10-е изд. – Санкт-Петербург : Диалектика, 2022. – Т. 1. – 770 с.",
    "msgeneric": "Обобщенная математика [Электронный ресурс] // Microsoft Learn. – Режим доступа: https://learn.microsoft.com/ru-ru/dotnet/standard/generics/math (дата обращения: 15.11.2026).",
    "mswinforms": "Руководство по классическим приложениям (Windows Forms .NET) [Электронный ресурс] // Microsoft Learn. – Режим доступа: https://learn.microsoft.com/ru-ru/dotnet/desktop/winforms/ (дата обращения: 15.11.2026).",
    "martin": "Мартин, Р. Чистая архитектура. Искусство разработки программного обеспечения / Р. Мартин ; пер. с англ. – Санкт-Петербург : Питер, 2018. – 352 с.",
    "gost19701": "ГОСТ 19.701-90. Единая система программной документации. Схемы алгоритмов, программ, данных и систем. Обозначения условные и правила выполнения : межгосударственный стандарт : введён 1992-01-01. – Москва : Стандартинформ, 2010. – 23 с.",
    "orlov": "Орлов, С. А. Программная инженерия. Технологии разработки программного обеспечения : учебник для вузов / С. А. Орлов. – 5-е изд., обновл. и доп. – Санкт-Петербург : Питер, 2016. – 640 с.",
}


def title(doc):
    def line(text, size=14, bold=False, align="center", before=0, after=0, caps=False):
        p = doc.add_paragraph(); fmt(p, indent=False, align=align, before=before, after=after, spacing=1.0)
        r = p.add_run(text); r.font.size = Pt(size); r.bold = bold; r.font.name = FONT
        return p
    line("МИНИСТЕРСТВО НАУКИ И ВЫСШЕГО ОБРАЗОВАНИЯ РОССИЙСКОЙ ФЕДЕРАЦИИ", 12)
    line("Федеральное государственное автономное образовательное учреждение высшего образования", 12)
    line("«САНКТ-ПЕТЕРБУРГСКИЙ ГОСУДАРСТВЕННЫЙ УНИВЕРСИТЕТ", 12, bold=True)
    line("АЭРОКОСМИЧЕСКОГО ПРИБОРОСТРОЕНИЯ»", 12, bold=True)
    line("КАФЕДРА ТЕХНОЛОГИЙ ЗАЩИТЫ ИНФОРМАЦИИ", 12, before=18, after=24)
    line("КУРСОВАЯ РАБОТА (ПРОЕКТ)", 12, align="left")
    line("ЗАЩИЩЕНА С ОЦЕНКОЙ ____________", 12, align="left", after=6)
    line("РУКОВОДИТЕЛЬ", 12, align="left")
    t = doc.add_table(rows=2, cols=3)
    vals = [["Кандидат технических наук, профессор", "______________", "С. Г. Фомичева"],
            ["должность, уч. степень, звание", "подпись, дата", "инициалы, фамилия"]]
    for i in range(2):
        for j in range(3):
            c = t.cell(i, j); p = c.paragraphs[0]
            fmt(p, indent=False, align="center", spacing=1.0)
            r = p.add_run(vals[i][j]); r.font.size = Pt(12 if i == 0 else 10); r.font.name = FONT
    line("ПОЯСНИТЕЛЬНАЯ ЗАПИСКА К КУРСОВОМУ ПРОЕКТУ", 14, before=40, after=12)
    line("РАЗРАБОТКА ДИНАМИЧЕСКОЙ БИБЛИОТЕКИ ОПЕРАЦИЙ НАД МНОГОЧЛЕНАМИ", 14, bold=True)
    line("В ПОЛЕ ГАЛУА С ХАРАКТЕРИСТИКОЙ q", 14, bold=True, after=12)
    line("по дисциплине: Технологии и методы программирования", 14, after=60)
    line("РАБОТУ ВЫПОЛНИЛ", 12, align="left")
    t = doc.add_table(rows=2, cols=3)
    vals = [["СТУДЕНТ ГР. № 3431", "______________", "А. Ю. Машир"], ["", "подпись, дата", "инициалы, фамилия"]]
    for i in range(2):
        for j in range(3):
            c = t.cell(i, j); p = c.paragraphs[0]
            fmt(p, indent=False, align="left" if j == 0 else "center", spacing=1.0)
            r = p.add_run(vals[i][j]); r.font.size = Pt(12 if i == 0 else 10); r.font.name = FONT
    p = line("Санкт-Петербург 2026", 14, before=110)
    # содержание
    h = doc.add_paragraph(); fmt(h, indent=False, align="center", after=12)
    h.paragraph_format.page_break_before = True
    r = h.add_run("СОДЕРЖАНИЕ"); r.bold = True; r.font.size = Pt(14)
    p = doc.add_paragraph(); fmt(p, indent=False)
    add_field(p.add_run(), 'TOC \\o "1-2" \\h \\z \\u')


def sources(doc, b):
    for i, key in enumerate(b.cite_order, 1):
        p = doc.add_paragraph(); fmt(p, indent=True)
        add_inline(p, f"{i}. {SOURCES[key]}")
    unused = set(SOURCES) - set(b.cite_order)
    if unused:
        print("НЕ ЦИТИРУЮТСЯ:", unused)


def listing_files():
    groups = [("GFPolyLib", ["GfException.cs", "GaloisField.cs", "Polynomial.cs", "Fields/IFieldOps.cs",
                             "Fields/NumberTheory.cs", "Fields/PrimeFields.cs", "Fields/ExtensionField.cs",
                             "Algorithms/PolyOptions.cs", "Algorithms/PolyOps.cs", "Algorithms/PolyParser.cs"]),
              ("GFPolyApp", ["Program.cs", "UI/MainForm.cs", "UI/MainForm.Screens.cs", "UI/SimpleChart.cs",
                             "Storage/GfDatabase.cs", "Core/TaskFile.cs", "Core/Report.cs"]),
              ("GFPolyCli", ["Program.cs"]),
              ("GFPolyLib.Tests", ["PolynomialTests.cs"])]
    for proj, files in groups:
        for f in files:
            path = os.path.join(ROOT, proj, f)
            yield f"{proj}/{f}", open(path, encoding="utf-8-sig").read()


b = Builder()
content1.build(b, F)
content2.build(b, F, S)
b.h1("СПИСОК ИСПОЛЬЗОВАННЫХ ИСТОЧНИКОВ", numbered=False)
b.raw(sources)
b.h1("ПРИЛОЖЕНИЕ А. Листинг исходного кода", numbered=False)
b.p("Полный исходный код проекта, выполняемые файлы и примеры исходных данных размещены в репозитории "
    "проекта (ссылка приводится в личном кабинете АИС ГУАП). Ниже приведены исходные тексты всех модулей "
    "библиотеки, приложения, консольной утилиты и модульных тестов.")
for name, text in listing_files():
    b.code(f"Файл {name}", text)
b.h1("ПРИЛОЖЕНИЕ Б. Файл readme.txt", numbered=False)
b.code("", open(os.path.join(ROOT, "readme.txt"), encoding="utf-8").read())
import json
SPLITS = OUTDOC + ".splits.json"
if os.path.exists(SPLITS):
    b.tab_splits = json.load(open(SPLITS, encoding="utf-8"))
render(b, OUTDOC, title, SOURCES)
json.dump({"offset": b.table_offset, "map": b.table_map}, open(OUTDOC + ".map.json", "w", encoding="utf-8"), ensure_ascii=False)
print("sources:", len(b.cite_order))
print({k: v for k, v in b.labels.items() if k in ("addpoly","conv","karacost","kara","divdef","newtoniter","longdiv","newton","horner")})
