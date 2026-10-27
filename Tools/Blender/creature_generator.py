"""MASS FACTORY creature generator (Blender 4.x, run headless).

Builds the low-poly, bevelled "ForgeTax-style" athlete for any level 1-16 of an evolution line and renders
preview images. Everything is procedural: no external meshes or textures.

Usage:
  blender -b --python Tools/Blender/creature_generator.py -- --kind tentacle --levels 1-16 --out <dir>

Stages (4 levels each): 1 bodybuilder, 2 mutation, 3 monster, 4 elder god.
"""
import argparse
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

# ----------------------------------------------------------------------------------------------
# Scene helpers
# ----------------------------------------------------------------------------------------------

MATS = {}


def material(name, color, metallic=0.0, roughness=0.6, emission=None, strength=0.0):
    if name in MATS:
        return MATS[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = strength
    MATS[name] = mat
    return mat


def mesh_object(name, bm, mat, parent=None, bevel=0.0):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    me.materials.append(mat)
    if parent is not None:
        obj.parent = parent
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 1
        mod.limit_method = "ANGLE"
        mod.angle_limit = math.radians(35)
        mod.use_clamp_overlap = True
    return obj


def empty(name, parent=None, loc=(0, 0, 0), rot=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = loc
    obj.rotation_euler = Euler([math.radians(a) for a in rot])
    return obj


def shape(kind, name, parent, size, loc, mat, rot=(0, 0, 0), bevel=0.0, segments=8, taper=1.0):
    """kind: box | ell (faceted ellipsoid) | cyl (tapered prism along Z) | hex (6-sided stud, axis Y)."""
    bm = bmesh.new()
    if kind == "box":
        bmesh.ops.create_cube(bm, size=1.0)
    elif kind == "ell":
        bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=max(4, segments * 2 // 3), radius=0.5)
    elif kind == "cyl":
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments, radius1=0.5,
                              radius2=0.5 * taper, depth=1.0)
    elif kind == "hex":
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=6, radius1=0.5, radius2=0.42, depth=1.0)
        bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "X"), verts=bm.verts)
    elif kind == "cone":
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments, radius1=0.5, radius2=0.0, depth=1.0)
    matrix = (Matrix.Translation(Vector(loc)) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4()
              @ Matrix.Diagonal((*size, 1.0)))
    bmesh.ops.transform(bm, matrix=matrix, verts=bm.verts)
    return mesh_object(name, bm, mat, parent, bevel)


def tentacle(name, parent, base, direction, length, r0, r1, curl, mat, segs=9, sides=6, curl_axis=None,
             sucker_mat=None, wave=0.0):
    """Faceted tapered tentacle swept along a curling path (parallel-transport frames)."""
    d = Vector(direction).normalized()
    axis = Vector(curl_axis) if curl_axis is not None else d.cross(Vector((0, 0, 1)))
    if axis.length < 1e-4:
        axis = Vector((1, 0, 0))
    axis.normalize()
    step = length / segs
    pts, dirs = [], []
    p = Vector(base)
    for i in range(segs + 1):
        pts.append(p.copy())
        dirs.append(d.copy())
        t = i / segs
        angle = curl / segs * (0.4 + 1.6 * t) + wave * math.sin(t * math.pi * 2.0) / segs
        d = (Matrix.Rotation(angle, 3, axis) @ d).normalized()
        p = p + d * step

    bm = bmesh.new()
    rings = []
    ref = Vector((0, 0, 1)) if abs(dirs[0].z) < 0.9 else Vector((1, 0, 0))
    x = dirs[0].cross(ref).normalized()
    for i, (p, d) in enumerate(zip(pts, dirs)):
        x = (x - d * d.dot(x)).normalized()
        y = d.cross(x).normalized()
        r = r0 + (r1 - r0) * (i / segs)
        rings.append([bm.verts.new(p + (x * math.cos(2 * math.pi * k / sides) + y * math.sin(2 * math.pi * k / sides)) * r)
                      for k in range(sides)])
    for a, b in zip(rings, rings[1:]):
        for k in range(sides):
            bm.faces.new((a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k]))
    tip = bm.verts.new(pts[-1] + dirs[-1] * max(r1, 0.01) * 2.5)
    for k in range(sides):
        bm.faces.new((rings[-1][k], rings[-1][(k + 1) % sides], tip))
    bm.faces.new(list(reversed(rings[0])))
    obj = mesh_object(name, bm, mat, parent, 0.0)

    if sucker_mat is not None:
        # Small discs along the inner side of the curl.
        for i in range(1, segs - 1, 2):
            p, d = pts[i], dirs[i]
            inner = (axis.cross(d)).normalized()
            r = r0 + (r1 - r0) * (i / segs)
            pos = p + inner * r * 0.95
            size = r * 0.75
            look = inner.to_track_quat("Z", "Y").to_euler()
            shape("cyl", name + "_sucker", parent, (size, size, size * 0.35), pos, sucker_mat,
                  rot=[math.degrees(a) for a in look], segments=6)
    return obj


