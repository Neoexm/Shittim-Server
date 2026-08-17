"""Paints the chibi's face into the head region of BS_Body_2048.png.

The head was re-unwrapped for this. The original delivered atlas had no authored face at all in that region,
just base colour, so there was nothing there to preserve. The new island is non-overlapping at official-comparable
texel density but it is 33 8-connected uv charts, not one: six large head shells holding 139208 of 152915 texels,
plus 27 small fringe and spike pieces. Chart 20 is the face. lift() has to stay on the shells - a fringe spike that
overhangs the same (r,u) is nearer in 2d and would drag a whole crack segment out onto the hair.

Nothing outside the island rect is touched, which the run reports and which is the check to keep watching.

Writes BS_Body_Emission.png as well, and nothing consumes it any more. The additive glow shell it fed
(EM_BS_Body plus BSChibi_Glow on DSFX/FX_SHADER_Additive_0) was measured against official four ways and lost on
every axis - profile rms 0.024 albedo-only against 0.052 and 0.125 additive, blue/red peak 1.76 against 1.62 and
1.36, core aspect 1.67 against 1.33 and 0.94 - and official's own prefab has no additive glow renderer either,
so the shell was deleted and the glow lives in the albedo. The emission output is kept because it is a readable
picture of the mask.

Needs isl_tex/isl_pos/isl_nrm/isl_chart.npy (the island bake), strokes.npy (the portrait's traced strokes),
atlas_used.npy and the v1 albedo. Those are untracked; ramp.npz beside this file is the live glow ramp.
"""
import numpy as np, sys
from PIL import Image

SPIKE = r'c:/Users/tomda/Documents/Shittim-Server/JP_Dev/Assets/_BlacksuitSpike/BSChibi/'
V1 = r'c:/Users/tomda/Documents/Shittim-Server/JP_Dev/Src/BSComplex/work/emrev/BS_Body_2048_v1.png'
WHB = np.array([0.0, 0.0278, 0.6216])

NSTROKE = int(sys.argv[1]) if len(sys.argv) > 1 else 6
LWMM = float(sys.argv[2]) if len(sys.argv) > 2 else 2.01
EMK = float(sys.argv[3]) if len(sys.argv) > 3 else 1.0

# every colour and every dimension below is measured off Blacksuit_Atlas.png and the official head's uv dump.
# official's island runs 1.0996 mm of surface per texel, which is what turns its texel counts into millimetres.
HBASE = np.array([0.0431, 0.0431, 0.0627], np.float32)
CBEAD = np.array([0.9490, 0.9686, 1.0000], np.float32)
CEYE = np.array([0.8118, 0.8941, 1.0000], np.float32)
CLINE = np.array([0.5765, 0.6392, 0.8078], np.float32)
CCRES = np.array([0.1451, 0.1608, 0.2235], np.float32)

# ours is 287.1mm tall against official's 314.0, so placements and feature sizes both carry that 0.9143
K = 287.1 / 314.0
ER, EU = 0.0551, 0.1609
# the island's texel axes are not aligned with world up, so the eye's shape has to come off the render rather
# than off the atlas: official's >0.90 core measures 32.2 x 17.5 mm, a horizontal lens, not a vertical one
EYE_A, EYE_B = 0.0147, 0.0080
# the core is a lens but the bloom around it is circular, and it goes blue as it falls. this ramp is official's
# own render profile sampled in the sector below its eye that none of its cracks cross - the atlas annulus means
# used before were crossed by lines, which is what made them read as a wide flat halo instead of this.
# the extra 0.87 is perspective: the face sits ~13% nearer the camera than the head's mid-depth that sets the
# frame scale, so a radius painted at face depth renders that much wider than the nominal mm it was authored in
GRR = np.array([0.0, 0.006, 0.010, 0.014, 0.018, 0.0225, 0.0285, 0.034], np.float32) * (287.1 / 314.0) * 0.87
GC = np.array([[0.973, 0.984, 1.000], [0.973, 0.984, 1.000], [0.886, 0.935, 1.000], [0.602, 0.701, 0.891],
               [0.229, 0.278, 0.418], [0.186, 0.201, 0.259], [0.066, 0.069, 0.094], [0.0431, 0.0431, 0.0627]], np.float32)
