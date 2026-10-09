"""Terrain tiles (4 variants each, seamless within a tile) and ore overlays (3 variants each)."""
import math
import random

from palette import *
from pixel import Canvas, periodic_noise

SIZE = 16


def _phases(rng, n=3, max_k=2):
    return [(rng.randint(-max_k, max_k) or 1, rng.randint(-max_k, max_k) or 1, rng.uniform(0, 6.28)) for _ in range(n)]


def _shade_field(c, ramp, rng, centre=2, spread=1.1, grain=0.45):
    """Smooth periodic noise picks a tone around ramp[centre]; per-pixel grain adds texture."""
    phases = _phases(rng)
    for y in range(SIZE):
        for x in range(SIZE):
            v = periodic_noise(x, y, SIZE, phases) * spread + rng.uniform(-grain, grain)
            idx = max(0, min(len(ramp) - 1, centre + int(round(v))))
            c.px(x, y, ramp[idx])


def grass(variant):
    rng = random.Random(100 + variant)
    c = Canvas(SIZE)
    _shade_field(c, GRASS, rng, centre=2, spread=0.9, grain=0.6)
    # Tufts: small upward strokes, lit on top.
    for _ in range(3 + variant % 2):
        x, y = rng.randint(1, 14), rng.randint(3, 14)
        c.px(x, y, GRASS[4])
        c.px(x, y + 1, GRASS[3])
        c.px(x - 1, y + 1, GRASS[3])
        c.px(x + 1, y + 1, GRASS[1])
    if variant == 3:
        fx, fy = rng.randint(3, 12), rng.randint(3, 12)
        col = FLOWERS[rng.randrange(len(FLOWERS))]
        for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
            c.px(fx + dx, fy + dy, col)
        c.px(fx, fy, BRASS[4])
    return c


def dirt(variant):
    rng = random.Random(200 + variant)
    c = Canvas(SIZE)
    _shade_field(c, DIRT, rng, centre=2, spread=1.0, grain=0.5)
    for _ in range(2 + variant):
        x, y = rng.randint(1, 14), rng.randint(1, 14)
        c.px(x, y, STONE[3])
        c.px(x + 1, y, STONE[2])
        c.px(x, y + 1, DIRT[0])
    return c


def sand(variant):
    rng = random.Random(300 + variant)
    c = Canvas(SIZE)
    _shade_field(c, SAND, rng, centre=2, spread=0.7, grain=0.5)
    # Wind ripples: short light arcs.
    for _ in range(2):
        x, y = rng.randint(0, 12), rng.randint(2, 13)
        for i in range(4):
            c.px((x + i) % SIZE, y - (1 if i in (1, 2) else 0), SAND[4])
            c.px((x + i) % SIZE, y + 1 - (1 if i in (1, 2) else 0), SAND[1])
    return c


def rock(variant):
    rng = random.Random(400 + variant)
    c = Canvas(SIZE)
    _shade_field(c, ROCK, rng, centre=2, spread=1.2, grain=0.4)
    # A wandering, forking crack with a lit lip.
    def crack(x, y, steps):
        for _ in range(steps):
            c.px(x, y, ROCK[1])
            c.px(x, y - 1, ROCK[3])
            x += rng.choice((-1, 1, 1))
            y += rng.choice((0, 0, 1))
            if rng.random() < 0.15:
                crack(x, y, steps // 2)
    crack(rng.randint(2, 7), rng.randint(3, 9), rng.randint(5, 8))
    return c


def water(frame, variant=0):
    """Animated: highlight bands drift diagonally, looping every 4 frames."""
    rng = random.Random(500 + variant)
    c = Canvas(SIZE)
    phases = _phases(rng, 4, 2)
    for y in range(SIZE):
        for x in range(SIZE):
            v = periodic_noise(x, y, SIZE, phases) + rng.uniform(-0.15, 0.15)
            c.px(x, y, WATER[1] if v < -0.55 else WATER[3] if v > 0.55 else WATER[2])
    shift = frame * 4
    for i, (bx, by) in enumerate([(2, 3), (9, 7), (4, 12)]):
        x = (bx + shift + i * 3) % SIZE
        y = (by + frame) % SIZE
        for k in range(3):
            c.px((x + k) % SIZE, y, WATER[4])
        c.px((x + 1) % SIZE, (y - 1) % SIZE, WATER[5])
    return c


# ------------------------------------------------------------------ ore overlays (transparent)

ORE_STYLES = {
    # (rock ramp dark->light, accent ramp, accent density)
    'iron': ([ROCK[1], ROCK[3], IRON[3], IRON[4]], [IRON[2], IRON[4], rgb('#c8d8ec')], 0.35),
    'copper': ([COPPER[0], COPPER[1], COPPER[2], COPPER[3]], [PATINA[1], PATINA[2], COPPER[4]], 0.3),
    'coal': ([SOOT[0], SOOT[1], SOOT[2], rgb('#6a6070')], [rgb('#141012'), rgb('#8a8496'), rgb('#b0aac0')], 0.25),
    'stone': ([STONE[1], STONE[2], STONE[3], STONE[4]], [STONE[2], STONE[4], rgb('#ece4d4')], 0.15),
}


def ore(kind, variant):
    # Stable seed (str hash() is randomized per Python run).
    rng = random.Random(sum(map(ord, kind)) * 31 + variant)
    ramp, accent, density = ORE_STYLES[kind]
    c = Canvas(SIZE)
    # Faint stain under the lumps so a patch reads as one region rather than scattered rubble.
    for _ in range(14):
        x, y = rng.randint(0, 15), rng.randint(0, 15)
        c.px(x, y, ramp[0], 70)
    # Random, non-overlapping lump positions so neighbouring tiles don't line up into a grid.
    placed = []
    for _ in range(2 + variant % 2):
        for _attempt in range(20):
            r = rng.uniform(1.7, 2.9)
            cx, cy = rng.uniform(r + 1, 15 - r), rng.uniform(r + 1, 15 - r)
            if all((cx - px) ** 2 + (cy - py) ** 2 > (r + pr + 1) ** 2 for px, py, pr in placed):
                placed.append((cx, cy, r))
                break
    for cx, cy, r in placed:
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                dx, dy = x + 0.5 - cx, (y + 0.5 - cy) * 1.15
                if dx * dx + dy * dy > r * r:
                    continue
                t = (dx + dy) / (2 * r)
                col = ramp[3] if t < -0.4 else ramp[2] if t < 0.05 else ramp[1] if t < 0.45 else ramp[0]
                if rng.random() < density:
                    col = accent[rng.randrange(len(accent))]
                c.px(x, y, col)
    # Outline only the lumps (not the stain).
    solid = Canvas(SIZE)
    solid.a = c.a.copy()
    solid.a[solid.a[:, :, 3] < 255] = 0
    solid.outline(OUTLINE)
    c.a[(solid.a[:, :, 3] == 255) & (c.a[:, :, 3] < 255)] = solid.a[(solid.a[:, :, 3] == 255) & (c.a[:, :, 3] < 255)]
    return c
