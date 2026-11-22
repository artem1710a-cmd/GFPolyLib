"""Построитель пояснительной записки по требованиям ГОСТ 7.32 / методических указаний кафедры 33."""
import re, copy
from docx import Document
from docx.shared import Pt, Cm, Emu
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_LINE_SPACING, WD_BREAK, WD_TAB_ALIGNMENT
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.enum.section import WD_SECTION
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
from lxml import etree
import latex2mathml.converter

XSL = etree.XSLT(etree.parse(r"C:/Program Files/Microsoft Office/root/Office16/MML2OMML.XSL"))
FONT = "Times New Roman"


def omml(latex):
    mml = latex2mathml.converter.convert(latex)
    res = XSL(etree.fromstring(mml.encode()))
    el = res.getroot()
    # шрифт формул: Cambria Math по умолчанию; кегль 14 задаётся абзацем
    return el


class Builder:
    def __init__(self):
        self.blocks = []  # (kind, payload)
        self.counters = {}
        self.labels = {}
        self.cites = {}
        self.cite_order = []
        self.used_refs = set()
        self.tab_splits = {}   # метка таблицы -> индексы строк, с которых начинается «Продолжение таблицы»
        self.table_map = []    # (метка, номер части, первая строка данных, есть ли строка с номерами граф)

    # ------------------------------------------------------------ API разметки
    def h1(self, text, numbered=True): self.blocks.append(("h1", (text, numbered)))
    def h2(self, text): self.blocks.append(("h2", text))
    def p(self, text, **kw): self.blocks.append(("p", (text, kw)))
    def defn(self, label, text): self.blocks.append(("def", (label, text)))
    def ol(self, items): self.blocks.append(("ol", items))
    def ul(self, items): self.blocks.append(("ul", items))
    def eq(self, latex, label=None): self.blocks.append(("eq", (latex, label)))
    def fig(self, path, caption, label, width=16.0, landscape=False):
        self.blocks.append(("fig", (path, caption, label, width, landscape)))
    def tab(self, caption, label, header, rows, widths, fs=12, align=None):
        self.blocks.append(("tab", (caption, label, header, rows, widths, fs, align)))
    def code(self, title, text): self.blocks.append(("code", (title, text)))
    def pagebreak(self): self.blocks.append(("pb", None))
    def raw(self, fn): self.blocks.append(("raw", fn))

    # ------------------------------------------------------------ нумерация
    def number(self):
        """Сквозная нумерация рисунков, таблиц и формул (ГОСТ 7.32); в приложениях – А.1, А.2 …"""
        cnt = {"fig": 0, "tab": 0, "eq": 0}
        appendix = None
        for kind, pl in self.blocks:
            if kind == "h1":
                text, numbered = pl
                if text.startswith("ПРИЛОЖЕНИЕ"):
                    appendix = text.split()[1].rstrip(".")
                    cnt = {"fig": 0, "tab": 0, "eq": 0}
            elif kind in ("fig", "tab", "eq"):
                label = pl[2] if kind == "fig" else pl[1]
                if kind == "eq" and label is None:
                    continue
                cnt[kind] += 1
                self.labels[label] = f"{appendix}.{cnt[kind]}" if appendix else str(cnt[kind])

    def resolve(self, text):
        def rep_ref(m):
            key, kind, prep = m.group("key"), m.group("kind"), m.group("prep") or ""
            if key not in self.labels:
                raise KeyError(key)
            self.used_refs.add(key)
            v = self.labels[key]
            if kind == "eq":
                return f"{prep}({v})"
            # ГОСТ 7.32: в ссылках слово «рисунок»/«таблица» пишется полностью
            word = {"fig": ("рисунок", "рисунке"), "tab": ("таблица", "таблице")}[kind][1 if prep else 0]
            return f"{prep}{word} {v}"
        text = re.sub(r"(?P<prep>(?<!\w)(?:[Нн]а|[Вв])\s)?\{(?P<kind>fig|tab|eq):(?P<key>[\w\-]+)\}", rep_ref, text)

        def rep_cite(m):
            keys = [k.strip().lstrip("@") for k in m.group(1).split(",")]
            nums = []
            for k in keys:
                if k not in self.cites:
                    self.cite_order.append(k)
                    self.cites[k] = len(self.cite_order)
                nums.append(self.cites[k])
            nums.sort()
            # сжатие диапазонов 3, 4, 5 -> 3–5
            out, i = [], 0
            while i < len(nums):
                j = i
                while j + 1 < len(nums) and nums[j + 1] == nums[j] + 1:
                    j += 1
                out.append(f"{nums[i]}–{nums[j]}" if j - i >= 2 else ", ".join(str(n) for n in nums[i:j + 1]))
                i = j + 1
            return "[" + ", ".join(out) + "]"
        return re.sub(r"\[@([\w\-, @]+)\]", rep_cite, text)