# official's bright knots are cross-bars lying across the crack, 4-6 mm long and about 1.3 mm thick
# the toon shader plus the 8x msaa resolve broadens the ramp on the way to the frame, so what the texture has to
# hold is a pre-sharpened version of official's render profile rather than the profile itself. fit.py measures our
# own render against official's and rewrites the ramp here.
import os
if os.path.exists('ramp.npz'):
    _z = np.load('ramp.npz')
    GRR, GC = _z['r'].astype(np.float32), _z['c'].astype(np.float32)

BEAD_PERP, BEAD_ALONG = 0.0033, 0.0013
# a crack dims as it runs away from the eye - official's median goes 0.785 at the rim to 0.491 at the far tip,
# against the 0.638 its overall median reports
LTR = np.array([0.022, 0.035, 0.050, 0.070, 0.112], np.float32)
LTV = np.array([0.785, 0.712, 0.638, 0.564, 0.491], np.float32) / 0.638
NBEAD = 8
# every crack radiates out of the eye. bearing in degrees with +x the character's right and 90 straight up, then length.
# both come from official's bright atlas texels beyond 20mm binned at 5 degrees and merged into contiguous runs - the
# runs are the arms, and reading peaks instead of runs is what split official's single 130..175 sweep into three.
# length is the p90 reach, since the max is one stray texel at the tip.
ARMS = [(100.5, 0.101), (155.4, 0.096), (5.7, 0.050), (44.3, 0.039), (-66.0, 0.029), (-26.0, 0.025)]
FAN_UP, FAN_L, FAN_R = 0.1088 * K, 0.1261 * K, 0.0552 * K
# the grin spans the whole face, from well past the eye to the far cheek, and its tips curl up above eye level
CRES_R0, CRES_R1 = -0.0857 * K, 0.0308 * K
CRES_U0, CRES_U1 = -0.0585 * K, 0.0081 * K

PEX, PEY = 183.5, 216.1
SUP = 0.2239 / 184.0
SRT = 0.2402 / 148.0

tex = np.load('isl_tex.npy')
pos = np.load('isl_pos.npy')
nrm = np.load('isl_nrm.npy')
irow = 2047 - tex[:, 1]
r = -pos[:, 0]
u = pos[:, 2] - WHB[2]
f = WHB[1] - pos[:, 1]
nf = -nrm[:, 1]
front = nf > 0.0
P = np.stack([r, u, f], 1)

nn = []
for i in range(0, len(P), 37):
    d3 = np.linalg.norm(P - P[i], axis=1)
    dt = np.hypot(tex[:, 0] - tex[i, 0], irow - irow[i])
    m = (dt > 0.5) & (dt < 5)
    if m.any():
        nn.append((1000.0 * d3[m] / dt[m]).mean())
MMT = float(np.median(nn))
print('our island: %d texels, %.4f mm of surface per texel (official 1.0996)' % (len(tex), MMT))


def sh(m, dy, dx, fill=0):
    o = np.full_like(m, fill)
    H, W = m.shape
    o[max(0, dy):H + min(0, dy), max(0, dx):W + min(0, dx)] = m[max(0, -dy):H - max(0, dy), max(0, -dx):W - max(0, dx)]
    return o


def comps(m):
    lab = np.where(m, np.arange(m.size).reshape(m.shape), -1)
    while True:
        c = lab.copy()
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                c = np.maximum(c, sh(lab, dy, dx, -1))
        c = np.where(m, c, -1)
        if (c == lab).all():
            return lab
        lab = c


keep = np.load('strokes.npy')
lab = comps(keep)
ids = np.unique(lab[keep])

# a crest component is a set of pixels, so walking it end to end is what turns it into a curve that can be
# drawn continuously instead of sampled per texel
def trace(m):
    ys, xs = np.nonzero(m)
    pts = list(zip(ys.tolist(), xs.tolist()))
    idx = {p: i for i, p in enumerate(pts)}
    adj = [[] for _ in pts]
    for i, (y, x) in enumerate(pts):
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dy or dx:
                    j = idx.get((y + dy, x + dx))
                    if j is not None:
                        adj[i].append(j)

    def far(s):
        d = [-1] * len(pts)
        d[s] = 0
        q = [s]
        while q:
            nq = []
            for i in q:
                for j in adj[i]:
                    if d[j] < 0:
                        d[j] = d[i] + 1
                        nq.append(j)
            q = nq
        b = int(np.argmax(d))
        return b, d

    a, _ = far(0)
    b, d = far(a)
    path = [b]
    while path[-1] != a:
        i = path[-1]
        path.append(min(adj[i], key=lambda j: d[j]))
    return np.array([pts[i] for i in path], np.float32)


