"""Does our chibi's smoke read like official's, and what should _Tint alpha be.

Blacksuit/Smoke is an unlit tex2D(_MainTex, uv) * _Tint under Blend SrcAlpha OneMinusSrcAlpha, so the composite alpha of a frame can be recovered exactly instead of guessed at: shoot the same camera twice, once over black and once over white, and in linear light white minus black is 1-a whatever colour the smoke is. BlacksuitShots.Smoke writes those pairs; this reads them back. The project is Linear, so the pngs have to be un-sRGB'd before differencing or the answer comes out wrong by a lot near the soft edges.

Both smokes are framed on their own baked smoke verts at the same distance, so px/mm is identical across every image here and every area or width below is in real millimetres rather than pixels.

Verdict: 0.370 is too light by a wide margin and the premise it was authored on is backwards. Official's three cards read at composite alpha 0.714 front-on and 0.782 at 55 degrees, ours at 0.370 reads 0.396 and 0.369, so we are 0.32 to 0.41 under, not over. Our composite is 0.888*a plus a 10.6% triple-overlap sliver at 1-(1-a)^3, which puts the front-on optimum at a=0.69 and the 55 degree optimum at a=0.78; 0.75 lands within +0.055 and -0.033 of official on both and is the recommendation. Two things no tint value reaches: our wisp covers 5953 mm2 front-on against official's 31494, so even at a=1.0 we carry 26% of official's integrated alpha there, and 88.8% of our covered pixels sit on one flat value against official's 47.8% because BS_Body_2048 is RGB24 with no alpha channel and our uvs land on a 7x21 pixel patch of it, so there is no per-pixel ramp to have. Both are geometry and texture decisions, not tint ones.

Two harness bugs had to be fixed before any of this measured anything. BlacksuitShots.Setup dropped filtered-out renderers from the proxy pass but left them enabled and on the camera layer, so the first isolated-smoke renders had the whole body in frame; and the camera asks for post so MXFinalBloom will run, which is not a linear composite and blooms a white background as well, so white-minus-black recovered nothing until Smoke turned it off.
"""
import sys
import os
import numpy as np
from PIL import Image

DIR = sys.argv[1]
PPMM = 3.4666
VARIANTS = ["off", "our025", "our037", "our050", "our075", "our100"]
YAWS = [0, 55]


def lin(u8):
    c = u8.astype(np.float64) / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def alpha(tag, yaw):
    k = lin(np.asarray(Image.open(os.path.join(DIR, "%s_y%d_k.png" % (tag, yaw))).convert("RGB")))
    w = lin(np.asarray(Image.open(os.path.join(DIR, "%s_y%d_w.png" % (tag, yaw))).convert("RGB")))
    a = 1.0 - (w - k).mean(axis=2)
    return np.clip(a, 0.0, 1.0), k


def grad_mm(a):
    gy, gx = np.gradient(a)
    return np.hypot(gx, gy) * PPMM


def stats(a):
    cov = a > 0.05
    n = int(cov.sum())
    if n == 0:
        return None
    v = a[cov]
    # anything measuring the width of an alpha ramp reads as noise on a card that has no ramp, so the plateau share stands in for edge softness instead: what fraction of the covered pixels sit within a couple of percent of the single commonest value. a textured card spreads, a flat tint piles up.
    hist, edges = np.histogram(v, bins=50, range=(0.0, 1.0))
    mode = 0.5 * (edges[hist.argmax()] + edges[hist.argmax() + 1])
    flat = float((np.abs(v - mode) < 0.02).mean())
    g = grad_mm(a)
    return dict(area=n / PPMM ** 2, mean=float(v.mean()), p10=float(np.percentile(v, 10)),
                p90=float(np.percentile(v, 90)), peak=float(np.percentile(v, 99.5)),
                # integrated alpha, in mm2 of fully opaque equivalent. this is the number that decides how heavy the smoke reads, since area and opacity trade off against each other and neither alone says.
                ink=float(v.sum()) / PPMM ** 2, flat=flat, grad=float(g[cov].mean()))


print("%-7s %-4s %9s %6s %6s %6s %6s %9s %6s %8s" % ("var", "yaw", "area_mm2", "mean", "p10", "p90", "peak", "ink_mm2", "flat", "grad/mm"))
ref = {}
ours = {}
for t in VARIANTS:
    for y in YAWS:
        a, _ = alpha(t, y)
        s = stats(a)
        (ref if t == "off" else ours)[(t, y)] = s
        print("%-7s %-4d %9.1f %6.3f %6.3f %6.3f %6.3f %9.1f %6.3f %8.4f" % (t, y, s["area"], s["mean"], s["p10"], s["p90"], s["peak"], s["ink"], s["flat"], s["grad"]))

print()
print("match against official, per yaw: opacity error and the share of official's ink we carry")
print("%-7s %s" % ("var", "  ".join("y%-2d dmean   ink%%" % y for y in YAWS)))
for t in VARIANTS[1:]:
    cells = []
    for y in YAWS:
        cells.append("%+7.3f %5.0f%%" % (ours[(t, y)]["mean"] - ref[("off", y)]["mean"], 100.0 * ours[(t, y)]["ink"] / ref[("off", y)]["ink"]))
    print("%-7s %s" % (t, "  ".join(cells)))

print()
print("alpha profile across the wisp, row through the coverage centroid, sampled every 8 px (%.2f mm)" % (8.0 / PPMM))
for t in VARIANTS:
    a, _ = alpha(t, 0)
    cov = a > 0.05
    ys, xs = np.nonzero(cov)
    row = int(round(ys.mean()))
    seg = a[row, xs.min():xs.max() + 1]
    print("%-7s row %3d  %s" % (t, row, " ".join("%.2f" % v for v in seg[::8])))

print()
print("histogram of composite alpha over covered pixels, 10 bins 0..peak")
for t in VARIANTS:
    a, _ = alpha(t, 0)
    v = a[a > 0.05]
    peak = np.percentile(v, 99.5)
    h, _ = np.histogram(v, bins=10, range=(0.0, peak))
    print("%-7s peak %.3f  %s" % (t, peak, " ".join("%4.1f%%" % (100.0 * c / len(v)) for c in h)))

# composite every variant over mid grey for the eye check. src*a is exactly the over-black render, so the grey composite is that plus grey*(1-a) with no extra render needed.
grey = lin(np.full((1, 1, 3), 137, dtype=np.uint8))[0, 0, 0]
tiles = []
for t in VARIANTS:
    row = []
    for y in YAWS:
        a, k = alpha(t, y)
        comp = k + grey * (1.0 - a)[:, :, None]
        row.append(comp)
        row.append(np.repeat((a / max(1e-6, a.max()))[:, :, None], 3, axis=2))
    tiles.append(np.concatenate(row, axis=1))
sheet = np.concatenate(tiles, axis=0)
srgb = np.where(sheet <= 0.0031308, sheet * 12.92, 1.055 * np.clip(sheet, 0, None) ** (1 / 2.4) - 0.055)
Image.fromarray((np.clip(srgb, 0, 1) * 255).astype(np.uint8)).save(os.path.join(DIR, "sheet.png"))
print()
print("sheet.png written: rows are %s, columns are yaw0 composite, yaw0 alpha, yaw55 composite, yaw55 alpha" % ", ".join(VARIANTS))