# =================================================================== рендер

def set_cell_margins(cell, top=40, bottom=40, left=80, right=80):
    tcPr = cell._tc.get_or_add_tcPr()
    mar = OxmlElement("w:tcMar")
    for k, v in (("top", top), ("bottom", bottom), ("start", left), ("end", right)):
        e = OxmlElement(f"w:{k}")
        e.set(qn("w:w"), str(v)); e.set(qn("w:type"), "dxa")
        mar.append(e)
    tcPr.append(mar)


def add_field(run, instr):
    f1 = OxmlElement("w:fldChar"); f1.set(qn("w:fldCharType"), "begin")
    it = OxmlElement("w:instrText"); it.set(qn("xml:space"), "preserve"); it.text = instr
    f2 = OxmlElement("w:fldChar"); f2.set(qn("w:fldCharType"), "separate")
    t = OxmlElement("w:t"); t.text = "1"
    f3 = OxmlElement("w:fldChar"); f3.set(qn("w:fldCharType"), "end")
    for e in (f1, it, f2, t, f3):
        run._r.append(e)


H2_SHIFT = 0.5  # два знака кегля 14 пт ≈ 0,5 см


def heading_text(text):
    """«2.1. Заголовок» -> «2.1 Заголовок»: после номера раздела/подраздела точку не ставят (ГОСТ 7.32)."""
    return re.sub(r"^(\d+(?:\.\d+)*)\.\s+", r"\1 ", text)


INLINE = re.compile(r"(\*\*.+?\*\*|\$.+?\$)")


SCRIPT = re.compile(r"\^(\((?:[^()]|\([^()]*\))*\)|[0-9A-Za-z]+(?:,[0-9]+)?)|(?<![A-Za-z0-9])([A-Za-z])_(\([^)]*\)|[0-9a-zA-Z]+)")


def scripts(text):
    out, pos = [], 0
    for m in SCRIPT.finditer(text):
        out.append(("n", text[pos:m.start()]))
        if m.group(1) is not None:
            g = m.group(1)
            if g.startswith("(") and g.endswith(")"): g = g[1:-1]
            out.append(("sup", g))
        else:
            out.append(("n", m.group(2)))
            g = m.group(3)
            if g.startswith("(") and g.endswith(")"): g = g[1:-1]
            out.append(("sub", g))
        pos = m.end()
    out.append(("n", text[pos:]))
    return [o for o in out if o[1]]


def add_inline(par, text, size=14, bold=False, italic=False):
    """**жирный**, //курсив//, $латех$ — встроенная формула."""
    for part in INLINE.split(text):
        if not part:
            continue
        if part.startswith("**") and part.endswith("**"):
            r = par.add_run(part[2:-2]); r.bold = True; r.italic = italic
        elif part.startswith("//") and part.endswith("//"):
            r = par.add_run(part[2:-2]); r.italic = True; r.bold = bold
        elif part.startswith("$") and part.endswith("$") and len(part) > 2:
            par._p.append(omml(part[1:-1]))
            continue
        else:
            for kind, txt in scripts(part):
                r = par.add_run(txt); r.bold = bold; r.italic = italic
                r.font.size = Pt(size); r.font.name = FONT
                if kind == "sup": r.font.superscript = True
                elif kind == "sub": r.font.subscript = True
            continue
        r.font.size = Pt(size); r.font.name = FONT


def fmt(par, indent=True, align="justify", before=0, after=0, spacing=1.5, keep=False, size=None):
    pf = par.paragraph_format
    pf.first_line_indent = Cm(1.25) if indent else Cm(0)
    pf.space_before = Pt(before); pf.space_after = Pt(after)
    pf.line_spacing = spacing
    pf.alignment = {"justify": WD_ALIGN_PARAGRAPH.JUSTIFY, "center": WD_ALIGN_PARAGRAPH.CENTER,
                    "left": WD_ALIGN_PARAGRAPH.LEFT, "right": WD_ALIGN_PARAGRAPH.RIGHT}[align]
    if keep:
        pf.keep_with_next = True
    pf.widow_control = True


def new_section(doc, landscape):
    from docx.enum.section import WD_ORIENT
    s = doc.add_section(WD_SECTION.NEW_PAGE)
    s.different_first_page_header_footer = False
    if landscape:
        s.orientation = WD_ORIENT.LANDSCAPE
        s.page_width, s.page_height = Cm(29.7), Cm(21.0)
    else:
        s.orientation = WD_ORIENT.PORTRAIT
        s.page_width, s.page_height = Cm(21.0), Cm(29.7)
    s.left_margin = Cm(3); s.right_margin = Cm(1.5); s.top_margin = Cm(2); s.bottom_margin = Cm(2)
    return s