polys = []
for i in ids:
    m = (lab == i)
    if m.sum() < 6:
        continue
    p = trace(m)
    n = max(3, int(len(p) / 6))
    q = np.array([p[int(round(t))] for t in np.linspace(0, len(p) - 1, n)], np.float32)
    for _ in range(3):
        q[1:-1] = (q[:-2] + 2 * q[1:-1] + q[2:]) / 4.0
    polys.append(q)

anchor = np.array([PEY, PEX], np.float32)
scored = []
for q in polys:
    seg = np.linalg.norm(np.diff(q, axis=0), axis=1).sum()
    near = np.linalg.norm(q - anchor, axis=1).min()
    cen = q[:, 0].mean()
    scored.append((seg, near, cen, q))
cres = [s for s in scored if s[2] > 268]
# official puts the whole fan above the eye and only the crescent below, so a stroke qualifies by reaching
# above it, not by its centroid
fan = [s for s in scored if s[3][:, 0].min() < PEY - 3]
fan.sort(key=lambda s: -s[0])
fan = fan[:NSTROKE]
print('traced %d strokes -> fan %d (lengths %s), crescent %d' % (
    len(polys), len(fan), np.round([s[0] for s in fan], 0).astype(int), len(cres)))


def tomm(q):
    return np.stack([(PEX - q[:, 1]) * SRT, (PEY - q[:, 0]) * SUP], 1)


E = np.array([ER, EU], np.float32)
raw = [tomm(s[3]) for s in fan]
raw.sort(key=lambda q: -np.linalg.norm(np.diff(q, axis=0), axis=1).sum())
# the portrait supplies the curvature and the jitter of each crack; where it sits relative to the eye is
# official's business, so each stroke gets swung onto one of official's bearings and stretched to its length
fm = []
for q, (deg, ln) in zip(raw, sorted(ARMS[:len(raw)], key=lambda a: -a[1])):
    if np.linalg.norm(q[0]) > np.linalg.norm(q[-1]):
        q = q[::-1]
    th = np.arctan2(q[-1, 1] - q[0, 1], q[-1, 0] - q[0, 0])
    c, s = np.cos(th), np.sin(th)
    q = (q - q[0]) @ np.array([[c, -s], [s, c]], np.float32)
    L = ln * K
    q = q * (L / q[-1, 0])
    # official's cracks only bow slightly, so a portrait stroke that wanders gets its sideways excursion pulled in
    ay = np.abs(q[:, 1]).max()
    if ay > 0.15 * L:
        q[:, 1] *= 0.15 * L / ay
    cs, sn = np.cos(np.radians(deg)), np.sin(np.radians(deg))
    q = q @ np.array([[cs, sn], [-sn, cs]], np.float32)
    fm.append(q + E + np.array([cs, sn], np.float32) * (0.8 / np.hypot(cs / EYE_A, sn / EYE_B)))
print('%d arms placed on official bearings %s, lengths %s mm' % (
    len(fm), [int(a[0]) for a in sorted(ARMS[:len(raw)], key=lambda a: -a[1])],
    np.round([1000 * np.linalg.norm(np.diff(q, axis=0), axis=1).sum() for q in fm], 0).astype(int)))
allp = np.concatenate(fm, 0)
print('placed fan: right %+.4f..%+.4f  up %.4f..%.4f (target up<=%.4f, left>=%.4f)' % (
    allp[:, 0].min(), allp[:, 0].max(), allp[:, 1].min(), allp[:, 1].max(), EU + FAN_UP, ER - FAN_L))

cm = []
if cres:
    c = np.concatenate([tomm(s[3]) for s in cres], 0)
    # the portrait's grin arrives as two broken components. official's is one unbroken arc, so the points get collapsed
    # into a single curve of median height per column instead of drawn component by component
    c = c[np.argsort(c[:, 0])]
    bins = np.linspace(c[0, 0], c[-1, 0], 25)
    q = np.array([[0.5 * (bins[i] + bins[i + 1]), np.median(c[(c[:, 0] >= bins[i]) & (c[:, 0] <= bins[i + 1]), 1])]
                  for i in range(24) if ((c[:, 0] >= bins[i]) & (c[:, 0] <= bins[i + 1])).any()], np.float32)
    for _ in range(2):
        q[1:-1, 1] = (q[:-2, 1] + 2 * q[1:-1, 1] + q[2:, 1]) / 4.0
    q = np.stack([ER + CRES_R0 + (q[:, 0] - q[:, 0].min()) / max(np.ptp(q[:, 0]), 1e-6) * (CRES_R1 - CRES_R0),
                  EU + CRES_U0 + (q[:, 1] - q[:, 1].min()) / max(np.ptp(q[:, 1]), 1e-6) * (CRES_U1 - CRES_U0)], 1)
    cm.append(q)
    print('placed crescent: %d nodes, right %+.4f..%+.4f  up %+.4f..%+.4f (%.0f mm of arc)' % (
        len(q), q[:, 0].min(), q[:, 0].max(), q[:, 1].min(), q[:, 1].max(),
        1000 * np.linalg.norm(np.diff(q, axis=0), axis=1).sum()))


