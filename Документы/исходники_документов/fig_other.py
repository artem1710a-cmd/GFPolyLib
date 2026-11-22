import os, sys, csv, textwrap
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, FancyBboxPatch, FancyArrowPatch, Polygon

plt.rcParams["font.family"] = "Times New Roman"
OUT = sys.argv[1]
BENCH = sys.argv[2]
os.makedirs(OUT, exist_ok=True)


def wrap(t, w):
    out = []
    for part in t.split("\n"):
        out += textwrap.wrap(part, w) or [""]
    return "\n".join(out)


def canvas(w, h, scale=0.62):
    fig = plt.figure(figsize=(w * scale, h * scale))
    ax = fig.add_axes([0, 0, 1, 1])
    ax.set_xlim(0, w); ax.set_ylim(0, h); ax.axis("off")
    return fig, ax


def arr(ax, pts, label="", lpos=None, fs=10, ha="center", style="-|>", lw=1.1):
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    ax.plot(xs, ys, color="k", lw=lw, zorder=1)
    ax.add_patch(FancyArrowPatch(pts[-2], pts[-1], arrowstyle=style, mutation_scale=12, color="k", lw=lw,
                                 shrinkA=0, shrinkB=0, zorder=1))
    if label:
        if lpos is None:
            i = len(pts) // 2 - 1 if len(pts) > 2 else 0
            lpos = ((pts[i][0] + pts[i + 1][0]) / 2, (pts[i][1] + pts[i + 1][1]) / 2)
        ax.text(lpos[0], lpos[1], label, fontsize=fs, ha=ha, va="center", zorder=6,
                bbox=dict(boxstyle="square,pad=0.08", fc="white", ec="none"))