def render(b, path, title_fn, sources):
    b.number()
    doc = Document()
    st = doc.styles["Normal"]
    st.font.name = FONT; st.font.size = Pt(14)
    st.element.rPr.rFonts.set(qn("w:eastAsia"), FONT)
    for hn, size in (("Heading 1", 14), ("Heading 2", 14)):
        hs = doc.styles[hn]
        hs.font.name = FONT; hs.font.size = Pt(size); hs.font.bold = True
        hs.font.color.rgb = None
        rpr = hs.element.get_or_add_rPr()
        rf = rpr.find(qn("w:rFonts"))
        if rf is None:
            rf = OxmlElement("w:rFonts"); rpr.append(rf)
        for a in ("w:ascii", "w:hAnsi", "w:eastAsia", "w:cs"):
            rf.set(qn(a), FONT)
        for t in ("w:asciiTheme", "w:hAnsiTheme", "w:eastAsiaTheme", "w:cstheme"):
            if rf.get(qn(t)) is not None:
                del rf.attrib[qn(t)]
        col = rpr.find(qn("w:color"))
        if col is not None:
            col.set(qn("w:val"), "000000")
            for t in ("w:themeColor", "w:themeShade"):
                if col.get(qn(t)) is not None:
                    del col.attrib[qn(t)]
        hs.paragraph_format.space_before = Pt(0)
        hs.paragraph_format.space_after = Pt(12 if hn == "Heading 1" else 6)
        hs.paragraph_format.line_spacing = 1.5
        hs.paragraph_format.keep_with_next = True
    for s in ("TOC 1", "TOC 2"):
        try:
            ts = doc.styles[s]
        except KeyError:
            ts = doc.styles.add_style(s, 1)
        ts.font.name = FONT; ts.font.size = Pt(14)
        ts.paragraph_format.line_spacing = 1.5
        ts.paragraph_format.space_after = Pt(0)
        if s == "TOC 2":
            ts.paragraph_format.left_indent = Cm(0.6)

    sec = doc.sections[0]
    sec.page_height = Cm(29.7); sec.page_width = Cm(21.0)
    sec.left_margin = Cm(3); sec.right_margin = Cm(1.5); sec.top_margin = Cm(2); sec.bottom_margin = Cm(2)
    sec.different_first_page_header_footer = True
    fp = sec.footer.paragraphs[0]
    fp.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = fp.add_run(); r.font.size = Pt(14); r.font.name = FONT
    add_field(r, "PAGE")

    title_fn(doc)
    b.table_map = []
    title_tables = len(doc.tables)

    first_h1 = True
    for kind, pl in b.blocks:
        if kind == "h1":
            text, numbered = pl
            hp = doc.add_paragraph(style="Heading 1")
            hp.paragraph_format.page_break_before = True
            hp.alignment = WD_ALIGN_PARAGRAPH.CENTER if not numbered else WD_ALIGN_PARAGRAPH.LEFT
            hp.paragraph_format.first_line_indent = Cm(0) if not numbered else Cm(1.25)
            if text.startswith("ПРИЛОЖЕНИЕ"):
                # ГОСТ 7.32: «ПРИЛОЖЕНИЕ А» по центру, заголовок приложения – отдельной строкой по центру
                m = re.match(r"(ПРИЛОЖЕНИЕ\s+\S+?)\.?\s+(.*)", text)
                run = hp.add_run(m.group(1)); run.font.name = FONT; run.font.size = Pt(14); run.bold = True
                run.add_break()
                run = hp.add_run(m.group(2)); run.font.name = FONT; run.font.size = Pt(14); run.bold = True
            else:
                text = heading_text(text)
                run = hp.add_run(text); run.font.name = FONT; run.font.size = Pt(14); run.bold = True
        elif kind == "h2":
            hp = doc.add_paragraph(style="Heading 2")
            # обозначение подраздела – с отступом на два знака правее обозначения раздела
            hp.paragraph_format.first_line_indent = Cm(1.25 + H2_SHIFT)
            hp.paragraph_format.space_before = Pt(6)
            add_inline(hp, heading_text(pl), bold=True)
        elif kind == "p":
            text, kw = pl
            par = doc.add_paragraph()
            fmt(par, indent=kw.get("indent", True), align=kw.get("align", "justify"), keep=kw.get("keep", False))
            add_inline(par, b.resolve(text), size=kw.get("size", 14))
        elif kind == "def":
            label, text = pl
            par = doc.add_paragraph(); fmt(par)
            r = par.add_run(f"Определение {label}. "); r.font.size = Pt(14); r.font.name = FONT
            add_inline(par, b.resolve(text))
        elif kind in ("ol", "ul"):
            for i, item in enumerate(pl):
                par = doc.add_paragraph(); fmt(par, indent=False)
                par.paragraph_format.left_indent = Cm(1.25)
                par.paragraph_format.first_line_indent = Cm(-0.0)
                marker = f"{i + 1}) " if kind == "ol" else "– "
                add_inline(par, marker + b.resolve(item))
        elif kind == "eq":
            latex, label = pl
            par = doc.add_paragraph(); fmt(par, indent=False, align="left", before=3, after=3)
            tabs = par.paragraph_format.tab_stops
            tabs.add_tab_stop(Cm(8.25), WD_TAB_ALIGNMENT.CENTER)
            tabs.add_tab_stop(Cm(16.5), WD_TAB_ALIGNMENT.RIGHT)
            par.add_run("\t")
            par._p.append(omml(latex))
            if label:
                r = par.add_run(f"\t({b.labels[label]})"); r.font.size = Pt(14)
        elif kind == "fig":
            fpath, caption, label, width, landscape = pl
            if landscape:
                new_section(doc, True)
            par = doc.add_paragraph(); fmt(par, indent=False, align="center", before=6, keep=True, spacing=1.0)
            par.add_run().add_picture(fpath, width=Cm(width))
            cap = doc.add_paragraph(); fmt(cap, indent=False, align="center", after=6)
            add_inline(cap, f"Рисунок {b.labels[label]} – {b.resolve(caption)}", size=14)
            if landscape:
                new_section(doc, False)
        elif kind == "tab":
            caption, label, header, rows, widths, fs, align = pl
            num = b.labels[label]
            # ГОСТ 7.32: «Таблица N – Наименование» над таблицей слева, без абзацного отступа
            c1 = doc.add_paragraph(); fmt(c1, indent=False, align="left", keep=True, before=6)
            add_inline(c1, f"Таблица {num} – {b.resolve(caption)}")
            if 0 in b.tab_splits.get(label, []):
                c1.paragraph_format.page_break_before = True
            splits = sorted(set(x for x in b.tab_splits.get(label, []) if 0 < x < len(rows)))
            bounds = [0] + splits + [len(rows)]
            numrow = [str(i + 1) for i in range(len(header))] if splits else None
            for part in range(len(bounds) - 1):
                chunk = rows[bounds[part]:bounds[part + 1]]
                if part > 0:
                    cp = doc.add_paragraph(); fmt(cp, indent=False, align="left", keep=True)
                    cp.paragraph_format.page_break_before = True
                    add_inline(cp, f"Продолжение таблицы {num}")
                lines = []
                if part == 0:
                    lines.append(("head", header))
                if numrow:
                    lines.append(("num", numrow))
                lines += [("data", r) for r in chunk]
                b.table_map.append((label, part, bounds[part], part == 0, bool(numrow)))
                t = doc.add_table(rows=len(lines), cols=len(header))
                t.style = "Table Grid"
                t.alignment = WD_TABLE_ALIGNMENT.CENTER
                small = len(rows) <= 12 and not splits
                for ri, (lk, row) in enumerate(lines):
                    trPr = t.rows[ri]._tr.get_or_add_trPr()
                    cs = OxmlElement("w:cantSplit"); cs.set(qn("w:val"), "true"); trPr.append(cs)
                    for ci, val in enumerate(row):
                        cell = t.cell(ri, ci)
                        cell.width = Cm(widths[ci])
                        cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
                        cp = cell.paragraphs[0]
                        a = "center" if lk != "data" else (align[ci] if align else "left")
                        keep = (lk != "data") or (small and ri < len(lines) - 1)
                        fmt(cp, indent=False, align=a, spacing=1.0, keep=keep)
                        add_inline(cp, b.resolve(str(val)), size=fs, bold=(lk == "head"))
                        set_cell_margins(cell)
            sp = doc.add_paragraph(); fmt(sp, spacing=1.0); sp.paragraph_format.space_after = Pt(0)
        elif kind == "code":
            title, text = pl
            hp = doc.add_paragraph(); fmt(hp, indent=False, align="left", before=8, keep=True, spacing=1.0)
            r = hp.add_run(title); r.font.size = Pt(12); r.font.name = FONT
            for line in text.replace("\t", "    ").split("\n"):
                cp = doc.add_paragraph(); fmt(cp, indent=False, align="left", spacing=1.0)
                r = cp.add_run(line.rstrip() if line.strip() else " ")
                r.font.name = "Courier New"; r.font.size = Pt(10)
                r._r.get_or_add_rPr().get_or_add_rFonts().set(qn("w:hAnsi"), "Courier New")
        elif kind == "pb":
            doc.add_paragraph().add_run().add_break(WD_BREAK.PAGE)
        elif kind == "raw":
            pl(doc, b)

    b.table_offset = title_tables
    unused = [k for k in b.labels if k not in b.used_refs]
    if unused:
        print("НЕТ ССЫЛОК В ТЕКСТЕ НА:", unused)
    doc.save(path)
    return b
