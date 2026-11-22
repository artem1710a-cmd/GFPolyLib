"""Сборка пояснительной записки по ГОСТ 7.32 с автоматическим оформлением переноса таблиц.

Использование: python build_all.py <корень проекта> <выходной .docx> <выходной .pdf>

1) build_pz.py собирает .docx;
2) Word (measure_tables.ps1) пагинирует документ и сообщает страницу каждой строки таблиц;
3) для таблиц, переходящих на следующую страницу, вычисляются точки разрыва, и документ
   пересобирается: под шапкой – строка с номерами граф, на новой странице –
   «Продолжение таблицы N» и снова строка с номерами граф;
4) шаги 1–3 повторяются до устойчивого результата, затем word_finalize.ps1 обновляет
   содержание и экспортирует PDF.
"""
import json, os, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
root, out_docx, out_pdf = (os.path.abspath(a) for a in sys.argv[1:4])
splits_file = out_docx + ".splits.json"
map_file = out_docx + ".map.json"
pages_file = out_docx + ".pages.txt"
if os.path.exists(splits_file):
    os.remove(splits_file)

splits = {}
for it in range(8):
    subprocess.run([sys.executable, os.path.join(HERE, "build_pz.py"), root, out_docx], check=True)
    subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                    os.path.join(HERE, "measure_tables.ps1"), out_docx, pages_file], check=True)
    m = json.load(open(map_file, encoding="utf-8"))
    pages = {}
    for line in open(pages_file, encoding="utf-8-sig"):
        idx, _, pg = line.strip().partition(" ")
        pages[int(idx)] = [int(x) for x in pg.split(",") if x]
    changed = False
    touched = set()
    for k, (label, part, start, has_head, has_num) in enumerate(m["map"]):
        if label in touched:
            continue
        pg = pages[m["offset"] + k]
        off = int(has_head) + int(has_num)
        data = pg[off:]
        first = pg[0]
        for j, p in enumerate(data):
            if p != first:
                if part == 0 and j < 2 and 0 not in splits.get(label, []):
                    # на странице поместилось бы меньше двух строк – вся таблица переносится на новую страницу
                    splits[label] = [0]
                    changed = True
                    touched.add(label)
                elif j > 0 and start + j not in splits.get(label, []):
                    # последующие разрывы этой таблицы пересчитываются на следующей итерации
                    splits[label] = [s for s in splits.get(label, []) if s < start + j] + [start + j]
                    changed = True
                    touched.add(label)
                break
    print(f"итерация {it + 1}: разрывы таблиц {splits}")
    if not changed:
        break
    json.dump(splits, open(splits_file, "w", encoding="utf-8"), ensure_ascii=False)

subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                os.path.join(HERE, "word_finalize.ps1"), out_docx, out_pdf], check=True)
for f in (map_file, pages_file):
    if os.path.exists(f):
        os.remove(f)
