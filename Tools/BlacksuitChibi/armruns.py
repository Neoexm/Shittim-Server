"""Measures the crack fan on official's Blacksuit head straight off Blacksuit_Atlas.png.

The arms are contiguous angular runs, not histogram peaks. Binning the bright texels at 5 degrees
and taking the peaks splits official's single +130..+175 sweep into three arms at 143/155/170, which is
what produced a fan bunched inside 27 degrees instead of the six arms it actually has. Merging adjacent
non-empty bins is the whole fix. Reach is the p90 and not the max, because the max is one stray tip texel.

The six runs this reports are the ARMS table in paintface.py.

Unresolved: this atlas measurement says six arms, and reading official's own render says three or four.
Two hypotheses are dead. Grazing side-of-head texels are not the cause - sweeping the front-facing cut at
N.z > 0.0 / 0.35 / 0.60 leaves the run structure unchanged. Nor is it texels that no facet samples: the
strict raster below agrees with the baked mask to 1150 out of 1150 bright texels, so nothing is being
counted by area. The fan derived here is the one in use, on the grounds that it is a direct per-texel
measurement of official's own texture, but the disagreement with the render is not explained.

off_P.npy and off_Nrm.npy are 12 MB each and untracked; they are per-texel world position and normal for
the head island, baked out of the Unity spike scene. uv_Blacksuit_Head.txt is a plain dump of
Blacksuit_Head's mesh.uv and mesh.triangles.
"""
import numpy as np
from PIL import Image

ATL = r"c:\Users\tomda\Documents\Shittim-Server\JP_Dev\Assets\_BlacksuitSpike\Blacksuit_Atlas.png"
R = 1024
W = np.array([.2126, .7152, .0722], np.float32)
# the head bone's world height in the spike scene, which is what the baked positions are measured from
BONE = 0.6640
# official's eye centre in that frame
EYE = np.array([0.0553, 0.7464 - BONE])


def load_uv(path):
    ln = open(path).read().split("\n")
    nuv, ntri = [int(x) for x in ln[0].split()]
    uv = np.array([[float(y) for y in ln[1 + i].split()] for i in range(nuv)], np.float64)
    tri = np.array([[int(y) for y in ln[1 + nuv + i].split()] for i in range(ntri // 3)], np.int32)
    return uv, tri


def raster(uv, tri):
    m = np.zeros((R, R), bool)
    px = np.stack([uv[:, 0] * R, (1.0 - uv[:, 1]) * R], 1)
    for a, b, c in tri:
        p0, p1, p2 = px[a], px[b], px[c]
        x0 = max(int(np.floor(min(p0[0], p1[0], p2[0]))) - 1, 0)
        x1 = min(int(np.ceil(max(p0[0], p1[0], p2[0]))) + 1, R)
        y0 = max(int(np.floor(min(p0[1], p1[1], p2[1]))) - 1, 0)
        y1 = min(int(np.ceil(max(p0[1], p1[1], p2[1]))) + 1, R)
        if x1 <= x0 or y1 <= y0:
            continue
        d = (p1[0] - p0[0]) * (p2[1] - p0[1]) - (p2[0] - p0[0]) * (p1[1] - p0[1])
        if abs(d) < 1e-12:
            continue
        yy, xx = np.mgrid[y0:y1, x0:x1]
        # texel centres, so a texel counts as sampled only when its centre lands inside a triangle
        qx, qy = xx + 0.5, yy + 0.5
        w1 = ((qx - p0[0]) * (p2[1] - p0[1]) - (p2[0] - p0[0]) * (qy - p0[1])) / d
        w2 = ((p1[0] - p0[0]) * (qy - p0[1]) - (qx - p0[0]) * (p1[1] - p0[1])) / d
        m[y0:y1, x0:x1] |= (w1 >= 0) & (w2 >= 0) & (w1 + w2 <= 1)
    return m


lum = np.asarray(Image.open(ATL).convert("RGB")).astype(np.float32) @ W / 255.
P = np.load("off_P.npy")
N = np.load("off_Nrm.npy")
baked = np.abs(P).sum(2) > 0
head = raster(*load_uv("uv_Blacksuit_Head.txt"))

bright = (lum > 0.45) & head & (N[:, :, 2] > 0.0)
print("head raster %d texels, baked mask %d, bright on the head %d" % (head.sum(), baked.sum(), bright.sum()))
print("bright disagreement between raster and baked mask: %d texels" % (((lum > 0.45) & (head ^ baked)).sum()))

q = np.stack([P[:, :, 0][bright], (P[:, :, 1] - BONE)[bright]], 1)
d = np.linalg.norm(q - EYE, axis=1)
ang = np.degrees(np.arctan2(q[:, 1] - EYE[1], q[:, 0] - EYE[0]))
k = d > 0.020
h, ed = np.histogram(ang[k], bins=72, range=(-180, 180))

runs, cur = [], None
for i in range(72):
    if h[i]:
        cur = [i, i] if cur is None else [cur[0], i]
    elif cur:
        runs.append(cur)
        cur = None
if cur:
    runs.append(cur)

print("%d texels beyond 20 mm in %d contiguous runs" % (k.sum(), len(runs)))
arms = []
for a, b in runs:
    s = (ang[k] >= ed[a]) & (ang[k] < ed[b + 1])
    rr, aa = d[k][s], ang[k][s]
    bearing = float((aa * rr).sum() / rr.sum())
    reach = float(np.percentile(rr, 90))
    arms.append((bearing, reach))
    print("   %+4.0f..%+4.0f deg  %4d texels  bearing %+6.1f  reach p90 %5.1f max %5.1f mm" % (
        ed[a], ed[b + 1], s.sum(), bearing, 1000 * reach, 1000 * rr.max()))
arms.sort(key=lambda t: -t[1])
print("ARMS = %s" % [(round(a, 1), round(r, 3)) for a, r in arms])
# 531 texels over 338 mm of merged arc is a 1.9 mm effective line width, which is where LWMM 2.01 comes from
print("total arc %.0f mm at %d texels -> effective width %.2f mm" % (
    1000 * sum(r for _, r in arms), k.sum(), 1000 * sum(r for _, r in arms) / max(k.sum(), 1) * 1.0996))
