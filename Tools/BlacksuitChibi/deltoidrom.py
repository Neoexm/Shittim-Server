"""Deltoid range-of-motion check on the chibi suit: does a shoulder weight change that helps the rest pose hurt the posed ones.

Run headless. blender.exe -b file.blend --python this.py hangs forever on this machine (rc 124 at a 180 s timeout) unless --factory-startup is passed; it is the user startup config or an addon in it, not a file lock and not one particular blend, and killing the leftover Blender that held the .blend made no difference. Every invocation here needs the flag.

What the phase-4 shoulder fix actually is, measured between BS_phase4.blend and BS_phase4_fix.blend: 42 of BS_Suit's 768 verts, moved at most 1.99 mm, reweighted across both clavicles, both upper arms, both upper-arm twists and Spine1, with a largest single weight delta of 0.603. BS_Body is untouched to the bit in every pose, so this is a suit-only change however much the shoulder renders make it look like a body one. Bone_b_L/R also appear in the weight diff but only as renormalisation spill of 0.0013 or less; they are the chest bones off Spine1 and nothing here drives them. BS_p4_mid and BS_p4_soft are the weaker variants of the same edit - soft moves 24 verts by at most 0.36 mm and is close enough to a no-op that it buys back almost nothing. BS_phase5_headuv.blend, which is what the FBX came from, reports every number in this file identically to phase4_fix, which is the check that the head re-unwrap left the skinning alone.

The verdict is a trade and it is worth understanding before touching the weights again. Rest improves on both sides (dihedral p95 154.7 -> 153.0 left, 153.4 -> 151.4 right, and the count of over-90-degree edges 12 -> 10 on each). A 70-degree coronal raise and a 60-degree forward swing both come out with more added bend than before: left raise dgain p95 86.2 -> 105.2, right forward 48.3 -> 65.0, and per edge the left raise has 22 edges bending more than 5 degrees further against 7 bending less. The 110-degree across pose improves clearly on both sides. Edge-length strain is very slightly better in the fixed variant at every angle on both sides, so nothing is being stretched worse - what moves is where the shell chooses to fold.

The added-bend regression is not monotone in raise angle, which is why the single 70-degree sample overstates it: sweeping the left raise gives dgain p95 15.8 -> 31.9 at 40 degrees, 62.5 -> 58.2 at 55, 86.2 -> 105.2 at 70, 85.4 -> 80.4 at 85. Two of those four are improvements.

What settles whether any of it matters is the reach of the official clips, measured separately in Unity over the 34 clips in Assets/_BlacksuitSpike/Anim: the upper arm swings up to 88.8 degrees from bind on the left and 81.8 on the right, so these test angles are all in range for magnitude, but by elevation the two sides are nothing alike. Bind sits at -33.7 degrees on the left and -39.8 on the right. The left reaches +51.6 (Victory_Start, and the three Callsign clips are near it), i.e. an 85-degree raise, where the fixed variant is the better of the two. The right never rises above -26.3 in any clip, a raise of 13 degrees, so the right-side raise regression - the one that looks worst of all in the clay renders, a shard standing off the lapel - is in a pose no official animation can put the model in. That leaves the forward swing as the only regression that is both real and reachable, and it is three to five edges deep with a worst case of +28 degrees.

Renders live in the scratchpad, not here: rom/romcmp.png is before/after/diff per pose, rom/rom11.png the 1:1 crops that the visual read above comes from, rom/romzoom.png a 5x blowup of the hottest window per pose across all four variants.
"""
import bpy
import sys
import json
import math
from mathutils import Vector, Matrix

WORK = r"c:\Users\tomda\Documents\Shittim-Server\JP_Dev\Src\BSComplex\work"
R = 0.055
POSES = [("rest", None, 0.0), ("raised", "up", 70.0), ("forward", "fwd", 60.0), ("across", "fwd", 110.0)]
MESHES = ("BS_Suit", "BS_Body")

arm = bpy.data.objects["Armature"]
bn = arm.data.bones


def deformed(name):
    o = bpy.data.objects[name]
    ev = o.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = ev.to_mesh()
    co = [o.matrix_world @ v.co for v in me.vertices]
    ev.to_mesh_clear()
    return co


def topo(name):
    ef = {}
    for p in bpy.data.objects[name].data.polygons:
        vs = list(p.vertices)
        for k in range(len(vs)):
            e = (min(vs[k], vs[(k + 1) % len(vs)]), max(vs[k], vs[(k + 1) % len(vs)]))
            ef.setdefault(e, []).append(tuple(vs))
    return ef


def tnorm(co, vs):
    # newell, because the suit has quads and to_mesh does not triangulate
    n = Vector()
    for k in range(len(vs)):
        a, b = co[vs[k]], co[vs[(k + 1) % len(vs)]]
        n.x += (a.y - b.y) * (a.z + b.z)
        n.y += (a.z - b.z) * (a.x + b.x)
        n.z += (a.x - b.x) * (a.y + b.y)
    return n.normalized() if n.length > 1e-12 else Vector((0, 0, 1))


def pct(v, q):
    v = sorted(v)
    return v[min(len(v) - 1, int(q * len(v)))]


def setpose(side, kind, deg):
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    if kind is None:
        return
    pb = arm.pose.bones["Bip001 %s UpperArm" % side]
    sgn = 1.0 if side == "L" else -1.0
    # the chibi faces +y with +x to its left, so a coronal lift is about -y on the left and a forward swing is about +z
    ax = Vector((0, -sgn, 0)) if kind == "up" else Vector((0, 0, sgn))
    h = pb.matrix.to_translation()
    pb.matrix = Matrix.Translation(h) @ Matrix.Rotation(math.radians(deg), 4, ax) @ Matrix.Translation(-h) @ pb.matrix
    bpy.context.view_layer.update()


res = {}
tp = {m: topo(m) for m in MESHES}
setpose("L", None, 0)
rest = {m: deformed(m) for m in MESHES}

for side in ("L", "R"):
    sh = Vector(bn["Bip001 %s UpperArm" % side].head_local)
    for m in MESHES:
        near = set(i for i, c in enumerate(rest[m]) if (c - sh).length < R and (c.x > 0 if side == "L" else c.x < 0))
        for pname, kind, deg in POSES:
            setpose(side, kind, deg)
            co = deformed(m)
            st, dh, dgain = [], [], []
            for e, fs in tp[m].items():
                if e[0] not in near and e[1] not in near:
                    continue
                l0 = (rest[m][e[0]] - rest[m][e[1]]).length
                if l0 > 1e-6:
                    st.append(abs(math.log((co[e[0]] - co[e[1]]).length / l0)))
                if len(fs) == 2:
                    a1 = math.degrees(tnorm(co, fs[0]).angle(tnorm(co, fs[1])))
                    dh.append(a1)
                    dgain.append(a1 - math.degrees(tnorm(rest[m], fs[0]).angle(tnorm(rest[m], fs[1]))))
            res["%s|%s|%s" % (side, m, pname)] = dict(
                nvert=len(near), nedge=len(st), strain_p95=pct(st, .95), strain_max=max(st),
                dih_p95=pct(dh, .95), dih_max=max(dh), dih_over90=sum(1 for a in dh if a > 90),
                dgain_p95=pct(dgain, .95), dgain_max=max(dgain))

json.dump(res, open(sys.argv[sys.argv.index("--") + 1], "w"), indent=0)
