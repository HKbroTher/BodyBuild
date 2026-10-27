"""MASS FACTORY creature phases v2 (Blender 4.x, headless).

Five discrete forms per evolution line instead of 16 gradual ones. Bodies are built as overlapping
anatomical volumes in one bmesh, then fused (voxel remesh), softened, displaced with noise, decimated
to a faceted low-poly budget and painted with curvature-based vertex colours (no textures, no UVs).

Usage:
  blender -b --python Tools/Blender/creature_phases.py -- --kind tentacle --phases 1,2 --out <dir>
"""
import argparse
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import creature_generator as base  # noqa: E402  (stage, lights, camera, vignette, simple shapes)

# ----------------------------------------------------------------------------------------------
# Volume primitives written straight into one bmesh
# ----------------------------------------------------------------------------------------------


def _rot(rot):
    return Euler([math.radians(a) for a in rot]).to_matrix().to_4x4()


def ell(bm, center, radii, rot=(0, 0, 0), seg=20):
    res = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(8, seg // 2 + 2), radius=1.0)
    matrix = Matrix.Translation(Vector(center)) @ _rot(rot) @ Matrix.Diagonal((*radii, 1.0))
    bmesh.ops.transform(bm, matrix=matrix, verts=res["verts"])


def ell_along(bm, a, b, rx, ry, extra=1.15, seg=18):
    """Ellipsoid whose long axis spans a->b (slightly longer so joints overlap)."""
    a, b = Vector(a), Vector(b)
    axis = b - a
    res = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(8, seg // 2 + 2), radius=1.0)
    quat = axis.normalized().to_track_quat("Z", "Y")
    matrix = (Matrix.Translation((a + b) * 0.5) @ quat.to_matrix().to_4x4()
              @ Matrix.Diagonal((rx, ry, axis.length * 0.5 * extra, 1.0)))
    bmesh.ops.transform(bm, matrix=matrix, verts=res["verts"])


def tentacle_bm(bm, base_pt, direction, length, r0, r1, curl, segs=14, sides=10, curl_axis=None, wave=0.0):
    d = Vector(direction).normalized()
    axis = Vector(curl_axis) if curl_axis is not None else d.cross(Vector((0, 0, 1)))
    if axis.length < 1e-4:
        axis = Vector((1, 0, 0))
    axis.normalize()
    step = length / segs
    pts, dirs = [], []
    p = Vector(base_pt)
    for i in range(segs + 1):
        pts.append(p.copy())
        dirs.append(d.copy())
        t = i / segs
        angle = curl / segs * (0.3 + 1.7 * t) + wave * math.sin(t * math.pi * 3.0) / segs
        d = (Matrix.Rotation(angle, 3, axis) @ d).normalized()
        p = p + d * step
    rings = []
    ref = Vector((0, 0, 1)) if abs(dirs[0].z) < 0.9 else Vector((1, 0, 0))
    x = dirs[0].cross(ref).normalized()
    for i, (p, d) in enumerate(zip(pts, dirs)):
        x = (x - d * d.dot(x)).normalized()
        y = d.cross(x).normalized()
        r = r0 + (r1 - r0) * (i / segs) ** 0.8
        rings.append([bm.verts.new(p + (x * math.cos(2 * math.pi * k / sides) + y * math.sin(2 * math.pi * k / sides)) * r)
                      for k in range(sides)])
    for a, b in zip(rings, rings[1:]):
        for k in range(sides):
            bm.faces.new((a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k]))
    tip = bm.verts.new(pts[-1] + dirs[-1] * max(r1, 0.005) * 2.0)
    for k in range(sides):
        bm.faces.new((rings[-1][k], rings[-1][(k + 1) % sides], tip))
    bm.faces.new(list(reversed(rings[0])))
    return pts, dirs


def warp(bm, fn):
    for v in bm.verts:
        v.co = fn(v.co.copy())


# ----------------------------------------------------------------------------------------------
# Fuse -> soften -> displace -> decimate -> paint
# ----------------------------------------------------------------------------------------------


def _evaluate(obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    return bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph))


