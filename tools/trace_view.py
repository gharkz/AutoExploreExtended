# Draws the last run in BepInEx/aex-trace.csv: the path coloured by time (blue -> red), AutoExplore's chosen actions
# (green = multi-tool job, cyan = collect/pick-up, grey = other), fights (red X), route steps (orange numbers).
import csv, sys, re
from PIL import Image, ImageDraw
src = sys.argv[1] if len(sys.argv) > 1 else r"X:\SteamLibrary\steamapps\common\Elin\BepInEx\aex-trace.csv"
out = sys.argv[2] if len(sys.argv) > 2 else "trace.png"
rows = list(csv.reader(open(src, encoding="utf-8", errors="replace")))
start = max(i for i, r in enumerate(rows) if len(r) > 1 and r[1] == "map")
run = rows[start:]
m = re.search(r"(\d+)-(\d+) (\d+)-(\d+)", run[0][4]); x0, x1, z0, z1 = map(int, m.groups())
S = 12; W, H = (x1 - x0 + 1) * S, (z1 - z0 + 1) * S
img = Image.new("RGB", (W, H + 30), (24, 22, 20)); d = ImageDraw.Draw(img)
def xy(x, z): return ((int(x) - x0) * S + S // 2, H - ((int(z) - z0) * S + S // 2))
pos = [r for r in run[1:] if r[1] == "pos"]
for i in range(1, len(pos)):
    t = i / max(1, len(pos) - 1); col = (int(60 + 195 * t), 80, int(255 - 195 * t))
    d.line([xy(pos[i-1][2], pos[i-1][3]), xy(pos[i][2], pos[i][3])], fill=col, width=3)
walked = sum(abs(int(pos[i][2]) - int(pos[i-1][2])) + abs(int(pos[i][3]) - int(pos[i-1][3])) for i in range(1, len(pos)))
step = 0
for r in run[1:]:
    if r[1] == "act":
        c = (80, 220, 90) if r[4].startswith("multi") else (80, 210, 230) if "Collect" in r[4] or "Pick" in r[4] else (150, 150, 150)
        x, y = xy(r[2], r[3]); d.ellipse([x - 3, y - 3, x + 3, y + 3], outline=c, width=2)
    elif r[1] == "note" and "fight" in r[4]:
        x, y = xy(r[2], r[3]); d.line([x - 5, y - 5, x + 5, y + 5], fill=(240, 60, 50), width=2); d.line([x - 5, y + 5, x + 5, y - 5], fill=(240, 60, 50), width=2)
    elif r[1] == "note" and "next chunk" in r[4]:
        step += 1; x, y = xy(r[2], r[3]); d.text((x + 4, y - 10), str(step), fill=(255, 170, 60))
d.text((6, H + 8), f"{run[0][4][:90]}  |  walked ~{walked} tiles over {len(pos)} s", fill=(230, 230, 230))
img.save(out); print(out, len(pos), "samples, walked", walked)