_ch = np.load('isl_chart.npy')
_id, _n = np.unique(_ch, return_counts=True)
# the island is 33 uv charts: six of them are the head shells and the other 27 are fringe and spike pieces
FACE = np.isin(_ch, _id[_n >= 15000])
print('%d of %d charts are head shells, %d of %d texels' % ((_n >= 15000).sum(), len(_id), FACE.sum(), len(_ch)))


# a polyline drawn in the projected frame smears where the surface turns away, so each vertex gets lifted onto
# the nearest island texel and the distance is taken in 3d. the lift has to stay on the head shell: a fringe spike
# that overhangs the same (r,u) is nearer in 2d and would drag the whole segment out onto the hair
def lift(q):
    out = np.zeros((len(q), 3), np.float32)
    for i, (a, b) in enumerate(q):
        d = (r - a) ** 2 + (u - b) ** 2 + np.where(front & FACE, 0.0, 1.0)
        out[i] = P[int(np.argmin(d))]
    return out


def dist3(segs):
    best = np.full(len(P), 1e9, np.float32)
    for q in segs:
        L = lift(q)
        for i in range(len(L) - 1):
            a, b = L[i], L[i + 1]
            ab = b - a
            n2 = float(ab @ ab)
            if n2 < 1e-12:
                continue
            t = np.clip(((P - a) @ ab) / n2, 0, 1)
            d = np.linalg.norm(P - (a + t[:, None] * ab), axis=1)
            best = np.minimum(best, d)
    return best


dl = dist3(fm)
dc = dist3(cm) if cm else np.full(len(P), 1e9, np.float32)

beads = np.zeros(len(P), bool)
arc = []
for q in fm:
    s = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(q, axis=0), axis=1))])
    arc.append((q, s, s[-1]))
tot = sum(a[2] for a in arc)
step = tot / NBEAD
acc, want = 0.0, step * 0.5
bpts = []
for q, s, ln in arc:
    while want < acc + ln:
        t = want - acc
        i = int(np.searchsorted(s, t)) - 1
        i = max(0, min(i, len(q) - 2))
        w = (t - s[i]) / max(s[i + 1] - s[i], 1e-9)
        tg = q[i + 1] - q[i]
        bpts.append((q[i] + w * tg, tg / max(np.linalg.norm(tg), 1e-9)))
        want += step
    acc += ln
for b, tg in bpts:
    pr = np.array([-tg[1], tg[0]], np.float32)
    al = (r - b[0]) * tg[0] + (u - b[1]) * tg[1]
    pp = (r - b[0]) * pr[0] + (u - b[1]) * pr[1]
    beads |= front & ((al / BEAD_ALONG) ** 2 + (pp / BEAD_PERP) ** 2 <= 1.0)
print('%d beads placed along %.1f mm of stroke' % (len(bpts), 1000 * tot))

sq = np.abs((r - ER) / EYE_A) ** 1.6 + np.abs((u - EU) / EYE_B) ** 1.6
se = sq ** (1.0 / 1.6)
core = front & (se <= 1.0)
dr = np.hypot(r - ER, u - EU)
# the ramp is a disc, not a second lens. official's >0.90 region measures 29.7 x 15.8 mm at aspect 1.88, which is the
# core lens itself: the disc only clears 0.90 out to about the lens's own 8 mm half-axis, so the union reads as the lens.
gc = np.stack([np.interp(dr, GRR, GC[:, i]) for i in range(3)], 1).astype(np.float32)
gf = np.interp(dr, GRR, GC @ np.array([.2126, .7152, .0722], np.float32)) / 0.9829
glow = front & ~core & (dr < GRR[-1])