def fuse(name, bm, parent=None, voxel=0.012, smooth=0.6, smooth_iter=4, noise_strength=0.006, noise_scale=0.06,
         target_tris=5000):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent

    remesh = obj.modifiers.new("Remesh", "REMESH")
    remesh.mode = "VOXEL"
    remesh.voxel_size = voxel
    sm = obj.modifiers.new("Smooth", "SMOOTH")
    sm.factor = smooth
    sm.iterations = smooth_iter
    if noise_strength > 0:
        tex = bpy.data.textures.new(name + "_noise", "CLOUDS")
        tex.noise_scale = noise_scale
        tex.noise_depth = 2
        disp = obj.modifiers.new("Displace", "DISPLACE")
        disp.texture = tex
        disp.strength = noise_strength
        disp.texture_coords = "GLOBAL"
    dense = _evaluate(obj)
    tris = sum(len(p.vertices) - 2 for p in dense.polygons)
    bpy.data.meshes.remove(dense)
    dec = obj.modifiers.new("Decimate", "DECIMATE")
    dec.decimate_type = "COLLAPSE"
    dec.ratio = min(1.0, target_tris / max(1, tris))
    final = _evaluate(obj)
    obj.modifiers.clear()
    old = obj.data
    obj.data = final
    bpy.data.meshes.remove(old)
    for poly in obj.data.polygons:
        poly.use_smooth = False
    return obj