# ----------------------------------------------------------------------------------------------
# Creature
# ----------------------------------------------------------------------------------------------


def lerp(a, b, t):
    return a + (b - a) * t


def lerp_color(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(lerp(x, y, t) for x, y in zip(a, b))


def skin_color(level):
    stops = [(1, (0.62, 0.30, 0.16)), (4, (0.66, 0.24, 0.14)), (8, (0.36, 0.40, 0.26)),
             (12, (0.14, 0.30, 0.28)), (16, (0.06, 0.13, 0.18))]
    for (l0, c0), (l1, c1) in zip(stops, stops[1:]):
        if level <= l1:
            return lerp_color(c0, c1, (level - l0) / (l1 - l0))
    return stops[-1][1]


def build_tentacle_line(level, seed=7):
    """Tentacle evolution: bodybuilder -> sprouting tendrils -> octopus-headed brute -> winged drowned god."""
    rnd = random.Random(seed + level)
    stage = (level - 1) // 4 + 1
    mus = min(1.0, (level - 1) / 6.0) ** 0.85      # muscle growth: the bodybuilding fantasy peaks by level 7
    god = max(0.0, (level - 12) / 4.0)            # 0..1 through stage 4

    root = empty("Creature")
    body_scale = 1.0 + 0.04 * min(level, 12) + 0.22 * god * 4
    root.scale = (body_scale, body_scale, body_scale)
    if stage == 4:
        root.location.z = 0.15 + 0.12 * (level - 12)

    skin = material("skin_%d" % level, skin_color(level), roughness=0.45)
    dark_skin = material("skin_dark_%d" % level, tuple(c * 0.55 for c in skin_color(level)), roughness=0.5)
    sucker = material("sucker", (0.85, 0.55, 0.6), roughness=0.35)
    trunks = material("trunks", (0.45, 0.04, 0.05), roughness=0.5)
    gold = material("gold", (1.0, 0.72, 0.28), metallic=1.0, roughness=0.25)
    eye_white = material("eye_white", (0.9, 0.88, 0.8), roughness=0.3)
    pupil = material("pupil", (0.02, 0.02, 0.02), roughness=0.2)
    glow = material("glow", (0.3, 1.0, 0.85), emission=(0.25, 1.0, 0.8), strength=8.0)
    vein_glow = material("vein_glow", (0.2, 0.9, 1.0), emission=(0.2, 0.9, 1.0), strength=4.0 + 4 * god)
    hair = material("hair", (0.06, 0.04, 0.03), roughness=0.8)
    membrane = material("membrane", (0.12, 0.08, 0.16), roughness=0.55)
    bone = material("bone", (0.75, 0.7, 0.6), roughness=0.5)

    w = 1.0 + 0.7 * mus                            # torso width factor
    m = 1.0 + 1.1 * mus                            # limb muscle factor

    # --- pelvis, trunks and the champion belt (gold accents like ForgeTax) ---
    shape("box", "Pelvis", root, (0.36 * w, 0.24 + 0.04 * mus, 0.2), (0, 0, 0.95), trunks, bevel=0.03)
    shape("box", "Belt", root, (0.38 * w + 0.02, 0.27 + 0.04 * mus, 0.07), (0, 0, 1.06), gold, bevel=0.015)
    shape("hex", "Buckle", root, (0.13, 0.03, 0.11), (0, -0.15 - 0.02 * mus, 1.06), gold, bevel=0.01)

    # --- abdomen with hexagonal six-pack studs ---
    shape("box", "Waist", root, (0.3 * w, 0.2 + 0.03 * mus, 0.22), (0, 0, 1.18), skin, bevel=0.04)
    for row in range(3):
        for col in (-1, 1):
            s = 0.07 + 0.03 * mus
            shape("hex", "Ab", root, (s, 0.04 + 0.02 * mus, s * 0.85),
                  (col * (0.045 + 0.012 * mus), -0.1 - 0.02 * mus, 1.11 + row * (0.065 + 0.01 * mus)), skin, bevel=0.008)

    # --- chest, lats, traps ---
    chest_z = 1.42 + 0.02 * mus
    shape("box", "Ribcage", root, (0.42 * w, 0.26 + 0.08 * mus, 0.32), (0, 0.01, chest_z - 0.04), skin, bevel=0.05)
    for side in (-1, 1):
        shape("box", "Pec", root, (0.2 + 0.1 * mus, 0.1 + 0.06 * mus, 0.15 + 0.05 * mus),
              (side * (0.1 + 0.05 * mus), -0.12 - 0.04 * mus, chest_z), skin, rot=(0, side * 6, 0), bevel=0.035)
        shape("ell", "Lat", root, (0.14 + 0.16 * mus, 0.22, 0.34 + 0.1 * mus),
              (side * (0.18 + 0.12 * mus), 0.04, 1.28), skin, rot=(0, side * -12, 0), segments=8)
    shape("box", "Traps", root, (0.3 + 0.18 * mus, 0.16, 0.12 + 0.12 * mus), (0, 0.04, 1.58 + 0.03 * mus), skin,
          rot=(0, 0, 0), bevel=0.04)

    # --- arms (pivot at the shoulder, posed outward; god raises them) ---
    shoulder_x = 0.27 + 0.16 * mus
    for side in (-1, 1):
        shape("ell", "Delt", root, (0.19 * m, 0.2 * m, 0.18 * m), (side * shoulder_x, 0, 1.5 + 0.03 * mus), skin, segments=8)
        spread = 12 + 18 * mus + (35 * god if stage == 4 else 0)
        pivot = empty("ArmPivot", root, loc=(side * shoulder_x, 0, 1.48), rot=(0, side * -spread, 0))
        shape("ell", "UpperArm", pivot, (0.12 * m, 0.12 * m, 0.34), (0, 0, -0.18), skin, segments=7)
        shape("ell", "Biceps", pivot, (0.12 * m, 0.13 * m, 0.2 + 0.04 * mus), (0, -0.05 * m, -0.16), skin, segments=7)
        shape("cyl", "Forearm", pivot, (0.11 * m, 0.11 * m, 0.3), (0, 0, -0.47), skin, segments=7, taper=0.75)
        if stage >= 3:
            # Hands become hooked claws.
            for k in (-1, 0, 1):
                shape("cone", "Claw", pivot, (0.045, 0.045, 0.16 + 0.03 * stage), (k * 0.04, -0.02, -0.7),
                      bone, rot=(180 + 15, 0, k * 12), segments=5)
            shape("box", "Hand", pivot, (0.12, 0.1, 0.1), (0, 0, -0.64), dark_skin, bevel=0.02)
        else:
            shape("box", "Fist", pivot, (0.1 * m, 0.1 * m, 0.1), (0, 0, -0.66), skin, bevel=0.025)
        if 6 <= level:
            # Tendril bursting out of the forearm.
            count = 1 if level < 10 else 2
            for k in range(count):
                tentacle("ArmTendril", pivot, (0, 0.04, -0.42 + 0.1 * k), (side * 0.6, 0.2, -0.6),
                         0.35 + 0.05 * level, 0.035 + 0.003 * level, 0.008, side * 2.2, dark_skin, segs=7,
                         sucker_mat=sucker if level >= 9 else None)

    # --- legs (stage 4 grows a skirt of tentacles that finally replaces them) ---
    legs_visible = level < 16
    for side in (-1, 1):
        if legs_visible:
            shape("ell", "Quad", root, (0.19 * m, 0.21 * m, 0.46), (side * (0.11 + 0.04 * mus), -0.01, 0.66), skin, segments=8)
            shape("cyl", "Calf", root, (0.13 * m, 0.14 * m, 0.4), (side * (0.11 + 0.04 * mus), 0.02, 0.27), skin,
                  segments=7, taper=0.7)
            shape("box", "Foot", root, (0.11, 0.24, 0.08), (side * (0.11 + 0.04 * mus), -0.05, 0.04), dark_skin, bevel=0.02)

    if stage == 4:
        skirt = 6 + 2 * (level - 13)
        for k in range(skirt):
            a = 2 * math.pi * k / skirt + 0.3
            base = (math.cos(a) * 0.17 * w, math.sin(a) * 0.14, 0.92)
            out = Vector((math.cos(a), math.sin(a), -2.2)).normalized()
            tentacle("SkirtTentacle", root, base, out, 0.7 + 0.25 * god + rnd.uniform(0, 0.2), 0.07, 0.012,
                     rnd.uniform(1.2, 2.6), dark_skin, segs=10, curl_axis=Vector((-math.sin(a), math.cos(a), 0)),
                     sucker_mat=sucker)

    # --- skin details: sucker scars (stage 2+) and glowing veins (stage 3+) ---
    if level >= 6:
        for _ in range(min(18, (level - 5) * 3)):
            x = rnd.uniform(-0.2, 0.2) * w
            z = rnd.uniform(1.1, 1.55)
            shape("cyl", "Scar", root, (0.04, 0.04, 0.012), (x, -0.16 - 0.06 * mus, z), sucker, rot=(90, 0, 0), segments=6)
    if level >= 10:
        for k in range(4 + level - 10):
            x = rnd.uniform(-0.18, 0.18) * w
            shape("box", "Vein", root, (0.012, 0.01, rnd.uniform(0.12, 0.3)), (x, -0.17 - 0.06 * mus, rnd.uniform(1.15, 1.45)),
                  vein_glow, rot=(0, rnd.uniform(-25, 25), 0))

    # --- back tentacles ---
    back_counts = [0, 0, 0, 0, 2, 2, 4, 4, 4, 6, 6, 8, 8, 10, 10, 12]
    n_back = back_counts[level - 1]
    for k in range(n_back):
        side = -1 if k % 2 == 0 else 1
        tier = k // 2
        base = (side * (0.08 + 0.05 * tier) * w, 0.13, 1.5 - tier * 0.09)
        out = (side * (0.5 + 0.12 * tier), 0.9, 0.5 - 0.15 * tier)
        length = 0.5 + 0.09 * level + rnd.uniform(-0.05, 0.1)
        tentacle("BackTentacle", root, base, out, length, 0.05 + 0.004 * level, 0.01, side * rnd.uniform(1.8, 3.0),
                 dark_skin, segs=11, curl_axis=Vector((0, 0.3, side)), sucker_mat=sucker if level >= 9 else None,
                 wave=1.5)

    # --- head ---
    neck_z = 1.62 + 0.04 * mus
    shape("cyl", "Neck", root, (0.13 + 0.1 * mus, 0.13 + 0.1 * mus, 0.12), (0, 0.02, neck_z), skin, segments=7)
    head_z = 1.77 + 0.05 * mus
    if stage <= 2:
        shape("box", "Head", root, (0.2, 0.23, 0.25), (0, -0.01, head_z), skin, bevel=0.04)
        shape("box", "Jaw", root, (0.21, 0.2, 0.09), (0, -0.03, head_z - 0.1), skin, bevel=0.03)
        shape("box", "Brow", root, (0.21, 0.04, 0.035), (0, -0.12, head_z + 0.04), skin, bevel=0.01)
        shape("box", "Hair", root, (0.21, 0.23, 0.06), (0, 0.0, head_z + 0.13), hair, bevel=0.02)
        for side in (-1, 1):
            shape("box", "Eye", root, (0.045, 0.02, 0.025), (side * 0.05, -0.12, head_z + 0.005),
                  glow if level >= 8 else eye_white)
            shape("box", "Pupil", root, (0.015, 0.025, 0.02), (side * 0.05, -0.125, head_z + 0.005), pupil)
        if level >= 7:
            # Mouth tendrils: the first sign the face is going.
            for k in range(3 if level == 7 else 5):
                x = (k - (2 if level > 7 else 1)) * 0.035
                tentacle("MouthTendril", root, (x, -0.11, head_z - 0.12), (x * 2, -0.25, -1), 0.17 + 0.03 * (k % 2),
                         0.018, 0.004, 1.2, dark_skin, segs=6, curl_axis=Vector((1, 0, 0)))
    else:
        # Octopus mantle head swept back, with glowing eyes and a beard of face tentacles.
        big = 1.0 + 0.25 * (stage == 4) + 0.1 * god
        shape("ell", "Mantle", root, (0.34 * big, 0.5 * big, 0.36 * big), (0, 0.16 * big, head_z + 0.13 * big),
              dark_skin, rot=(-28, 0, 0), segments=9)
        shape("ell", "Face", root, (0.3 * big, 0.26 * big, 0.26 * big), (0, -0.04, head_z), skin, segments=9)
        for ridge in range(3):
            shape("box", "Ridge", root, (0.04, 0.32 * big, 0.04), ((ridge - 1) * 0.09 * big, 0.2 * big, head_z + 0.28 * big),
                  dark_skin, rot=(-30, 0, 0), bevel=0.01)
        eye_pairs = 1 if stage == 3 else 1 + (level - 12)
        for pair in range(eye_pairs):
            for side in (-1, 1):
                ex = side * (0.075 + 0.03 * pair) * big
                ez = head_z + 0.04 + 0.055 * pair
                shape("ell", "Eye", root, (0.065 - 0.01 * pair, 0.04, 0.05 - 0.008 * pair), (ex, -0.16 * big, ez), glow, segments=6)
        face_count = [6, 6, 8, 8, 10, 10, 12, 12][level - 9]
        for k in range(face_count):
            t = (k / (face_count - 1)) * 2 - 1
            base = (t * 0.11 * big, -0.12 * big, head_z - 0.08 * big)
            out = (t * 0.35, -0.35, -1.0)
            length = 0.45 + 0.07 * (level - 8) + (0.12 if abs(t) < 0.5 else 0)
            tentacle("FaceTentacle", root, base, out, length, 0.032 + 0.002 * level, 0.006, rnd.uniform(0.8, 1.8) * (1 if t >= 0 else -1),
                     dark_skin, segs=9, curl_axis=Vector((0.2, 1.0, 0.0)), sucker_mat=sucker if stage == 4 else None)
        if stage == 4:
            # Crown of horns.
            for k in range(5 + (level - 13)):
                a = math.radians(-70 + 140 * k / (4 + (level - 13)))
                shape("cone", "Horn", root, (0.06, 0.06, 0.25 + 0.06 * god * 4),
                      (math.sin(a) * 0.2 * big, 0.12 * big, head_z + 0.3 * big + math.cos(a) * 0.08), bone,
                      rot=(-25, math.degrees(a) * 0.5, 0), segments=5)

    # --- wings and halo (elder god) ---
    if stage == 4:
        span = 0.5 + 0.45 * (level - 12)
        for side in (-1, 1):
            pivot = empty("WingPivot", root, loc=(side * 0.14 * w, 0.2, 1.5), rot=(0, 0, side * -20))
            fingers = []
            for f in range(4):
                ang = math.radians(25 - f * 22)
                tip = Vector((side * span * math.cos(ang), 0.15 + 0.1 * f, span * math.sin(ang) * 0.9 + 0.1))
                fingers.append(tip)
                length = tip.length
                shape("cyl", "WingBone", pivot, (0.03, 0.03, length), tip / 2, bone,
                      rot=[math.degrees(a) for a in tip.normalized().to_track_quat("Z", "Y").to_euler()], segments=5, taper=0.4)
            bm = bmesh.new()
            origin = bm.verts.new((0, 0.1, 0))
            verts = [bm.verts.new(t) for t in fingers]
            for a, b in zip(verts, verts[1:]):
                mid = bm.verts.new(((a.co + b.co) * 0.5) * 0.72 + Vector((0, 0.05, -0.05)))
                bm.faces.new((origin, a, mid))
                bm.faces.new((origin, mid, b))
            mesh_object("WingMembrane", bm, membrane, pivot)
        if level >= 15:
            halo = material("halo", (1.0, 0.75, 0.3), metallic=0.8, roughness=0.3, emission=(1.0, 0.6, 0.2),
                            strength=3.0 + 4.0 * (level - 15))
            shape("cyl", "HaloRing", root, (1.15, 1.15, 0.05), (0, 0.4, head_z + 0.35), halo, rot=(80, 0, 0), segments=14)
            shape("cyl", "HaloHole", root, (1.0, 1.0, 0.07), (0, 0.4, head_z + 0.35), material("void", (0.01, 0.0, 0.02)),
                  rot=(80, 0, 0), segments=14)
            for k in range(8 if level == 16 else 5):
                a = 2 * math.pi * k / (8 if level == 16 else 5)
                shape("hex", "Rune", root, (0.07, 0.02, 0.07),
                      (math.cos(a) * 0.75, 0.25, head_z + 0.1 + math.sin(a) * 0.75), halo, bevel=0.005)
    return root


BUILDERS = {"tentacle": build_tentacle_line}

# ----------------------------------------------------------------------------------------------
# Stage and rendering
# ----------------------------------------------------------------------------------------------


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()


def build_stage(scale=1.0):
    scene = bpy.context.scene
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.012, 0.008, 0.006, 1)
    scene.world = world

    rnd = random.Random(3)
    brick_mats = [material("brick_%d" % i, (0.10 + 0.025 * i, 0.06 + 0.014 * i, 0.04 + 0.008 * i), roughness=0.85)
                  for i in range(3)]
    wall = empty("Wall")
    wall.scale = (scale, scale, scale)
    wall.location = (0, 2.6 * (scale - 1), 0)
    for row in range(16):
        for col in range(-9, 10):
            offset = 0.27 if row % 2 else 0.0
            shape("box", "Brick", wall, (0.5, 0.18, 0.24), (col * 0.54 + offset, 2.6 + rnd.uniform(-0.02, 0.02), 0.13 + row * 0.27),
                  brick_mats[rnd.randrange(3)], bevel=0.03)
    shape("box", "Floor", None, (14 * scale, 10 * scale, 0.1), (0, 0, -0.05), material("floor", (0.05, 0.035, 0.03), roughness=0.7))
    iron = material("iron", (0.08, 0.075, 0.07), metallic=0.7, roughness=0.45)
    gold = material("gold", (1.0, 0.72, 0.28), metallic=1.0, roughness=0.25)
    shape("cyl", "Pedestal", None, (1.6, 1.6, 0.16), (0, 0, 0.08), iron, segments=6, bevel=0.03)
    shape("cyl", "PedestalRim", None, (1.66, 1.66, 0.04), (0, 0, 0.17), gold, segments=6, bevel=0.01)

    def light(name, kind, loc, target, color, energy, size):
        data = bpy.data.lights.new(name, kind)
        data.color = color
        data.energy = energy
        if kind == "AREA":
            data.size = size
        else:
            data.shadow_soft_size = size
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = loc
        direction = Vector(target) - Vector(loc)
        obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        return obj

    k = scale
    light("Key", "AREA", (-2.6 * k, -3.2 * k, 4.2 * k), (0, 0, 1.3 * k), (1.0, 0.7, 0.42), 520 * k * k, 2.5 * k)
    light("Rim", "SPOT", (2.4 * k, 2.0 * k, 3.2 * k), (0, 0, 1.4 * k), (0.45, 0.6, 1.0), 1500 * k * k, 0.4 * k)
    light("Fill", "AREA", (3.0 * k, -3.0 * k, 1.2 * k), (0, 0, 1.2 * k), (0.6, 0.45, 0.4), 60 * k * k, 3.0 * k)
    light("Under", "POINT", (0, -1.2, 0.3), (0, 0, 1), (1.0, 0.5, 0.25), 40, 0.5)

    eevee = scene.eevee
    eevee.use_bloom = True
    eevee.bloom_threshold = 0.9
    eevee.bloom_intensity = 0.06
    eevee.bloom_radius = 5.0
    eevee.use_gtao = True
    eevee.gtao_distance = 0.4
    eevee.use_soft_shadows = True
    eevee.shadow_cube_size = "1024"
    eevee.shadow_cascade_size = "2048"
    eevee.taa_render_samples = 32
    scene.render.engine = "BLENDER_EEVEE"
    try:
        scene.view_settings.view_transform = "AgX"
        scene.view_settings.look = "AgX - Punchy"
    except TypeError:
        scene.view_settings.view_transform = "Filmic"
    scene.view_settings.exposure = 0.0
    add_vignette(scene)


