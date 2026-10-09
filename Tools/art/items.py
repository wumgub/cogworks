"""Item icons, 16x16 with a dark outline so they read on belts and in slots."""
import math
import random

from palette import *
from pixel import Canvas


def _chunks(c, ramp, accent, seed, count=3):
    rng = random.Random(seed)
    spots = [(5.5, 6.5, 3.4), (10.5, 5.5, 2.8), (9.0, 10.5, 3.6)][:count]
    for cx, cy, r in spots:
        for y in range(16):
            for x in range(16):
                dx, dy = x + 0.5 - cx, y + 0.5 - cy
                # Faceted: distance warped by angle so chunks look like broken rock, not balls.
                a = math.atan2(dy, dx)
                rr = r * (0.85 + 0.15 * math.cos(3 * a + cx))
                if dx * dx + dy * dy > rr * rr:
                    continue
                t = (dx * 0.8 + dy) / (2 * r)
                col = ramp[3] if t < -0.35 else ramp[2] if t < 0.1 else ramp[1] if t < 0.4 else ramp[0]
                if accent and rng.random() < 0.18:
                    col = accent[rng.randrange(len(accent))]
                c.px(x, y, col)
    c.outline(OUTLINE)


def iron_ore():
    c = Canvas(16)
    _chunks(c, [ROCK[1], ROCK[3], IRON[3], IRON[4]], [rgb('#c8d8ec'), IRON[2]], 1)
    return c


def copper_ore():
    c = Canvas(16)
    _chunks(c, [COPPER[0], COPPER[1], COPPER[2], COPPER[3]], [PATINA[1], PATINA[2]], 2)
    return c


def coal():
    c = Canvas(16)
    _chunks(c, [SOOT[0], SOOT[1], SOOT[2], rgb('#6e6676')], [rgb('#a8a2b8')], 3)
    return c


def stone():
    c = Canvas(16)
    _chunks(c, [STONE[1], STONE[2], STONE[3], STONE[4]], None, 4, count=2)
    return c


def _plate(c, ramp):
    """Ingot seen from slightly above: narrow lit top face over a wider, darker front face."""
    for y in range(5, 9):
        inset = (8 - y) // 2  # top face tapers toward the back
        c.rect(4 + inset, y, 8 - 2 * inset, 1, ramp[3])
    c.rect(5, 5, 6, 1, ramp[4])
    c.rect(3, 9, 10, 3, ramp[2])
    c.rect(3, 11, 10, 1, ramp[1])
    c.px(12, 9, ramp[1])
    c.px(12, 10, ramp[1])
    c.px(4, 9, ramp[3])
    # Stamped mark.
    c.px(7, 10, ramp[1])
    c.px(8, 10, ramp[1])
    c.outline(OUTLINE)


def iron_plate():
    c = Canvas(16)
    _plate(c, IRON)
    return c


def copper_plate():
    c = Canvas(16)
    _plate(c, COPPER)
    return c


def stone_brick():
    c = Canvas(16)
    for (x, y) in ((2, 8), (8, 8), (5, 3)):
        c.bevel(x, y, 6, 4, BRICK[2], BRICK[3], BRICK[0])
        c.px(x + 1, y + 1, BRICK[3])
    c.outline(OUTLINE)
    return c


def iron_gear():
    c = Canvas(16)
    c.gear(8, 8, 7, 5, 8, IRON[3], IRON[1], IRON[4], angle=0.2, hole=1.6)
    c.ring(8, 8, 3.2, IRON[2], 1.0)
    c.outline(OUTLINE)
    return c


def copper_wire():
    c = Canvas(16)
    # Wooden spool wound with copper.
    c.bevel(2, 3, 2, 10, WOOD[3], WOOD[4], WOOD[1])
    c.bevel(12, 3, 2, 10, WOOD[3], WOOD[4], WOOD[1])
    for y in range(5, 11):
        for x in range(4, 12):
            c.px(x, y, COPPER[3] if (x + y) % 3 == 0 else COPPER[2] if (x + y) % 3 == 1 else COPPER[1])
    for x in range(4, 12):
        c.px(x, 5, COPPER[4])
    c.outline(OUTLINE)
    return c


def circuit():
    c = Canvas(16)
    c.bevel(2, 3, 12, 10, GREEN_PCB[1], GREEN_PCB[2], GREEN_PCB[0])
    for x in (4, 6, 9, 11):
        c.line(x, 4, x, 6, BRASS[3])
        c.line(x, 10, x, 11, BRASS[3])
    c.bevel(5, 6, 6, 4, SOOT[1], SOOT[2], SOOT[0])
    c.px(6, 7, IRON[3])
    c.outline(OUTLINE)
    return c


def _flask(c, liquid):
    # Cork, neck, round bulb with liquid and a glint.
    c.rect(7, 1, 2, 2, WOOD[3])
    c.rect(7, 3, 2, 3, GLASS[2])
    c.disc(8, 10, 5, GLASS[1])
    c.disc(8, 10.5, 4, liquid[1])
    for x in range(4, 13):
        if c.get(x, 8)[:3] == liquid[1]:
            c.px(x, 8, liquid[2])
    c.disc(8, 12, 2.5, liquid[0])
    c.px(6, 8, GLASS[2])
    c.px(5, 9, GLASS[2])
    c.outline(OUTLINE)


def science_red():
    c = Canvas(16)
    _flask(c, RED_LIQ)
    return c


def science_green():
    c = Canvas(16)
    _flask(c, GREEN_LIQ)
    return c


ITEMS = {
    'iron_ore': iron_ore,
    'copper_ore': copper_ore,
    'coal': coal,
    'stone': stone,
    'iron_plate': iron_plate,
    'copper_plate': copper_plate,
    'stone_brick': stone_brick,
    'iron_gear': iron_gear,
    'copper_wire': copper_wire,
    'circuit': circuit,
    'science_red': science_red,
    'science_green': science_green,
}
