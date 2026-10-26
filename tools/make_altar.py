"""Builds the end altar's model and emblem: EclipseAllMaps/Assets/EndAltar.obj and EndAltarEmblem.png.

The model is hewn stone blocks (a plinth, a step, a tapered pillar, a capstone) with a round emblem on each
side of the pillar. The OBJ uses the usual axes (Y up, +Z front, counter-clockwise faces); the mod mirrors x on
load, as Unity's importer does. Material "stone" gets Sky Meadow's rock, "emblem" the emblem texture.
The emblem is the game's Eclipse difficulty icon with its number filled in: a dark moon in a blue corona.

Usage: python make_altar.py <texDifficultyEclipse8Icon.png> [<preview.png> <stone texture.png>]
(the icon comes from ror2-base-eclipserun_assets_all_*.bundle; the stone, for the preview only, is
texSMRockSide3 from ror2-base-skymeadow_assets_all_*.bundle)
Needs numpy, scipy and Pillow; matplotlib too for the preview.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw
from scipy.spatial import ConvexHull

ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "EclipseAllMaps", "Assets")

# name, bottom, top, half width at the bottom and at the top, chamfer, jitter (all in metres)
BLOCKS = [
    ("plinth", -0.45, 0.32, 1.40, 1.34, 0.10, 0.035),
    ("step", 0.32, 0.60, 1.04, 0.98, 0.07, 0.03),
    ("pillar", 0.60, 1.92, 0.60, 0.50, 0.06, 0.0),
    ("cap", 1.92, 2.34, 0.86, 0.94, 0.07, 0.03),
]
UV_METRES = 1.5  # one stone texture repeat
EMBLEM_HEIGHT, EMBLEM_RADIUS, EMBLEM_RELIEF = 1.26, 0.40, 0.03

# The icon's moon (fitted to its white rim): fill it to hide the number and the ring around it.
MOON_CENTRE, MOON_FILL = (71.7, 68.8), 33.0
PLAQUE = (24, 21, 30)
FRAME = (78, 70, 92)


def block(bottom, top, w0, w1, chamfer, jitter, rng):
    points = []
    for y, w, sy in ((bottom, w0, -1), (top, w1, 1)):
        for sx in (-1, 1):
            for sz in (-1, 1):
                corner = np.array([sx * w, y, sz * w])
                for offset in ([sx * chamfer, 0, 0], [0, sy * chamfer, 0], [0, 0, sz * chamfer]):
                    points.append(corner - offset + rng.uniform(-jitter, jitter, 3))
    points = np.array(points)
    hull = ConvexHull(points)
    triangles = []
    for simplex, equation in zip(hull.simplices, hull.equations):
        a, b, c = points[simplex]
        if np.dot(np.cross(b - a, c - a), equation[:3]) < 0:
            b, c = c, b
        triangles.append((a, b, c))
    return triangles


def box_uv(p, n, offset):
    axis = np.argmax(np.abs(n))
    u, v = {0: (p[2], p[1]), 1: (p[0], p[2]), 2: (p[0], p[1])}[axis]
    return np.array([u, v]) / UV_METRES + offset


def pillar_face(y):
    _, bottom, top, w0, w1, _, _ = BLOCKS[2]
    return w0 + (w1 - w0) * (y - bottom) / (top - bottom), (w1 - w0) / (top - bottom)


def emblem(yaw, rings, segments):
    """Triangles with uvs of one emblem on the pillar side facing yaw (0 = +Z); rings > 1 only for the preview."""
    half, slope = pillar_face(EMBLEM_HEIGHT)
    normal = np.array([0, -slope, 1.0])
    normal /= np.linalg.norm(normal)
    up = np.array([0, 1.0, 0]) - normal * normal[1]
    up /= np.linalg.norm(up)
    right = np.cross(up, normal)
    centre = np.array([0, EMBLEM_HEIGHT, half])
    rotation = np.array([[np.cos(yaw), 0, np.sin(yaw)], [0, 1, 0], [-np.sin(yaw), 0, np.cos(yaw)]])

    def at(r, angle, depth):
        local = centre + normal * depth + (right * np.cos(angle) + up * np.sin(angle)) * r * EMBLEM_RADIUS
        return rotation @ local, np.array([0.5 + 0.5 * r * np.cos(angle), 0.5 + 0.5 * r * np.sin(angle)])

    triangles = []
    angles = np.linspace(0, 2 * np.pi, segments + 1)
    radii = np.linspace(0, 1, rings + 1)
    for i in range(segments):
        a0, a1 = angles[i], angles[i + 1]
        for j in range(rings):
            r0, r1 = radii[j], radii[j + 1]
            p00, p01, p10, p11 = at(r0, a0, EMBLEM_RELIEF), at(r0, a1, EMBLEM_RELIEF), at(r1, a0, EMBLEM_RELIEF), at(r1, a1, EMBLEM_RELIEF)
            triangles.append((p00, p10, p11))
            if j:
                triangles.append((p00, p11, p01))
        # the rim, down into the pillar, in the plaque's colour
        front0, front1 = at(1, a0, EMBLEM_RELIEF)[0], at(1, a1, EMBLEM_RELIEF)[0]
        back0, back1 = at(1, a0, -0.02)[0], at(1, a1, -0.02)[0]
        corner = np.array([0.02, 0.02])
        triangles += [((front0, corner), (back0, corner), (back1, corner)), ((front0, corner), (back1, corner), (front1, corner))]
    return triangles


def build(emblem_rings):
    rng = np.random.default_rng(7)
    stone, emblems = [], []
    for name, bottom, top, w0, w1, chamfer, jitter in BLOCKS:
        offset = rng.uniform(0, 1, 2)
        for tri in block(bottom, top, w0, w1, chamfer, jitter, rng):
            n = np.cross(tri[1] - tri[0], tri[2] - tri[0])
            n /= np.linalg.norm(n)
            stone.append([(p, box_uv(p, n, offset)) for p in tri])
    for k in range(4):
        emblems += emblem(k * np.pi / 2, emblem_rings, 40)
    return stone, emblems


def write_obj(path, groups):
    """Flat shaded: each triangle has its own corners and normal."""
    lines = ["# The end altar, made by tools/make_altar.py", "o EndAltar"]
    faces = []
    count = 0
    for material, triangles in groups:
        faces.append(f"usemtl {material}")
        for tri in triangles:
            count += 1
            ps = [np.asarray(p) for p, _ in tri]
            n = np.cross(ps[1] - ps[0], ps[2] - ps[0])
            lines.append("vn %.4f %.4f %.4f" % tuple(n / np.linalg.norm(n)))
            for p, uv in tri:
                lines.append("v %.4f %.4f %.4f" % tuple(p))
                lines.append("vt %.4f %.4f" % tuple(uv))
            first = 3 * count - 2
            faces.append("f " + " ".join(f"{first + i}/{first + i}/{count}" for i in range(3)))
    with open(path, "w", newline="\n") as f:
        f.write("\n".join(lines + faces) + "\n")


def make_emblem(icon_path, out_path, size=256):
    icon = np.array(Image.open(icon_path).convert("RGBA")).astype(float)
    yy, xx = np.mgrid[0:icon.shape[0], 0:icon.shape[1]]
    fill = np.clip(MOON_FILL + 0.5 - np.hypot(xx - MOON_CENTRE[0], yy - MOON_CENTRE[1]), 0, 1)[..., None]
    icon = icon * (1 - fill) + np.array([2, 4, 10, 235.0]) * fill
    icon = Image.fromarray(icon.astype(np.uint8))

    plaque = Image.new("RGBA", (size, size), PLAQUE + (255,))
    ImageDraw.Draw(plaque).ellipse([3, 3, size - 4, size - 4], outline=FRAME + (255,), width=7)
    # The icon's farthest opaque pixel stays inside the frame.
    alpha = np.array(icon)[..., 3]
    ys, xs = np.nonzero(alpha > 40)
    reach = np.hypot(xs - icon.width / 2, ys - icon.height / 2).max()
    scale = (size / 2 - 12) / reach
    icon = icon.resize((round(icon.width * scale), round(icon.height * scale)), Image.LANCZOS)
    plaque.alpha_composite(icon, ((size - icon.width) // 2, (size - icon.height) // 2))
    plaque.convert("RGB").save(out_path)


def preview(path, stone_path, emblem_path):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from mpl_toolkits.mplot3d.art3d import Poly3DCollection

    stone_tex = np.array(Image.open(stone_path).convert("RGB")).astype(float) / 255
    emblem_tex = np.array(Image.open(emblem_path).convert("RGB")).astype(float) / 255
    light = np.array([0.4, 0.8, 0.6]) / np.linalg.norm([0.4, 0.8, 0.6])
    stone, emblems = build(emblem_rings=24)
    polys, colours = [], []
    for triangles, tex, glow in ((stone, stone_tex, 0), (emblems, emblem_tex, 0.6)):
        for tri in triangles:
            ps = np.array([p for p, _ in tri])
            uv = np.mean([uv for _, uv in tri], axis=0) % 1
            texel = tex[int((1 - uv[1]) * (tex.shape[0] - 1)), int(uv[0] * (tex.shape[1] - 1))]
            n = np.cross(ps[1] - ps[0], ps[2] - ps[0])
            n /= np.linalg.norm(n)
            shade = 0.35 + 0.65 * max(np.dot(n, light), 0)
            polys.append(ps[:, [0, 2, 1]])  # matplotlib's z is up
            colours.append(np.clip(texel * shade + texel * glow, 0, 1))
    fig = plt.figure(figsize=(12, 6), facecolor=(0.55, 0.6, 0.7))
    for i, (elev, azim) in enumerate(((12, -60), (35, 20))):
        ax = fig.add_subplot(1, 2, i + 1, projection="3d")
        ax.set_facecolor((0.55, 0.6, 0.7))
        ax.add_collection3d(Poly3DCollection(polys, facecolors=colours, edgecolors="none"))
        ax.set_xlim(-1.5, 1.5), ax.set_ylim(-1.5, 1.5), ax.set_zlim(-0.6, 2.4)
        ax.set_box_aspect((3, 3, 3))
        ax.view_init(elev, azim)
        ax.axis("off")
    fig.savefig(path, dpi=110, bbox_inches="tight")


if __name__ == "__main__":
    emblem_png = os.path.join(ASSETS, "EndAltarEmblem.png")
    make_emblem(sys.argv[1], emblem_png)
    stone, emblems = build(emblem_rings=1)
    write_obj(os.path.join(ASSETS, "EndAltar.obj"), [("stone", stone), ("emblem", emblems)])
    print(f"{len(stone)} stone and {len(emblems)} emblem triangles")
    if len(sys.argv) > 3:
        preview(sys.argv[2], sys.argv[3], emblem_png)