def add_vignette(scene):
    """Compositor vignette: darkens the corners like ForgeTax's moody framing."""
    scene.use_nodes = True
    tree = scene.node_tree
    tree.nodes.clear()
    layers = tree.nodes.new("CompositorNodeRLayers")
    mask = tree.nodes.new("CompositorNodeEllipseMask")
    mask.width = 0.95
    mask.height = 0.85
    blur = tree.nodes.new("CompositorNodeBlur")
    blur.filter_type = "FAST_GAUSS"
    blur.use_relative = True
    blur.factor_x = 30
    blur.factor_y = 30
    mix = tree.nodes.new("CompositorNodeMixRGB")
    mix.blend_type = "MULTIPLY"
    mix.inputs[0].default_value = 0.75
    out = tree.nodes.new("CompositorNodeComposite")
    tree.links.new(mask.outputs[0], blur.inputs[0])
    tree.links.new(layers.outputs["Image"], mix.inputs[1])
    tree.links.new(blur.outputs[0], mix.inputs[2])
    tree.links.new(mix.outputs[0], out.inputs[0])


def creature_bounds(root):
    bpy.context.view_layer.update()
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for obj in [root] + list(root.children_recursive):
        if obj.type != "MESH":
            continue
        for corner in obj.bound_box:
            world = obj.matrix_world @ Vector(corner)
            lo = Vector(map(min, lo, world))
            hi = Vector(map(max, hi, world))
    return lo, hi


