"""Мини-движок для рисования диаграмм IDEF0 (Р 50.1.028-2001) средствами matplotlib."""
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, FancyArrowPatch
import textwrap

plt.rcParams["font.family"] = "Times New Roman"

LW = 1.1


def wrap(t, w):
    out = []
    for part in t.split("\n"):
        out += textwrap.wrap(part, w) or [""]
    return "\n".join(out)


class Diagram:
    def __init__(self, w=16, h=10.5, title="", node="", number=""):
        self.fig = plt.figure(figsize=(w * 0.62, h * 0.62))
        self.ax = self.fig.add_axes([0, 0, 1, 1])
        self.ax.set_xlim(0, w)
        self.ax.set_ylim(0, h)
        self.ax.axis("off")
        self.w, self.h = w, h
        self.boxes = {}
        # рамка бланка IDEF0
        self.ax.add_patch(Rectangle((0.15, 0.15), w - 0.3, h - 0.3, fill=False, lw=0.8))
        self.ax.plot([0.15, w - 0.15], [0.95, 0.95], color="k", lw=0.8)
        self.ax.plot([2.6, 2.6], [0.15, 0.95], color="k", lw=0.8)
        self.ax.plot([w - 2.6, w - 2.6], [0.15, 0.95], color="k", lw=0.8)
        self.ax.text(0.3, 0.72, "УЗЕЛ:", fontsize=8, va="center")
        self.ax.text(1.4, 0.5, node, fontsize=12, va="center", ha="center", weight="bold")
        self.ax.text(2.8, 0.72, "НАЗВАНИЕ:", fontsize=8, va="center")
        self.ax.text(w / 2, 0.47, title, fontsize=12, va="center", ha="center", weight="bold")
        self.ax.text(w - 2.45, 0.72, "НОМЕР:", fontsize=8, va="center")
        self.ax.text(w - 1.4, 0.47, number, fontsize=11, va="center", ha="center")
        self.top, self.bottom, self.left, self.right = h - 0.35, 1.15, 0.35, w - 0.35

    def box(self, key, x, y, bw, bh, text, num, fs=12, wrapw=18):
        self.ax.add_patch(Rectangle((x, y), bw, bh, fill=True, facecolor="white", edgecolor="k", lw=1.6, zorder=3))
        self.ax.text(x + bw / 2, y + bh / 2 + 0.08, wrap(text, wrapw), ha="center", va="center", fontsize=fs, zorder=4,
                     linespacing=1.15)
        self.ax.text(x + bw - 0.1, y + 0.1, num, ha="right", va="bottom", fontsize=10, zorder=4, weight="bold")
        self.boxes[key] = (x, y, bw, bh)

    # точки на сторонах блоков; frac — доля длины стороны от начала (слева/снизу)
    def L(self, k, f):
        x, y, w, h = self.boxes[k]
        return (x, y + h * f)

    def R(self, k, f):
        x, y, w, h = self.boxes[k]
        return (x + w, y + h * f)

    def T(self, k, f):
        x, y, w, h = self.boxes[k]
        return (x + w * f, y + h)

    def B(self, k, f):
        x, y, w, h = self.boxes[k]
        return (x + w * f, y)

    def arrow(self, pts, label="", lpos=None, lal="left", fs=10.5, wrapw=24, lva="bottom"):
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        self.ax.plot(xs[:-1] + [xs[-1]], ys[:-1] + [ys[-1]], color="k", lw=LW, zorder=2,
                     solid_joinstyle="round")
        a = FancyArrowPatch(pts[-2], pts[-1], arrowstyle="-|>", mutation_scale=13, color="k", lw=LW, zorder=2,
                            shrinkA=0, shrinkB=0)
        self.ax.add_patch(a)
        if label:
            if lpos is None:
                lpos = ((pts[0][0] + pts[1][0]) / 2, (pts[0][1] + pts[1][1]) / 2 + 0.08)
            self.ax.text(lpos[0], lpos[1], wrap(label, wrapw), fontsize=fs, ha=lal, va=lva, zorder=5,
                         linespacing=1.1, bbox=dict(boxstyle="square,pad=0.05", fc="white", ec="none", alpha=0.85))

    def tunnel_note(self, x, y, text):
        self.ax.text(x, y, text, fontsize=9, style="italic")

    def save(self, path):
        self.fig.savefig(path, dpi=300)
        plt.close(self.fig)