# =============================================================== DFD (Гейн — Сарсон)
def dfd():
    fig, ax = canvas(16, 10.2)

    def proc(x, y, num, text, w=2.6, h=1.45):
        ax.add_patch(FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0,rounding_size=0.2", fc="white", ec="k", lw=1.5, zorder=3))
        ax.plot([x, x + w], [y + h - 0.38, y + h - 0.38], color="k", lw=1, zorder=4)
        ax.text(x + w / 2, y + h - 0.19, num, ha="center", va="center", fontsize=11, weight="bold", zorder=5)
        ax.text(x + w / 2, y + (h - 0.38) / 2, wrap(text, 20), ha="center", va="center", fontsize=11, zorder=5)

    def store(x, y, num, text, w=2.5, h=0.55):
        ax.add_patch(Rectangle((x, y), w, h, fc="#f4f4f4", ec="none", zorder=2))
        ax.plot([x + w, x, x, x + w], [y, y, y + h, y + h], color="k", lw=1.4, zorder=3)
        ax.plot([x + 0.55, x + 0.55], [y, y + h], color="k", lw=1.1, zorder=3)
        ax.text(x + 0.27, y + h / 2, num, ha="center", va="center", fontsize=11, weight="bold", zorder=4)
        ax.text(x + 0.65, y + h / 2, text, ha="left", va="center", fontsize=11, zorder=4)

    def ext(x, y, w, h, text):
        ax.add_patch(Rectangle((x + 0.08, y - 0.08), w, h, fc="#999999", ec="none", zorder=2))
        ax.add_patch(Rectangle((x, y), w, h, fc="white", ec="k", lw=1.5, zorder=3))
        ax.text(x + w / 2, y + h / 2, wrap(text, 12), ha="center", va="center", fontsize=11, weight="bold", zorder=4)

    ext(0.4, 5.2, 1.6, 1.6, "Пользова-\nтель")
    ext(0.4, 2.4, 1.6, 1.3, "Файловая система")
    ext(13.9, 8.95, 1.7, 0.8, "Принтер")
    proc(3.3, 7.0, "1", "Ввод и проверка исходных данных")
    proc(7.3, 7.0, "2", "Выполнение операций (GFPolyLib)")
    proc(11.3, 7.0, "6", "Формирование отчёта")
    proc(3.3, 4.0, "4", "Выборка из хранилища")
    proc(7.3, 4.0, "3", "Регистрация операции")
    proc(11.3, 4.0, "5", "Замер производитель-ности")
    store(2.6, 1.4, "D1", "Поля")
    store(6.0, 1.4, "D2", "Многочлены")
    store(9.4, 1.4, "D3", "Журнал операций")
    store(12.8, 1.4, "D4", "Замеры")

    arr(ax, [(2.0, 6.6), (3.3, 7.45)], "p, k, f(x), A, B, c", lpos=(2.45, 7.35), fs=9.5)
    arr(ax, [(2.0, 3.55), (3.55, 7.0)], "файл *.gfp", lpos=(2.3, 4.15), fs=9.5)
    arr(ax, [(5.9, 7.7), (7.3, 7.7)], "операнды", fs=9.5)
    arr(ax, [(9.9, 7.9), (11.3, 7.9)], "результаты", fs=9.5)
    arr(ax, [(13.9, 8.3), (14.5, 8.95)], "отчёт", lpos=(14.55, 8.55), fs=9.5)
    arr(ax, [(12.2, 8.45), (12.2, 9.55), (1.2, 9.55), (1.2, 6.8)], "результаты на экране", lpos=(6.7, 9.55), fs=9.5)
    arr(ax, [(13.9, 7.3), (15.5, 7.3), (15.5, 0.75), (1.2, 0.75), (1.2, 2.4)], "файл результатов *.txt", lpos=(8.0, 0.75), fs=9.5)
    arr(ax, [(8.6, 7.0), (8.6, 5.45)], "операнды, результат,\nалгоритм, время", lpos=(8.6, 6.25), fs=9.5)
    arr(ax, [(7.5, 4.0), (4.6, 1.95)], "поле", lpos=(6.55, 3.3), fs=9.5)
    arr(ax, [(8.2, 4.0), (7.6, 1.95)], "многочлены", lpos=(8.05, 2.95), fs=9.5)
    arr(ax, [(9.3, 4.0), (10.5, 1.95)], "запись операции", lpos=(10.0, 3.0), fs=9.5)
    arr(ax, [(6.6, 1.95), (5.4, 4.0)], "многочлены", lpos=(5.75, 2.5), fs=9.5)
    arr(ax, [(9.8, 1.95), (5.9, 4.55)], "журнал", lpos=(7.0, 3.75), fs=9.5)
    arr(ax, [(3.6, 1.95), (3.6, 4.0)], "поле", lpos=(3.6, 2.9), fs=9.5)
    arr(ax, [(2.0, 5.4), (3.3, 4.75)], "запрос", lpos=(2.55, 4.95), fs=9.5)
    arr(ax, [(4.6, 5.45), (4.6, 7.0)], "сохранённый\nмногочлен", lpos=(4.6, 6.2), fs=9.5)
    arr(ax, [(0.4, 5.5), (0.22, 5.5), (0.22, 1.1), (12.2, 1.1), (12.2, 4.0)], "параметры замера", lpos=(11.0, 1.1), fs=9.5)
    arr(ax, [(13.3, 4.0), (13.9, 1.95)], "результаты замеров", lpos=(14.3, 3.1), fs=9.5)
    arr(ax, [(12.6, 5.45), (12.6, 7.0)], "таблица и\nграфик", lpos=(12.6, 6.25), fs=9.5)
    fig.savefig(f"{OUT}/dfd.png", dpi=300); plt.close(fig)


# ================================================================== ER-модель
def er():
    fig, ax = canvas(16, 9.6)

    def table(x, y, name, rows, w=4.2):
        rh = 0.36
        h = rh * (len(rows) + 1)
        ax.add_patch(Rectangle((x, y - h), w, h, fc="white", ec="k", lw=1.4, zorder=3))
        ax.add_patch(Rectangle((x, y - rh), w, rh, fc="#dde6f0", ec="k", lw=1.4, zorder=3))
        ax.text(x + w / 2, y - rh / 2, name, ha="center", va="center", fontsize=12, weight="bold", zorder=4)
        pos = {}
        for i, (key, col, typ) in enumerate(rows):
            yy = y - rh * (i + 1.5)
            ax.text(x + 0.1, yy, key, fontsize=9.5, va="center", weight="bold", zorder=4)
            ax.text(x + 0.95, yy, col, fontsize=10.5, va="center", zorder=4,
                    weight="bold" if key == "PK" else "normal")
            ax.text(x + w - 0.1, yy, typ, fontsize=9.5, va="center", ha="right", zorder=4, color="#333333")
            pos[col] = yy
        return pos, (x, y - h, w, h)

    f, fb = table(0.5, 9.0, "fields (поля)", [("PK", "id", "INTEGER"), ("UQ", "characteristic", "TEXT"),
                                             ("UQ", "ext_degree", "INTEGER"), ("UQ", "modulus", "TEXT"),
                                             ("", "created_at", "TEXT")])
    p, pb = table(5.9, 9.0, "polynomials (многочлены)", [("PK", "id", "INTEGER"), ("FK,UQ", "field_id", "INTEGER"),
                  ("", "degree", "INTEGER"), ("", "coefficients", "TEXT"), ("UQ", "coeff_hash", "TEXT"),
                  ("", "source", "TEXT"), ("", "title", "TEXT"), ("", "created_at", "TEXT")])
    o, ob = table(11.3, 9.0, "operations (журнал)", [("PK", "id", "INTEGER"), ("", "op_code", "TEXT"),
                  ("FK", "operand_a", "INTEGER"), ("FK", "operand_b", "INTEGER"), ("", "point", "TEXT"),
                  ("FK", "result_id", "INTEGER"), ("FK", "remainder_id", "INTEGER"), ("", "value", "TEXT"),
                  ("", "algorithm", "TEXT"), ("", "elapsed_ms", "REAL"), ("", "performed_at", "TEXT")])
    b, bb = table(0.5, 4.4, "benchmarks (замеры)", [("PK", "id", "INTEGER"), ("FK", "field_id", "INTEGER"),
                  ("", "op_code", "TEXT"), ("", "algorithm", "TEXT"), ("", "size", "INTEGER"),
                  ("", "elapsed_ms", "REAL"), ("", "measured_at", "TEXT")])

    def rel(x1, y1, x2, y2, many_at_end=True, label=""):
        ax.plot([x1, x2], [y1, y2], color="k", lw=1.1, zorder=1)
        # «один» — двойная черта у начала, «многие» — «воронья лапка» у конца
        dx = 0.18 if x2 > x1 else -0.18
        if abs(y2 - y1) < 1e-6:
            ax.plot([x1 + dx, x1 + dx], [y1 - 0.13, y1 + 0.13], color="k", lw=1.1)
            ax.plot([x1 + 2 * dx, x1 + 2 * dx], [y1 - 0.13, y1 + 0.13], color="k", lw=1.1)
            ax.plot([x2 - 2 * dx, x2], [y2, y2 + 0.15], color="k", lw=1.1)
            ax.plot([x2 - 2 * dx, x2], [y2, y2 - 0.15], color="k", lw=1.1)
        if label:
            ax.text((x1 + x2) / 2, y1 + 0.12, label, ha="center", va="bottom", fontsize=9.5)

    rel(4.7, f["id"], 5.9, f["id"], label="1 : N")
    for col in ["operand_a", "operand_b", "result_id", "remainder_id"]:
        ax.plot([10.1, 10.7], [p["id"], o[col]], color="k", lw=1.0)
        ax.plot([10.7, 11.3], [o[col], o[col]], color="k", lw=1.0)
        ax.plot([11.0, 11.3], [o[col], o[col] + 0.13], color="k", lw=1.0)
        ax.plot([11.0, 11.3], [o[col], o[col] - 0.13], color="k", lw=1.0)
    ax.plot([10.28, 10.28], [p["id"] - 0.13, p["id"] + 0.13], color="k", lw=1.0)
    ax.plot([10.4, 10.4], [p["id"] - 0.13, p["id"] + 0.13], color="k", lw=1.0)
    ax.plot([10.1, 10.45], [p["id"], p["id"]], color="k", lw=1.0)
    ax.text(10.55, 7.0, "1 : N\n(4 роли)", fontsize=9.5, ha="center")
    # fields -> benchmarks
    ax.plot([0.3, 0.3], [f["id"], b["field_id"]], color="k", lw=1.1)
    ax.plot([0.3, 0.5], [f["id"], f["id"]], color="k", lw=1.1)
    ax.plot([0.3, 0.5], [b["field_id"], b["field_id"]], color="k", lw=1.1)
    ax.plot([0.5, 0.3], [b["field_id"], b["field_id"] + 0.13], color="k", lw=1.0)
    ax.plot([0.5, 0.3], [b["field_id"], b["field_id"] - 0.13], color="k", lw=1.0)
    ax.plot([0.22, 0.38], [f["id"] - 0.25, f["id"] - 0.25], color="k", lw=1.0)
    ax.plot([0.22, 0.38], [f["id"] - 0.37, f["id"] - 0.37], color="k", lw=1.0)
    ax.text(0.15, 5.6, "1 : N", rotation=90, fontsize=9.5, va="center", ha="center")
    ax.text(5.9, 4.1, "Обозначения: PK — первичный ключ, FK — внешний ключ, UQ — входит в ограничение уникальности;\n"
                      "связь «один ко многим»: две черты — сторона «один», «воронья лапка» — сторона «многие».",
            fontsize=10, va="top")
    fig.savefig(f"{OUT}/er.png", dpi=300, bbox_inches="tight", pad_inches=0.1); plt.close(fig)


# =========================================================== диаграмма классов
def classes():
    fig, ax = canvas(16, 11.6)

    def cls(x, y, name, attrs, ops, w=4.6, stereo=None):
        lh = 0.3
        hn = 0.42 + (0.28 if stereo else 0)
        ha = lh * max(1, len(attrs)) + 0.1
        ho = lh * max(1, len(ops)) + 0.1
        h = hn + ha + ho
        ax.add_patch(Rectangle((x, y - h), w, h, fc="white", ec="k", lw=1.3, zorder=3))
        ax.add_patch(Rectangle((x, y - hn), w, hn, fc="#e8eef6", ec="k", lw=1.3, zorder=3))
        if stereo:
            ax.text(x + w / 2, y - 0.18, stereo, ha="center", va="center", fontsize=9, zorder=4)
        ax.text(x + w / 2, y - hn + 0.2, name, ha="center", va="center", fontsize=11, weight="bold", zorder=4,
                style="italic" if stereo in ("«abstract»", "«interface»") else "normal")
        ax.plot([x, x + w], [y - hn - ha] * 2, color="k", lw=1.0, zorder=4)
        for i, a in enumerate(attrs):
            ax.text(x + 0.08, y - hn - 0.05 - lh * (i + 0.5), a, fontsize=9, va="center", zorder=4)
        for i, o in enumerate(ops):
            ax.text(x + 0.08, y - hn - ha - 0.05 - lh * (i + 0.5), o, fontsize=9, va="center", zorder=4)
        return (x, y - h, w, h)

    gf = cls(0.3, 11.3, "GaloisField", ["+Characteristic: BigInteger", "+ExtensionDegree: int", "+Order: BigInteger",
                                         "+Options: PolyOptions", "+Modulus, Implementation: string"],
             ["+Create(p, k, modulus): GaloisField", "+Parse(text): Polynomial", "+Random(degree, seed): Polynomial",
              "+NormalizeElement(text): string"], stereo="«abstract»")
    fi = cls(0.3, 5.9, "FieldImpl<T, TOps>", ["-ops: TOps"], ["#Mul(a, b, ct), DivRem(a, b, ct)", "#Evaluate(a, c, ct)",
                                                           "-Guard(action)"])
    po = cls(5.6, 11.3, "Polynomial", ["+Field: GaloisField", "+Degree: int", "~Data: object"],
             ["+Add, Subtract, Multiply(b, ct)", "+DivRem(b, ct): (Q, R)", "+Evaluate(c, ct): string",
              "+ToAlgebraic(), ToCoefficients()", "+операторы + − * / %"])
    op = cls(5.6, 7.3, "PolyOptions", ["+Multiplication: MulAlgorithm", "+Division: DivAlgorithm",
                                       "+KaratsubaThreshold, NewtonThreshold", "+AllowParallel: bool"],
             ["+ResolveMultiplication(n, m)", "+ResolveDivision(n, m)"])
    ex = cls(5.6, 3.3, "GfException", ["+Code: GfError"], ["+Describe(code): string"], w=3.4)
    io = cls(11.0, 11.3, "IFieldOps<T>", [], ["+Add, Sub, Neg, Mul, Inv", "+FromInteger, Random",
                                             "+ParseElement, Format"], w=4.6, stereo="«interface»")
    p64 = cls(10.3, 8.4, "PrimeField64", ["-p: ulong"], ["UInt128-произведение"], w=2.6, stereo="«struct»")
    pbg = cls(13.1, 8.4, "PrimeFieldBig", ["-p: BigInteger"], ["длинная арифметика"], w=2.6, stereo="«struct»")
    ext = cls(11.0, 6.2, "ExtensionField<TB, TBOps>", ["-f: TB[] (модуль)", "-k: int"],
              ["+Mul: умножение mod f(a)", "+Inv: расширенный Евклид"], w=4.6, stereo="«struct»")
    pops = cls(10.3, 3.5, "PolyOps<T, TOps>", [], ["MulSchool, MulKaratsuba", "DivRemLong, DivRemNewton",
                                                    "Evaluate, Gcd, IsIrreducible"], w=3.2, stereo="«static»")
    nt = cls(13.7, 3.5, "NumberTheory", [], ["IsProbablePrime", "ModInverse", "ParseIntegerExpression"],
             w=2.0 + 0.4, stereo="«static»")
    pp = cls(0.3, 2.6, "PolyParser / PolyFormatter", [], ["Parse<T, TOps>(text, f, var)", "Algebraic, Coefficients"],
             w=4.6, stereo="«static»")

    def inherit(child_top, parent_bottom):
        ax.plot([child_top[0], parent_bottom[0]], [child_top[1], parent_bottom[1] - 0.25], color="k", lw=1.1)
        tri = Polygon([(parent_bottom[0] - 0.15, parent_bottom[1] - 0.25), (parent_bottom[0] + 0.15, parent_bottom[1] - 0.25),
                       (parent_bottom[0], parent_bottom[1])], closed=True, fc="white", ec="k", lw=1.1, zorder=5)
        ax.add_patch(tri)

    def realize(child_top, parent_bottom):
        ax.plot([child_top[0], parent_bottom[0]], [child_top[1], parent_bottom[1] - 0.25], color="k", lw=1.1, ls="--")
        ax.add_patch(Polygon([(parent_bottom[0] - 0.15, parent_bottom[1] - 0.25), (parent_bottom[0] + 0.15, parent_bottom[1] - 0.25),
                              (parent_bottom[0], parent_bottom[1])], closed=True, fc="white", ec="k", lw=1.1, zorder=5))

    def top(b, f=0.5): return (b[0] + b[2] * f, b[1] + b[3])
    def bot(b, f=0.5): return (b[0] + b[2] * f, b[1])
    def left(b, f=0.5): return (b[0], b[1] + b[3] * f)
    def right(b, f=0.5): return (b[0] + b[2], b[1] + b[3] * f)

    inherit(top(fi), bot(gf))
    realize(top(p64), bot(io, 0.3))
    realize(top(pbg), bot(io, 0.7))
    realize((13.0, top(ext)[1]), (13.0, bot(io)[1]))
    arr(ax, [right(gf, 0.75), left(po, 0.7)], "создаёт", fs=9, style="->")
    arr(ax, [left(po, 0.35), right(gf, 0.35)], "Field", fs=9, style="->")
    arr(ax, [right(fi, 0.5), (5.3, right(fi, 0.5)[1]), (5.3, left(op, 0.5)[1]), left(op, 0.5)], "Options", fs=9, style="->")
    arr(ax, [right(fi, 0.2), (10.0, right(fi, 0.2)[1]), (10.0, left(pops, 0.5)[1]), left(pops, 0.5)], "вызывает", fs=9, style="->", )
    arr(ax, [bot(fi, 0.5), top(pp, 0.5)], "разбор и вывод", fs=9, style="->")
    arr(ax, [bot(ext, 0.3), (bot(ext, 0.3)[0], top(pops)[1])], "использует", fs=9, style="->")
    arr(ax, [bot(fi, 0.85), (bot(fi, 0.85)[0], 2.9), left(ex, 0.6)], "throw", fs=9, style="->")
    fig.savefig(f"{OUT}/classes.png", dpi=300); plt.close(fig)


# ========================================================= диаграмма компонентов
def components():
    fig, ax = canvas(16, 7.4)

    def comp(x, y, w, h, title, sub, fc="white"):
        ax.add_patch(Rectangle((x, y), w, h, fc=fc, ec="k", lw=1.4, zorder=3))
        ax.add_patch(Rectangle((x + w - 0.55, y + h - 0.45), 0.4, 0.28, fc="white", ec="k", lw=0.9, zorder=4))
        ax.add_patch(Rectangle((x + w - 0.62, y + h - 0.4), 0.15, 0.07, fc="white", ec="k", lw=0.8, zorder=5))
        ax.add_patch(Rectangle((x + w - 0.62, y + h - 0.29), 0.15, 0.07, fc="white", ec="k", lw=0.8, zorder=5))
        ax.text(x + w / 2, y + h - 0.5, title, ha="center", va="center", fontsize=12, weight="bold", zorder=4)
        ax.text(x + w / 2, y + (h - 0.8) / 2, sub, ha="center", va="center", fontsize=10, zorder=4, linespacing=1.25)

    comp(5.6, 2.6, 4.8, 2.4, "GFPolyLib.dll", "динамическая библиотека (.NET 8)\nGaloisField, Polynomial, PolyOptions,\nPolyOps, NumberTheory,\nPrimeField64/Big, ExtensionField", fc="#eef3fa")
    comp(0.4, 5.0, 4.4, 2.0, "GFPolyApp.exe", "приложение Windows Forms\nMainForm, TaskFile, ReportBuilder,\nTextPrinter, SimpleChart")
    comp(0.4, 0.5, 4.4, 1.8, "GFPolyCli (gfpoly.exe)", "консольная утилита:\nпакетные вычисления, замеры")
    comp(11.2, 5.0, 4.4, 2.0, "GFPolyLib.Tests.dll", "модульные тесты xUnit\n(89 тестов)")
    comp(11.2, 0.5, 4.4, 2.2, "Microsoft.Data.Sqlite", "провайдер ADO.NET для SQLite\n→ файл gfpoly.db\n(%LOCALAPPDATA%\\GFPolyLib)")
    arr(ax, [(4.8, 5.8), (5.6, 4.6)], "ссылка на сборку", lpos=(5.3, 5.55), fs=9.5, style="->")
    arr(ax, [(4.8, 1.4), (5.6, 3.0)], "ссылка на сборку", lpos=(5.2, 1.95), fs=9.5, style="->")
    arr(ax, [(11.2, 5.8), (10.4, 4.6)], "тестирует", lpos=(10.75, 5.55), fs=9.5, style="->")
    arr(ax, [(2.6, 5.0), (2.6, 2.45), (11.2, 2.45)], "GfDatabase: SQL-запросы", lpos=(8.6, 2.45), fs=9.5, style="->")
    ax.text(2.6, 3.8, "файлы *.gfp, *.txt,\nпринтер", ha="center", fontsize=9.5, bbox=dict(fc="white", ec="none"))
    fig.savefig(f"{OUT}/components.png", dpi=300); plt.close(fig)


# ============================================================== блок-схемы
class Flow:
    def __init__(self, w, h):
        self.fig, self.ax = canvas(w, h, scale=0.55)
        self.n = {}

    def node(self, key, kind, x, y, text, w=4.2, h=0.9):
        ax = self.ax
        if kind == "term":
            ax.add_patch(FancyBboxPatch((x - w / 2, y - h / 2), w, h, boxstyle=f"round,pad=0,rounding_size={h/2}", fc="white", ec="k", lw=1.3, zorder=3))
        elif kind == "proc":
            ax.add_patch(Rectangle((x - w / 2, y - h / 2), w, h, fc="white", ec="k", lw=1.3, zorder=3))
        elif kind == "sub":
            ax.add_patch(Rectangle((x - w / 2, y - h / 2), w, h, fc="white", ec="k", lw=1.3, zorder=3))
            ax.plot([x - w / 2 + 0.2] * 2, [y - h / 2, y + h / 2], color="k", lw=1.1, zorder=4)
            ax.plot([x + w / 2 - 0.2] * 2, [y - h / 2, y + h / 2], color="k", lw=1.1, zorder=4)
        elif kind == "io":
            s = 0.3
            ax.add_patch(Polygon([(x - w / 2 + s, y + h / 2), (x + w / 2 + s, y + h / 2), (x + w / 2 - s, y - h / 2), (x - w / 2 - s, y - h / 2)],
                                 closed=True, fc="white", ec="k", lw=1.3, zorder=3))
        elif kind == "dec":
            ax.add_patch(Polygon([(x, y + h / 2), (x + w / 2, y), (x, y - h / 2), (x - w / 2, y)], closed=True, fc="white", ec="k", lw=1.3, zorder=3))
        ax.text(x, y, text, ha="center", va="center", fontsize=10.5, zorder=5, linespacing=1.15)
        self.n[key] = (x, y, w, h)

    def P(self, key, side):
        x, y, w, h = self.n[key]
        return {"t": (x, y + h / 2), "b": (x, y - h / 2), "l": (x - w / 2, y), "r": (x + w / 2, y)}[side]

    def line(self, pts, label=None, lpos=None):
        arr(self.ax, pts, label or "", lpos=lpos, fs=10, style="-|>")

    def down(self, a, b):
        self.line([self.P(a, "b"), self.P(b, "t")])

    def save(self, name):
        self.fig.savefig(f"{OUT}/{name}", dpi=300, bbox_inches="tight", pad_inches=0.1); plt.close(self.fig)


def flow_operation():
    f = Flow(11.4, 17.2)
    X = 4.6
    f.node("s", "term", X, 16.6, "Начало", w=2.6, h=0.7)
    f.node("in", "io", X, 15.5, "Ввод p, k, f(x), A(x), B(x), c\n(клавиатура, файл *.gfp, хранилище)", w=5.4, h=1.0)
    f.node("cf", "sub", X, 14.2, "Создание поля GaloisField.Create:\nпроверка простоты p, неприводимости f(x)", w=5.6, h=1.0)
    f.node("okf", "dec", X, 12.85, "Поле\nсоздано?", w=2.8, h=1.2)
    f.node("pa", "sub", X, 11.45, "Разбор A(x), B(x), c\n(PolyParser)", w=4.6, h=0.95)
    f.node("okp", "dec", X, 10.1, "Запись\nверна?", w=2.8, h=1.2)
    f.node("alg", "proc", X, 8.7, "Выбор алгоритма по длинам n, m\n(PolyOptions.Resolve…)", w=5.0, h=0.95)
    f.node("op", "sub", X, 7.35, "Выполнение операции в фоновом\nпотоке (PolyOps)", w=5.0, h=0.95)
    f.node("okc", "dec", X, 6.0, "Прервана или\nошибка?", w=3.2, h=1.3)
    f.node("log", "proc", X, 4.55, "Запись операндов, результата,\nалгоритма и времени в хранилище", w=5.2, h=0.95)
    f.node("out", "io", X, 3.25, "Вывод результата на экран,\nв файл, на принтер", w=4.8, h=0.95)
    f.node("e", "term", X, 2.1, "Конец", w=2.6, h=0.7)
    f.node("err", "io", 8.3, 11.45, "Сообщение\nоб ошибке", w=2.4, h=0.95)
    for a, b in [("s", "in"), ("in", "cf"), ("cf", "okf"), ("okf", "pa"), ("pa", "okp"), ("okp", "alg"), ("alg", "op"),
                 ("op", "okc"), ("okc", "log"), ("log", "out"), ("out", "e")]:
        f.down(a, b)
    f.ax.text(X + 0.15, 12.1, "да", fontsize=10); f.ax.text(X + 0.15, 9.35, "да", fontsize=10); f.ax.text(X + 0.15, 5.2, "нет", fontsize=10)
    f.line([f.P("okf", "r"), (9.0, 12.85), f.P("err", "t")], "нет", lpos=(6.6, 13.05))
    f.line([f.P("okp", "r"), (9.0, 10.1), f.P("err", "b")], "нет", lpos=(6.6, 10.3))
    f.line([f.P("okc", "r"), (10.8, 6.0), (10.8, 15.5), f.P("in", "r")], "да", lpos=(7.0, 6.2))
    f.ax.plot([f.P("err", "r")[0], 10.8], [11.45, 11.45], color="k", lw=1.1)
    f.save("flow_operation.png")


def flow_karatsuba():
    f = Flow(11, 17.2)
    X = 4.8
    f.node("s", "term", X, 16.6, "KMul(A, B, n)", w=3.2, h=0.7)
    f.node("d", "dec", X, 15.3, "n ≤ n0 ?", w=2.8, h=1.1)
    f.node("sch", "proc", 9.0, 15.3, "Школьное\nумножение (2.7)", w=3.0, h=0.95)
    f.node("h", "proc", X, 13.9, "h = n div 2; A = A1·x^h + A0;\nB = B1·x^h + B0", w=5.0, h=0.95)
    f.node("sum", "proc", X, 12.6, "SA = A0 + A1;  SB = B0 + B1", w=5.0, h=0.8)
    f.node("par", "dec", X, 11.3, "n ≥ порога\nраспараллели-\nвания?", w=3.6, h=1.5)
    f.node("p3", "sub", 9.0, 9.8, "Parallel.Invoke:\nтри ветви\nодновременно", w=3.2, h=1.25)
    f.node("z", "sub", X, 9.8, "Z0 = KMul(A0, B0)\nZ2 = KMul(A1, B1)\nZ1 = KMul(SA, SB)", w=4.2, h=1.25)
    f.node("mid", "proc", X, 8.25, "Z1 = Z1 − Z0 − Z2", w=4.2, h=0.8)
    f.node("res", "proc", X, 6.95, "C = Z2·x^(2h) + Z1·x^h + Z0", w=5.0, h=0.8)
    f.node("e", "term", X, 5.7, "Возврат C", w=3.0, h=0.7)
    for a, b in [("s", "d"), ("h", "sum"), ("sum", "par"), ("z", "mid"), ("mid", "res"), ("res", "e")]:
        f.down(a, b)
    f.line([f.P("d", "b"), f.P("h", "t")]); f.ax.text(X + 0.15, 14.55, "нет", fontsize=10)
    f.line([f.P("d", "r"), f.P("sch", "l")], "да", lpos=(6.8, 15.5))
    f.line([f.P("sch", "r"), (10.8, 15.3), (10.8, 5.7), f.P("e", "r")])
    f.line([f.P("par", "b"), f.P("z", "t")]); f.ax.text(X + 0.15, 10.5, "нет", fontsize=10)
    f.line([f.P("par", "r"), (9.0, 11.3), f.P("p3", "t")], "да", lpos=(7.2, 11.5))
    f.line([f.P("p3", "b"), (9.0, 8.25), f.P("mid", "r")])
    f.save("flow_karatsuba.png")


def flow_division():
    f = Flow(11, 18.4)
    X = 4.8
    f.node("s", "term", X, 17.8, "DivRem(A, B)", w=3.2, h=0.7)
    f.node("z", "dec", X, 16.55, "B = 0 ?", w=2.6, h=1.0)
    f.node("ez", "io", 9.0, 16.55, "Ошибка\nDivisionByZero", w=3.0, h=0.95)
    f.node("lt", "dec", X, 15.1, "deg A < deg B ?", w=3.6, h=1.1)
    f.node("rq", "proc", 9.0, 15.1, "Q = 0, R = A", w=2.8, h=0.8)
    f.node("ch", "dec", X, 13.6, "n−m+1 ≥ N0 и\nm ≥ N0/2 ?", w=3.8, h=1.3)
    f.node("rev", "proc", X, 12.05, "Ã = rev(A) mod x^(n−m+1);\nB̃ = rev(B)", w=5.0, h=0.95)
    f.node("inv", "sub", X, 10.75, "G = B̃^(−1) mod x^(n−m+1)\n(итерации Ньютона)", w=5.0, h=0.95)
    f.node("q", "proc", X, 9.45, "Q = rev(Ã·G mod x^(n−m+1))", w=5.0, h=0.8)
    f.node("r", "proc", X, 8.25, "R = A − B·Q", w=3.4, h=0.8)
    f.node("long", "sub", 9.0, 11.4, "Деление\n«уголком»\n(2.11)", w=2.8, h=1.3)
    f.node("e", "term", X, 6.95, "Возврат (Q, R)", w=3.4, h=0.7)
    for a, b in [("s", "z"), ("rev", "inv"), ("inv", "q"), ("q", "r"), ("r", "e")]:
        f.down(a, b)
    f.line([f.P("z", "b"), f.P("lt", "t")]); f.ax.text(X + 0.15, 15.85, "нет", fontsize=10)
    f.line([f.P("z", "r"), f.P("ez", "l")], "да", lpos=(6.6, 16.75))
    f.line([f.P("lt", "b"), f.P("ch", "t")]); f.ax.text(X + 0.15, 14.4, "нет", fontsize=10)
    f.line([f.P("lt", "r"), f.P("rq", "l")], "да", lpos=(6.8, 15.3))
    f.line([f.P("rq", "r"), (10.7, 15.1), (10.7, 6.95), f.P("e", "r")])
    f.line([f.P("ch", "b"), f.P("rev", "t")]); f.ax.text(X + 0.15, 12.75, "да", fontsize=10)
    f.line([f.P("ch", "r"), (9.0, 13.6), f.P("long", "t")], "нет", lpos=(7.3, 13.8))
    f.line([f.P("long", "b"), (9.0, 7.6), (X + 0.05, 7.6)])
    f.save("flow_division.png")


# =================================================================== графики
def read_csv(name):
    rows = {}
    with open(os.path.join(BENCH, name), encoding="utf-8-sig") as fh:
        for r in csv.DictReader(fh, delimiter=";"):
            rows.setdefault(r["algorithm"], []).append((int(r["n"]), float(r["ms"])))
    return rows


NAMES = {"school": "школьный", "karatsuba": "Карацубы", "long": "«уголком»", "newton": "Ньютона"}
STY = {"school": dict(color="#2a78c8", marker="o"), "karatsuba": dict(color="#dc6e28", marker="s"),
       "long": dict(color="#2a78c8", marker="o"), "newton": dict(color="#3c9a5a", marker="s")}


def chart(files, name, title=None, xlabel="Число коэффициентов n", logy=False, titles=None):
    fig, axes = plt.subplots(1, len(files), figsize=(6.6 * len(files) * 0.75, 4.2))
    if len(files) == 1:
        axes = [axes]
    for i, (fn, ax) in enumerate(zip(files, axes)):
        data = read_csv(fn)
        for alg, pts in data.items():
            pts.sort()
            ax.plot([p[0] for p in pts], [p[1] for p in pts], label=NAMES[alg], lw=1.8, ms=4.5, **STY[alg])
        ax.set_xlabel(xlabel, fontsize=11)
        ax.set_ylabel("Время, мс", fontsize=11)
        if logy: ax.set_yscale("log")
        ax.grid(alpha=0.35)
        ax.legend(fontsize=10)
        if titles: ax.set_title(titles[i], fontsize=11)
    fig.tight_layout()
    fig.savefig(f"{OUT}/{name}", dpi=300); plt.close(fig)


def ops_count():
    import functools
    @functools.lru_cache(None)
    def school(n): return n * n + (n - 1) * (n - 1)
    @functools.lru_cache(None)
    def kar(n, n0):
        if n <= n0: return school(n)
        h = n // 2; hi = n - h
        return kar(h, n0) + 2 * kar(hi, n0) + 2 * h + (2 * h - 1) + (2 * hi - 1) * 2 + (2 * hi - 1)
    ns = [2 ** i for i in range(2, 14)]
    fig, ax = plt.subplots(figsize=(6.6, 4.2))
    ax.plot(ns, [school(n) for n in ns], "o-", label="школьный", color="#2a78c8")
    ax.plot(ns, [kar(n, 1) for n in ns], "s--", label="Карацубы (до n = 1)", color="#999999")
    ax.plot(ns, [kar(n, 4) for n in ns], "s-", label="гибридный, n0 = 4", color="#dc6e28")
    ax.set_xscale("log", base=2); ax.set_yscale("log")
    ax.set_xlabel("Число коэффициентов n", fontsize=11); ax.set_ylabel("Число операций в поле", fontsize=11)
    ax.grid(alpha=0.35, which="both"); ax.legend(fontsize=10)
    fig.tight_layout(); fig.savefig(f"{OUT}/ops_count.png", dpi=300); plt.close(fig)
    return {n: school(n) / kar(n, 4) for n in ns}


dfd(); er(); classes(); components()
flow_operation(); flow_karatsuba(); flow_division()
chart(["mul64_1.csv"], "bench_mul64.png")
chart(["mulbig_1.csv", "mulext_1.csv"], "bench_mul_other.png", titles=["GF(2^127 − 1)", "GF(2^8)"])
chart(["div64.csv", "divbig.csv"], "bench_div.png", xlabel="Степень делителя n (делимое — 2n)",
      titles=["GF(998244353)", "GF(2^127 − 1)"])
# ускорение за счёт параллелизма
single = dict(read_csv("mul64_1.csv")["karatsuba"] + read_csv("mul64_1big.csv")["karatsuba"])
par = dict(read_csv("mul64_par.csv")["karatsuba"])
ns = sorted(set(single) & set(par))
fig, ax = plt.subplots(figsize=(6.6, 4.0))
xs = range(len(ns))
ax.bar([x - 0.2 for x in xs], [single[n] for n in ns], 0.4, label="1 поток", color="#2a78c8")
ax.bar([x + 0.2 for x in xs], [par[n] for n in ns], 0.4, label="все ядра", color="#dc6e28")
ax.set_xticks(list(xs)); ax.set_xticklabels([str(n) for n in ns])
ax.set_yscale("log"); ax.set_xlabel("Число коэффициентов n", fontsize=11); ax.set_ylabel("Время, мс", fontsize=11)
ax.grid(alpha=0.35, axis="y"); ax.legend(fontsize=10)
fig.tight_layout(); fig.savefig(f"{OUT}/bench_parallel.png", dpi=300); plt.close(fig)
r = ops_count()
print({n: round(v, 2) for n, v in r.items()})
print("speedup", {n: round(single[n] / par[n], 1) for n in ns})