def place_camera(height, center_z):
    scene = bpy.context.scene
    cam_data = bpy.data.cameras.new("Camera")
    cam_data.lens = 50
    cam = bpy.data.objects.new("Camera", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    fov = cam_data.angle_y if scene.render.resolution_y >= scene.render.resolution_x else cam_data.angle_x
    distance = (height * 0.62) / math.tan(fov / 2)
    direction = Vector((-0.28, -1.0, 0.12)).normalized()
    target = Vector((0, 0, center_z))
    cam.location = target + direction * distance
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam_data.dof.use_dof = True
    cam_data.dof.focus_distance = distance
    cam_data.dof.aperture_fstop = 2.2
    return cam


def render_level(kind, level, frame_height, path, width, height):
    reset_scene()
    scene = bpy.context.scene
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    build_stage(max(1.0, frame_height / 2.6))
    root = BUILDERS[kind](level)
    lo, hi = creature_bounds(root)
    place_camera(frame_height, (lo.z + hi.z) * 0.5 if hi.z - lo.z > frame_height * 0.8 else frame_height * 0.5)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def measure(kind, level):
    reset_scene()
    root = BUILDERS[kind](level)
    lo, hi = creature_bounds(root)
    return hi.z - min(lo.z, 0.0), (lo, hi)


def parse_levels(text):
    if "-" in text:
        a, b = text.split("-")
        return list(range(int(a), int(b) + 1))
    return [int(x) for x in text.split(",")]


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--kind", default="tentacle", choices=sorted(BUILDERS))
    parser.add_argument("--levels", default="1-16")
    parser.add_argument("--out", required=True)
    parser.add_argument("--width", type=int, default=480)
    parser.add_argument("--height", type=int, default=640)
    args = parser.parse_args(argv)
    os.makedirs(args.out, exist_ok=True)

    levels = parse_levels(args.levels)
    # Frame each level on its own (never tighter than a human), so the pedestal shows the true scale.
    heights = {lvl: measure(args.kind, lvl)[0] for lvl in levels}
    for lvl in levels:
        frame = max(2.3, heights[lvl] * 1.08)
        path = os.path.join(args.out, "%s_L%02d.png" % (args.kind, lvl))
        render_level(args.kind, lvl, frame, path, args.width, args.height)
        print("rendered", path, "height %.2f" % heights[lvl])


if __name__ == "__main__":
    main()