lines = front & (dl < LWMM * 0.0005) & ~core
beads &= ~core
crescent = front & (dc < LWMM * 0.0005) & ~core & ~glow & ~lines & ~beads

im = np.asarray(Image.open(V1).convert('RGB')).astype(np.float32) / 255.
X0, X1 = tex[:, 0].min(), tex[:, 0].max()
R0, R1 = irow.min(), irow.max()
PAD = 6
im[R0 - PAD:R1 + PAD + 1, X0 - PAD:X1 + PAD + 1] = HBASE

rr, cc = irow, tex[:, 0]
# the core is flat, not graded: official holds 0.983 all the way out to its rim and the blue only starts outside
CWHITE = np.array([0.9804, 0.9882, 1.0000], np.float32)
im[rr[crescent], cc[crescent]] = CCRES
im[rr[glow], cc[glow]] = gc[glow]
# a line crossing the bloom has to survive it, so the two take whichever is brighter per channel
lc = CLINE * np.interp(dr, LTR, LTV)[:, None]
im[rr[lines], cc[lines]] = np.maximum(lc[lines], gc[lines])
im[rr[core], cc[core]] = CWHITE
im[rr[beads], cc[beads]] = CBEAD

em = np.zeros((2048, 2048, 3), np.float32)
em[rr[crescent], cc[crescent]] = CCRES * 0.35 * EMK
em[rr[glow], cc[glow]] = CEYE * (gf[glow] * 0.85 * EMK)[:, None]
em[rr[lines], cc[lines]] = np.maximum(lc[lines] * 0.75, CEYE * (gf[lines] * 0.85)[:, None]) * EMK
em[rr[core], cc[core]] = CWHITE * EMK
em[rr[beads], cc[beads]] = CBEAD * EMK

v1 = np.asarray(Image.open(V1).convert('RGB')).astype(np.float32) / 255.
diff = np.abs(im - v1).max(2) > 1e-6
out = diff.copy()
out[R0 - PAD:R1 + PAD + 1, X0 - PAD:X1 + PAD + 1] = False
used = np.load('atlas_used.npy')
n = len(tex)
lit = int(core.sum() + glow.sum() + lines.sum() + beads.sum() + crescent.sum())
print('\neye core %d (%.2f%%) glow %d  lines %d (%.2f%%)  beads %d  crescent %d  = %d lit (%.2f%%)' % (
    core.sum(), 100.0 * core.sum() / n, glow.sum(), lines.sum(), 100.0 * lines.sum() / n,
    beads.sum(), crescent.sum(), lit, 100.0 * lit / n))
print('official: eye core 229 (0.27%) lines 401 (0.19%) beads 124 crescent 65, island 219k texels')
print("line width %.2f mm = %.1f of our texels; bead %.1f x %.1f mm; eye core %.1f x %.1f mm, bloom radius %.1f mm" % (LWMM, LWMM / MMT, 2000 * BEAD_PERP, 2000 * BEAD_ALONG, 2000 * EYE_A, 2000 * EYE_B, 1000 * GRR[-1]))
print('changed texels %d, outside the island rect %d; used-atlas texels in the rect %d; body texel delta %.1e' % (
    diff.sum(), out.sum(), used[R0 - PAD:R1 + PAD + 1, X0 - PAD:X1 + PAD + 1].sum(),
    np.abs(im[226, 326] - v1[226, 326]).max()))
Image.fromarray((np.clip(im, 0, 1) * 255).round().astype(np.uint8)).save(SPIKE + 'BS_Body_2048.png')
Image.fromarray((np.clip(em, 0, 1) * 255).round().astype(np.uint8)).save(SPIKE + 'BS_Body_Emission.png')

PPM = 1600.0
RA, RB, UA, UB = -0.13, 0.13, -0.05, 0.30
w, h = int((RB - RA) * PPM), int((UB - UA) * PPM)
img = np.zeros((h, w, 3), np.float32)
k = np.nonzero(front)[0]
k = k[np.argsort(f[k])]
cx = ((r[k] - RA) * PPM).astype(int)
cy = (h - 1 - (u[k] - UA) * PPM).astype(int)
ok = (cx >= 0) & (cx < w) & (cy >= 0) & (cy < h)
img[cy[ok], cx[ok]] = im[rr[k][ok], cc[k][ok]]
Image.fromarray((np.clip(img, 0, 1) ** 0.65 * 255).astype(np.uint8)).save('proj_ours.png')
print('wrote albedo, emission and proj_ours.png')