def paint(obj, color_fn, cavity=1.6, jitter=0.07, seed=1, roughness=0.5, emission_fn=None):
    """Curvature-shaded, per-face-jittered vertex colours stored as a linear float corner attribute."""
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    bm.normal_update()
    curv = [0.0] * len(bm.verts)
    for v in bm.verts:
        if not v.link_edges:
            continue
        total = 0.0
        for e in v.link_edges:
            d = e.other_vert(v).co - v.co
            if d.length > 1e-9:
                total += d.normalized().dot(v.normal)
        curv[v.index] = total / len(v.link_edges)   # >0 concave crease, <0 convex ridge
    normals = [v.normal.copy() for v in bm.verts]
    bm.free()

    attr = me.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
    rnd = random.Random(seed)
    mw = obj.matrix_world
    for poly in me.polygons:
        j = 1.0 + rnd.uniform(-jitter, jitter)
        for li in poly.loop_indices:
            vi = me.loops[li].vertex_index
            co = mw @ me.vertices[vi].co
            c = color_fn(co, normals[vi])
            k = curv[vi]
            shade = max(0.25, 1.0 - cavity * max(0.0, k)) * (1.0 + 0.6 * max(0.0, -k))
            attr.data[li].color = (c[0] * shade * j, c[1] * shade * j, c[2] * shade * j, 1.0)

    mat = bpy.data.materials.new(obj.name + "_mat")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes["Principled BSDF"]
    col = nodes.new("ShaderNodeAttribute")
    col.attribute_name = "Col"
    mat.node_tree.links.new(col.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = roughness
    if emission_fn is not None:
        emission_fn(mat, col)
    me.materials.clear()
    me.materials.append(mat)
    return obj


def mix(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(x + (y - x) * t for x, y in zip(a, b))


# ----------------------------------------------------------------------------------------------
# Anatomy
# ----------------------------------------------------------------------------------------------


def torso(bm, pump=1.0):
    p = pump
    ell(bm, (0, 0.03, 1.40), (0.24 * p, 0.17 * p, 0.22))                    # ribcage
    for s in (-1, 1):
        ell(bm, (s * 0.115, -0.11 - 0.02 * p, 1.45), (0.13 * p, 0.075 * p, 0.09 * p), rot=(8, 0, s * -8))  # pecs
        ell(bm, (s * 0.20, 0.06, 1.32), (0.13 * p, 0.10, 0.21), rot=(0, s * 18, 0))  # lats
        ell(bm, (s * 0.15, -0.02, 1.12), (0.065, 0.09, 0.11))                # obliques
        ell(bm, (s * 0.10, 0.05, 1.60), (0.10 * p, 0.07, 0.065 * p))          # trap slopes
        ell(bm, (s * 0.09, 0.08, 0.95), (0.10, 0.09, 0.10))                   # glutes
        for row, z in enumerate((1.25, 1.16, 1.07)):
            ell(bm, (s * 0.047, -0.112, z), (0.046, 0.032, 0.039), seg=12)    # six pack
    ell(bm, (0, -0.02, 1.17), (0.15, 0.12, 0.16))                              # abdomen
    ell(bm, (0, 0.06, 1.61), (0.17 * p, 0.09, 0.08))                           # traps
    ell(bm, (0, 0.0, 0.98), (0.19, 0.13, 0.12))                                # pelvis


def leg(bm, s, bow=0.0, thick=1.0):
    hip = Vector((s * 0.12, 0, 0.98))
    knee = Vector((s * (0.155 + bow), -0.02, 0.53))
    ankle = Vector((s * (0.15 + bow * 0.5), 0.02, 0.09))
    t = thick
    ell_along(bm, hip, knee, 0.11 * t, 0.12 * t)
    ell(bm, hip.lerp(knee, 0.55) + Vector((s * 0.06, -0.02, 0)), (0.075 * t, 0.085 * t, 0.14))   # vastus lateralis
    ell(bm, knee + Vector((-s * 0.05, -0.045, 0.09)), (0.062 * t, 0.06 * t, 0.075))               # teardrop
    ell(bm, hip.lerp(knee, 0.5) + Vector((0, 0.07, 0)), (0.08 * t, 0.07 * t, 0.16))              # hamstrings
    ell(bm, knee + Vector((0, -0.05, 0)), (0.05, 0.04, 0.05), seg=12)                              # knee cap
    ell_along(bm, knee, ankle, 0.06 * t, 0.065 * t)
    ell(bm, knee.lerp(ankle, 0.3) + Vector((s * 0.022, 0.05, 0)), (0.07 * t, 0.07 * t, 0.12))     # calf
    ell(bm, ankle + Vector((0, -0.07, -0.045)), (0.055, 0.125, 0.04), seg=12)                     # foot
    return hip, knee, ankle


def double_biceps_arm(bm, s, pump=1.0):
    p = pump
    shoulder = Vector((s * 0.30, 0.02, 1.52))
    elbow = Vector((s * 0.66, 0.0, 1.56))
    wrist = Vector((s * 0.64, -0.05, 1.88))
    ell(bm, (s * 0.31, 0.0, 1.53), (0.125 * p, 0.12 * p, 0.115 * p))              # delt
    ell(bm, (s * 0.29, -0.075, 1.50), (0.07 * p, 0.06, 0.085))                       # front delt
    ell_along(bm, shoulder, elbow, 0.07 * p, 0.075 * p)
    mid = shoulder.lerp(elbow, 0.55)
    ell(bm, mid + Vector((0, -0.025, 0.065 * p)), (0.15, 0.085 * p, 0.095 * p))     # biceps peak
    ell(bm, mid + Vector((0, 0.03, -0.045)), (0.15, 0.07 * p, 0.07 * p))            # triceps
    ell_along(bm, elbow, wrist, 0.06 * p, 0.055 * p)
    ell(bm, elbow + Vector((0, -0.025, 0.09)), (0.072 * p, 0.07 * p, 0.1))          # brachioradialis
    ell(bm, wrist + Vector((0, -0.015, 0.07)), (0.08, 0.075, 0.085), seg=14)          # fist
    ell(bm, wrist + Vector((-s * 0.02, -0.06, 0.08)), (0.05, 0.035, 0.05), seg=10)    # knuckles
    return shoulder, elbow, wrist


def head(bm, center=(0, 0.0, 1.78), tilt=(0, 0, 0), jaw_open=0.0):
    m = Matrix.Translation(Vector(center)) @ _rot(tilt)

    def at(local):
        return m @ Vector(local)

    ell_along(bm, (0, 0.02, 1.56), at((0, 0.0, -0.06)), 0.085, 0.08)                 # neck
    ell(bm, at((0, 0.01, 0.02)), (0.095, 0.11, 0.12), rot=tilt)                         # cranium
    ell(bm, at((0, -0.035, -0.06 - jaw_open)), (0.085, 0.08, 0.058), rot=tilt)          # jaw
    ell(bm, at((0, -0.085, 0.03)), (0.082, 0.03, 0.026), rot=tilt, seg=12)              # brow
    ell(bm, at((0, -0.105, -0.015)), (0.019, 0.026, 0.036), rot=(tilt[0] - 15, tilt[1], tilt[2]), seg=10)  # nose
    for s in (-1, 1):
        ell(bm, at((s * 0.095, 0.0, 0.0)), (0.016, 0.03, 0.036), rot=tilt, seg=10)    # ears
        ell(bm, at((s * 0.055, -0.08, -0.03)), (0.03, 0.025, 0.022), rot=tilt, seg=10)  # cheekbones
    return m


# ----------------------------------------------------------------------------------------------
# Tentacle line, phases 1-2
# ----------------------------------------------------------------------------------------------

SKIN_P1 = (0.42, 0.17, 0.075)
TRUNKS = (0.35, 0.015, 0.02)


def accessories_p1(root, head_matrix, belt_tilt=0.0):
    gold = base.material("gold", (1.0, 0.7, 0.25), metallic=1.0, roughness=0.22)
    trunks = base.material("trunks", TRUNKS, roughness=0.45)
    white = base.material("eye_white", (0.85, 0.82, 0.75), roughness=0.3)
    dark = base.material("pupil", (0.01, 0.01, 0.01), roughness=0.2)
    hair = base.material("hair", (0.025, 0.018, 0.012), roughness=0.85)
    base.shape("ell", "Trunks", root, (0.40, 0.29, 0.2), (0, 0.0, 0.97), trunks, segments=12, bevel=0.0)
    base.shape("cyl", "Belt", root, (0.37, 0.30, 0.07), (0, -0.005, 1.065), gold, rot=(belt_tilt, 0, 0), segments=16, bevel=0.012)
    base.shape("hex", "Buckle", root, (0.12, 0.035, 0.1), (0, -0.155, 1.065), gold, rot=(belt_tilt, 0, 0), bevel=0.012)

    def at(local):
        return head_matrix @ Vector(local)

    rot = [math.degrees(a) for a in head_matrix.to_euler()]
    base.shape("ell", "Hair", root, (0.2, 0.225, 0.07), at((0, 0.012, 0.105)), hair, rot=rot, segments=10)
    for s in (-1, 1):
        base.shape("ell", "Eye", root, (0.036, 0.016, 0.02), at((s * 0.04, -0.093, 0.0)), white, rot=rot, segments=8)
        base.shape("ell", "Pupil", root, (0.014, 0.01, 0.016), at((s * 0.04, -0.1, 0.0)), dark, rot=rot, segments=6)


def phase1(root):
    bm = bmesh.new()
    torso(bm, pump=1.15)
    for s in (-1, 1):
        leg(bm, s)
        double_biceps_arm(bm, s, pump=1.15)
    hm = head(bm)

    def skin(co, n):
        c = SKIN_P1
        pumped = 0.5 + 0.5 * noise.noise(co * 6.0)
        c = mix(c, (0.5, 0.15, 0.07), 0.25 * pumped)           # flushed, pumped muscle
        c = mix(c, (0.22, 0.09, 0.05), max(0.0, 0.6 - co.z) * 0.6)   # darker toward the feet
        return c

    body = fuse("P1_Body", bm, root, voxel=0.01, smooth=0.4, smooth_iter=2, noise_strength=0.004, target_tris=3800)
    paint(body, skin, cavity=3.2, jitter=0.09, roughness=0.38)  # oiled-up competition skin
    accessories_p1(root, hm)
    return root


def phase2(root):
    """The Split: hunched, asymmetric, chest torn open into a vertical maw full of rib-teeth and tendrils."""
    rnd = random.Random(21)
    bm = bmesh.new()
    torso(bm, pump=1.3)
    leg(bm, -1, bow=0.06, thick=1.25)
    leg(bm, 1, bow=0.09, thick=1.35)

    # Right arm: monstrous, swollen, dragging on the floor.
    sh = Vector((0.34, 0.02, 1.5))
    el = Vector((0.62, -0.08, 0.98))
    wr = Vector((0.68, -0.2, 0.32))
    ell(bm, (0.36, 0.0, 1.53), (0.2, 0.19, 0.18))
    ell_along(bm, sh, el, 0.16, 0.17)
    ell(bm, sh.lerp(el, 0.5) + Vector((0.03, -0.08, 0)), (0.17, 0.15, 0.24), rot=(0, -35, 0))
    ell_along(bm, el, wr, 0.15, 0.14)
    ell(bm, el.lerp(wr, 0.3) + Vector((0.05, -0.04, 0)), (0.16, 0.14, 0.2), rot=(0, -10, 0))
    ell(bm, wr + Vector((0.0, -0.05, -0.16)), (0.17, 0.16, 0.13))     # club-like fist on the ground
    for k in range(9):
        t = k / 8.0
        p0 = sh.lerp(el, t * 2) if t < 0.5 else el.lerp(wr, (t - 0.5) * 2)
        off = Vector((rnd.uniform(-0.1, 0.1), -0.13, rnd.uniform(-0.05, 0.05)))
        ell(bm, p0 + off, (0.035, 0.03, 0.16), rot=(rnd.uniform(-30, 30), rnd.uniform(-30, 30), 0), seg=10)  # vein cords
    for k in range(5):
        p0 = sh.lerp(wr, rnd.uniform(0.2, 0.9)) + Vector((rnd.uniform(0.05, 0.14), rnd.uniform(-0.05, 0.1), 0))
        r = rnd.uniform(0.06, 0.1)
        ell(bm, p0, (r, r, r * 1.2), seg=12)                                 # tumours
    # Left arm: withered and twisted up behind the head.
    ls = Vector((-0.27, 0.04, 1.52))
    le = Vector((-0.42, 0.16, 1.82))
    lw = Vector((-0.2, 0.18, 2.05))
    ell(bm, (-0.29, 0.02, 1.52), (0.1, 0.1, 0.09))
    ell_along(bm, ls, le, 0.045, 0.045)
    ell_along(bm, le, lw, 0.035, 0.035)
    ell(bm, lw, (0.04, 0.04, 0.05), seg=10)

    # Lips of the chest maw.
    for s in (-1, 1):
        ell(bm, (s * 0.1, -0.17, 1.33), (0.075, 0.08, 0.3), rot=(0, s * -8, s * 18))
    # Tumours along the back and the swollen side.
    for _ in range(14):
        pos = Vector((rnd.uniform(-0.05, 0.38), rnd.uniform(0.08, 0.2), rnd.uniform(1.15, 1.65)))
        r = rnd.uniform(0.05, 0.11)
        ell(bm, pos, (r, r * 0.9, r * 1.1), seg=12)

    # Head lolls onto the left shoulder, jaw hanging open.
    hm = head(bm, center=(-0.17, -0.06, 1.66), tilt=(25, -65, 10), jaw_open=0.04)

    def hunch(co):
        # Fold the upper body forward around the waist; the swollen right side sags further.
        if co.z > 1.0:
            t = min(1.0, (co.z - 1.0) / 0.9)
            angle = math.radians(14) * t + math.radians(10) * t * max(0.0, co.x) * 2
            rel = co - Vector((0, 0, 1.0))
            co = Vector((0, 0, 1.0)) + Matrix.Rotation(-angle, 3, "X") @ rel
        return co

    warp(bm, hunch)

    def skin(co, n):
        c = mix((0.2, 0.19, 0.1), (0.12, 0.13, 0.08), 0.5 + 0.5 * noise.noise(co * 3.0))    # sickly olive grey
        bruise = max(0.0, noise.noise(co * 6.0 + Vector((3, 1, 2))))
        c = mix(c, (0.16, 0.03, 0.1), min(1.0, bruise * 1.6))                               # purple bruising
        maw = (co - Vector((0, -0.2, 1.3))).length
        c = mix(c, (0.4, 0.03, 0.03), max(0.0, 0.42 - maw) * 3.0)                           # raw red around the split
        return c

    body = fuse("P2_Body", bm, root, voxel=0.011, smooth=0.4, smooth_iter=2, noise_strength=0.014,
                noise_scale=0.05, target_tris=7500)
    paint(body, skin, cavity=3.0, jitter=0.12, roughness=0.35)

    # Maw interior, rib teeth and the tendrils boiling out of it.
    inside = bmesh.new()
    ell(inside, (0, -0.13, 1.33), (0.09, 0.07, 0.29), seg=16)
    inside_obj = fuse("P2_MawInside", inside, root, voxel=0.012, smooth=0.3, smooth_iter=2, noise_strength=0.0,
                      target_tris=800)
    warp_obj(inside_obj, hunch)

    def wet_red(mat, col):
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Emission Color"].default_value = (0.5, 0.02, 0.02, 1)
        bsdf.inputs["Emission Strength"].default_value = 3.0

    paint(inside_obj, lambda co, n: (0.18, 0.01, 0.015), cavity=1.0, roughness=0.15, emission_fn=wet_red)

    bone = base.material("bone", (0.72, 0.64, 0.5), roughness=0.45)
    for s in (-1, 1):
        for k in range(7):
            z = 1.08 + k * 0.075
            pos = hunch(Vector((s * 0.075, -0.21, z)))
            base.shape("cone", "RibTooth", root, (0.05, 0.05, 0.17 - abs(k - 3) * 0.012), pos, bone,
                       rot=(rnd.uniform(-10, 10), s * -75, 0), segments=5)

    tbm = bmesh.new()
    for k in range(9):
        z = 1.12 + k * 0.05
        start = hunch(Vector((rnd.uniform(-0.03, 0.03), -0.15, z)))
        direction = Vector((rnd.uniform(-0.9, 0.9), -1.0, rnd.uniform(-0.7, 0.9)))
        tentacle_bm(tbm, start, direction, rnd.uniform(0.55, 1.1), 0.06, 0.008, rnd.choice((-1, 1)) * rnd.uniform(2.0, 3.5),
                    curl_axis=Vector((rnd.uniform(-1, 1), 0.2, rnd.uniform(-1, 1))), wave=2.0)
    tent = fuse("P2_Tendrils", tbm, root, voxel=0.008, smooth=0.4, smooth_iter=2, noise_strength=0.002,
                noise_scale=0.03, target_tris=4000)
    paint(tent, lambda co, n: mix((0.32, 0.07, 0.14), (0.55, 0.25, 0.3), 0.5 + 0.5 * noise.noise(co * 12.0)),
          cavity=2.0, roughness=0.25)

    # What's left of the champion: one bulging eye, a torn belt hanging off one hip.
    accessories_p2(root, hm, hunch)
    root.scale = (1.7, 1.7, 1.7)
    return root


def warp_obj(obj, fn):
    for v in obj.data.vertices:
        v.co = fn(v.co.copy())


def accessories_p2(root, head_matrix, hunch):
    gold = base.material("gold", (1.0, 0.7, 0.25), metallic=1.0, roughness=0.22)
    trunks = base.material("trunks", TRUNKS, roughness=0.45)
    white = base.material("eye_bulge", (0.85, 0.75, 0.6), roughness=0.2)
    red = base.material("eye_red", (0.6, 0.03, 0.02), roughness=0.2)
    base.shape("ell", "Trunks", root, (0.46, 0.33, 0.22), (0, 0.0, 0.97), trunks, segments=12)
    base.shape("cyl", "BeltTorn", root, (0.42, 0.34, 0.07), (0.04, -0.01, 1.0), gold, rot=(0, -24, 0), segments=16, bevel=0.012)
    base.shape("hex", "Buckle", root, (0.12, 0.035, 0.1), (0.16, -0.17, 0.95), gold, rot=(0, -24, 0), bevel=0.012)

    def at(local):
        return hunch(head_matrix @ Vector(local))

    rot = [math.degrees(a) for a in head_matrix.to_euler()]
    base.shape("ell", "BulgingEye", root, (0.07, 0.06, 0.07), at((-0.04, -0.1, 0.0)), white, rot=rot, segments=10)
    base.shape("ell", "Iris", root, (0.03, 0.02, 0.03), at((-0.04, -0.15, 0.0)), red, rot=rot, segments=8)


PHASES = {"tentacle": {1: phase1, 2: phase2}}

# ----------------------------------------------------------------------------------------------
# Rendering
# ----------------------------------------------------------------------------------------------

VIEWS = {"front": Vector((0.0, -1.0, 0.1)), "threequarter": Vector((-0.65, -0.8, 0.14)), "side": Vector((-1.0, -0.12, 0.1))}


def render(kind, phase, view, out, width, height):
    base.reset_scene()
    scene = bpy.context.scene
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    root = base.empty("Creature")
    PHASES[kind][phase](root)
    lo, hi = base.creature_bounds(root)
    size = max(hi.z - min(lo.z, 0.0), (hi.x - lo.x) * height / width)
    frame = max(2.3, size * 1.12)
    base.build_stage(max(1.0, frame / 2.6))
    cam = base.place_camera(frame, frame * 0.47)
    target = Vector((0, 0, frame * 0.47))
    distance = (cam.location - target).length
    cam.location = target + VIEWS[view].normalized() * distance
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    path = os.path.join(out, "%s_P%d_%s.png" % (kind, phase, view))
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    tris = sum(len(p.vertices) - 2 for o in root.children_recursive if o.type == "MESH" for p in o.data.polygons)
    print("rendered", path, "tris", tris)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--kind", default="tentacle")
    parser.add_argument("--phases", default="1,2")
    parser.add_argument("--views", default="front,threequarter,side")
    parser.add_argument("--out", required=True)
    parser.add_argument("--width", type=int, default=600)
    parser.add_argument("--height", type=int, default=800)
    args = parser.parse_args(argv)
    os.makedirs(args.out, exist_ok=True)
    for phase in [int(p) for p in args.phases.split(",")]:
        for view in args.views.split(","):
            render(args.kind, phase, view, args.out, args.width, args.height)


if __name__ == "__main__":
    main()
