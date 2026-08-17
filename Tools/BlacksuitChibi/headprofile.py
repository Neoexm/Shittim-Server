"""Radial luminance profile of a chibi head render, in true millimetres from the eye centre.

This is the measurement of record for how close our face is to official's. Bands are means over the
-150..-30 degree wedge below the eye; that wedge is not clean on either model, since official's own arms
occupy -125..-115 and -75..-10, but it is the widest sector with comparable contamination on both sides.
A narrower arm-free window exists at -112..-78 and is unusable - official's profile there is non-monotonic
(12-16 mm reads 0.250 against 16-20 mm at 0.260), so it is too sparse to fit against.

The pixels-per-mm and eye-centre constants below are only valid for renders taken at BlacksuitShots.HeadFit
fill 0.80. fill rescales the frame without changing the head bounding box, so a render at 0.62 comes out at
1.318 px/mm with the eye at (252.0, 232.7) and every band lands in the wrong ring: it reads as an 18 px eye
displacement and a catastrophic profile regression when nothing about the model has changed. The head bbox is
identical across both fills (nv 2456, y 0.6148..0.9019, size 0.2871, target 0.0198/0.7583/-0.0501), which is
how to tell the two apart. BlacksuitShots.bindpose is static and resets on domain reload; it must be set true
before any face render or the two chibis are compared in different poses.

Absolute band values are only comparable between renders taken through the same camera. Official's own bloom
is a renderer feature, Hidden_MXFinalBloom, whose shader is not present in the ripped project -
UIRenderBloomSettings.asset ships but the pass cannot be rebuilt from what is there, and it was deliberately
not reconstructed. The offscreen camera enables allowHDR and renderPostProcessing so that URP's own bloom
stands in for it consistently on both sides.
"""
import sys
import numpy as np
from PIL import Image

W = np.array([.2126, .7152, .0722], np.float32)
BANDS = [(0, 4), (4, 8), (8, 12), (12, 16), (16, 20), (20, 25), (25, 32), (32, 42)]
OFFICIAL = ("y_off.png", 1.5823, (254.4, 182.6))
# markers painted at known (r,u) put our face at 1.7304 px per mm, not the 1.5604 the head bbox implies - the face
# sits nearer the camera than the bbox mid-depth that sets the frame scale. both heads are geometrically similar
# under the same fill so official carries the same 1.109 factor.
OURS_PPM = 1.7304
OURS_EYE = (243.0, 217.6)


def prof(p, ppm, cy, cx):
    a = np.asarray(Image.open(p).convert("RGB")).astype(np.float32)[:, :560] / 255.
    m = a @ W
    yy, xx = np.mgrid[0:560, 0:560]
    d = np.hypot(yy - cy, xx - cx) / ppm
    ang = np.degrees(np.arctan2(-(yy - cy), xx - cx))
    sec = (ang > -150) & (ang < -30)
    return (np.array([m[sec & (d >= lo) & (d < hi)].mean() for lo, hi in BANDS]),
            np.array([a[sec & (d >= lo) & (d < hi)].mean(0) for lo, hi in BANDS]))


def blob(m, thr, seed):
    """Connected region above thr containing seed, by iterated dilation. Returns count and bbox."""
    k = m > thr
    cur = np.zeros_like(k)
    cur[seed] = True
    for _ in range(800):
        n = cur.copy()
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                s = np.zeros_like(cur)
                H, Wd = cur.shape
                s[max(0, dy):H + min(0, dy), max(0, dx):Wd + min(0, dx)] = \
                    cur[max(0, -dy):H - max(0, dy), max(0, -dx):Wd - max(0, dx)]
                n |= s
        n &= k
        if (n == cur).all():
            break
        cur = n
    ys, xs = np.nonzero(cur)
    return cur.sum(), (ys.min(), ys.max(), xs.min(), xs.max())


if __name__ == "__main__":
    cases = [OFFICIAL] + [(n + ".png", OURS_PPM, OURS_EYE) for n in sys.argv[1:]]
    for p, ppm, (cy, cx) in cases:
        lm, c = prof(p, ppm, cy, cx)
        m = np.asarray(Image.open(p).convert("RGB")).astype(np.float32)[:, :560] @ W / 255.
        n, (y0, y1, x0, x1) = blob(m, 0.90, (int(cy), int(cx)))
        print("%-12s " % p.replace(".png", "") + " ".join("%.3f/%.2f" % (lm[i], c[i][2] / max(c[i][0], 1e-6))
                                                          for i in range(len(BANDS))))
        # official's >0.90 region is exactly its core lens, so this aspect is a direct read on lens vs blob and it
        # is threshold-sensitive: a 0.03 luminance difference at the rim moves it by 0.15
        print("             core >0.90 %5d px  %.1f x %.1f mm  aspect %.2f" % (
            n, (x1 - x0 + 1) / ppm, (y1 - y0 + 1) / ppm, (x1 - x0 + 1.) / (y1 - y0 + 1.)))
    print("%-12s " % "true mm" + " ".join("%9s" % ("%d-%d" % b) for b in BANDS))
