"""Building sprites. All face North (front = top of image); the game rotates directional ones.
Each function returns a list of frames; frame 0 is the idle/static look."""
import math
import random

from palette import *
from pixel import Canvas, lerp

TAU = math.pi * 2


def _bolt(c, x, y):
    c.px(x, y, BRASS[3])
    c.px(x + 1, y + 1, BRASS[1])


def _iron_frame(c, x, y, w, h):
    """Riveted iron border used on machine housings."""
    c.box(x, y, w, h, IRON[0])
    c.box(x + 1, y + 1, w - 2, h - 2, IRON[2])
    for i in range(x + 1, x + w - 1):
        c.px(i, y + 1, IRON[3])
    for j in range(y + 1, y + h - 1):
        c.px(x + 1, j, IRON[3])
    c.box(x + 2, y + 2, w - 4, h - 4, IRON[1])


# ------------------------------------------------------------------ logistics

def conveyor(frame, fast=False):
    """Rubber belt between riveted rails; chevron treads scroll toward the front (up)."""
    c = Canvas(16)
    rail = COPPER if fast else IRON
    c.rect(2, 0, 12, 16, RUBBER[1])
    c.rect(2, 0, 1, 16, RUBBER[0])
    c.rect(13, 0, 1, 16, RUBBER[2])
    for k in range(2):
        y0 = (k * 8 - frame * 2) % 16
        for i in range(5):
            for x, y in ((7 - i, y0 + i), (8 + i, y0 + i)):
                c.px(x, y % 16, RUBBER[3])
                c.px(x, (y + 1) % 16, RUBBER[0])
    for x0, light, dark in ((0, rail[3], rail[1]), (14, rail[2], rail[0])):
        c.rect(x0, 0, 2, 16, rail[2])
        c.rect(x0, 0, 1, 16, light)
        c.rect(x0 + 1, 0, 1, 16, dark)
    for y in (2, 10):
        c.px(0, y, BRASS[3])
        c.px(15, y + 4, BRASS[2])
    return c


def splitter():
    c = conveyor(0)
    # Brass housing with three output pips.
    c.bevel(4, 4, 8, 8, BRASS[2], BRASS[3], BRASS[0])
    c.rect(6, 6, 4, 4, BRASS[1])
    c.px(7, 7, BRASS[4])
    for x, y in ((7, 1), (8, 1), (1, 7), (1, 8), (14, 7), (14, 8)):
        c.px(x, y, FIRE[3])
    return [c]


def underground(entrance):
    """Half belt plus an arched tunnel housing. Entrance: belt enters at the bottom, tunnel ahead.
    Exit: tunnel behind, belt leaves at the top."""
    c = conveyor(0)
    top, bottom = (0, 9) if entrance else (7, 16)
    c.rect(1, top, 14, bottom - top, IRON[1])
    c.box(1, top, 14, bottom - top, IRON[0])
    c.rect(2, top + 1, 12, 1, IRON[3])
    # Brass arch and dark mouth facing the belt side.
    mouth_y = bottom - 3 if entrance else top
    c.rect(3, mouth_y, 10, 3, RUBBER[0])
    for x in range(3, 13):
        c.px(x, mouth_y - 1 if entrance else mouth_y + 3, BRASS[3])
    _bolt(c, 2, top + 2 if entrance else bottom - 3)
    _bolt(c, 12, top + 2 if entrance else bottom - 3)
    return [c]


def inserter_base():
    c = Canvas(16)
    c.bevel(2, 2, 12, 12, IRON[2], IRON[3], IRON[0])
    c.box(1, 1, 14, 14, OUTLINE)
    for x, y in ((3, 3), (11, 3), (3, 11), (11, 11)):
        _bolt(c, x, y)
    c.shaded_disc(8, 8, 3.5, BRASS[4], BRASS[2], BRASS[1])
    return [c]


def inserter_arm():
    """Points up from the centre; the game rotates it between source (down) and target (up)."""
    c = Canvas(16)
    c.rect(7, 2, 2, 6, IRON[3])
    c.rect(8, 2, 1, 6, IRON[1])
    # Claw.
    c.rect(5, 1, 6, 1, IRON[2])
    c.px(5, 0, IRON[3])
    c.px(10, 0, IRON[1])
    c.px(5, 2, IRON[2])
    c.px(10, 2, IRON[1])
    c.disc(8, 8.5, 1.8, BRASS[3])
    c.px(7, 7, BRASS[4])
    c.outline(OUTLINE)
    return [c]


def chest():
    c = Canvas(16)
    c.rect(1, 1, 14, 14, WOOD[2])
    for y in (4, 8, 12):
        c.rect(2, y, 12, 1, WOOD[0])
        c.rect(2, y - 1, 12, 1, WOOD[3])
    c.box(1, 1, 14, 14, IRON[1])
    c.box(0, 0, 16, 16, OUTLINE)
    for x, y in ((1, 1), (13, 1), (1, 13), (13, 13)):
        c.rect(x, y, 2, 2, BRASS[2])
        c.px(x, y, BRASS[4])
    c.rect(7, 6, 2, 3, BRASS[3])
    c.px(7, 6, BRASS[4])
    return [c]


# ------------------------------------------------------------------ production

def drill(frame):
    c = Canvas(32)
    c.rect(1, 1, 30, 30, IRON[1])
    _iron_frame(c, 0, 0, 32, 32)
    for x, y in ((3, 3), (27, 3), (3, 27), (27, 27)):
        _bolt(c, x, y)
    # Output chute at the front.
    c.bevel(12, 0, 8, 6, BRASS[2], BRASS[3], BRASS[0])
    c.rect(14, 0, 4, 3, RUBBER[0])
    # Drill pit and spinning spiral bit.
    c.disc(16, 18, 10, SOOT[0])
    c.ring(16, 18, 10, IRON[3], 1.0)
    a0 = frame * TAU / 16
    for arm in range(4):
        for r10 in range(10, 90, 6):
            r = r10 / 10
            a = a0 + arm * TAU / 4 + r * 0.35
            x, y = 16 + math.cos(a) * r, 18 + math.sin(a) * r
            c.px(x, y, IRON[4] if r < 4 else IRON[3] if r < 7 else IRON[2])
    c.shaded_disc(16, 18, 2.5, BRASS[4], BRASS[3], BRASS[1])
    # Little drive gear in the corner, turning with the bit.
    c.gear(6, 25, 4, 2.6, 6, BRASS[2], BRASS[0], BRASS[4], angle=-a0 * 2, hole=0.9)
    return c


def furnace(frame):
    """Frame 0 unlit; frames 1-3 burning with flicker."""
    rng = random.Random(frame)
    c = Canvas(32)
    # Brick body.
    for y in range(32):
        for x in range(32):
            row = y // 4
            off = 0 if row % 2 == 0 else 4
            mortar = y % 4 == 3 or (x + off) % 8 == 7
            c.px(x, y, STONE[1] if mortar else STONE[3] if (x + off) % 8 < 2 and y % 4 == 0 else STONE[2])
    c.box(0, 0, 32, 32, OUTLINE)
    c.box(1, 1, 30, 30, STONE[0])
    # Firebox.
    c.disc(16, 16, 8.5, SOOT[0])
    c.ring(16, 16, 8.5, STONE[4], 1.0)
    c.ring(16, 16, 9.5, STONE[0], 1.0)
    lit = frame > 0
    for y in range(8, 25):
        for x in range(8, 25):
            d = math.hypot(x + 0.5 - 16, y + 0.5 - 16)
            if d > 7:
                continue
            if lit:
                heat = 1 - d / 7 + rng.uniform(-0.25, 0.25)
                col = FIRE[4] if heat > 0.8 else FIRE[3] if heat > 0.55 else FIRE[2] if heat > 0.3 else FIRE[1] if heat > 0.1 else FIRE[0]
            else:
                col = SOOT[1] if rng.random() < 0.6 else SOOT[2] if rng.random() < 0.5 else rgb('#4a2a20')
            c.px(x, y, col)
    # Brass door frame at the bottom edge and chimney in the corner.
    c.bevel(10, 26, 12, 4, BRASS[2], BRASS[3], BRASS[0])
    c.rect(12, 27, 8, 2, FIRE[1] if lit else SOOT[1])
    c.bevel(24, 2, 6, 6, STONE[2], STONE[3], STONE[0])
    c.rect(25, 3, 4, 4, SOOT[0])
    if lit:
        c.px(26, 4, SOOT[2])
    return c


def assembler(frame):
    c = Canvas(48)
    # Wooden deck with iron edging.
    for y in range(48):
        for x in range(48):
            plank = (y // 6) % 2
            c.px(x, y, WOOD[2] if plank else WOOD[1] if y % 6 == 0 else WOOD[3] if y % 6 == 1 else WOOD[2])
    _iron_frame(c, 0, 0, 48, 48)
    for x, y in ((3, 3), (43, 3), (3, 43), (43, 43)):
        _bolt(c, x, y)
    # Big brass drive gear meshing with a small one; they turn with the frame.
    a = frame * TAU / 32
    c.gear(13, 13, 9, 7, 12, BRASS[2], BRASS[0], BRASS[4], angle=a, hole=2.0)
    c.ring(13, 13, 4, BRASS[1], 1.0)
    c.gear(24, 9, 5, 3.5, 8, IRON[3], IRON[1], IRON[4], angle=-a * 1.6, hole=1.0)
    # Work plate.
    c.bevel(20, 26, 20, 14, IRON[2], IRON[3], IRON[0])
    for gx in range(23, 38, 4):
        c.line(gx, 27, gx, 38, IRON[1])
    # Mechanical arm: pivot bottom-left, reaching over the plate; elbow swings with the frame.
    swing = math.sin(frame * TAU / 4) * 0.5
    px0, py0 = 12, 36
    ex, ey = px0 + math.cos(-0.6 + swing) * 12, py0 + math.sin(-0.6 + swing) * 12
    hx, hy = ex + math.cos(0.4 - swing) * 10, ey + math.sin(0.4 - swing) * 10
    for (x0, y0, x1, y1) in ((px0, py0, ex, ey), (ex, ey, hx, hy)):
        c.line(x0, y0, x1, y1, IRON[3])
        c.line(x0 + 1, y0 + 1, x1 + 1, y1 + 1, IRON[1])
    c.shaded_disc(px0, py0, 3, BRASS[4], BRASS[2], BRASS[0])
    c.disc(ex, ey, 1.6, BRASS[3])
    c.rect(int(hx) - 1, int(hy) - 1, 3, 3, IRON[4])
    return c


def lab(frame):
    c = Canvas(32)
    c.rect(1, 1, 30, 30, WOOD[1])
    c.box(0, 0, 32, 32, OUTLINE)
    # Brass octagonal frame around a glass dome.
    for y in range(32):
        for x in range(32):
            dx, dy = abs(x + 0.5 - 16), abs(y + 0.5 - 16)
            if max(dx, dy) < 14 and dx + dy < 19:
                c.px(x, y, BRASS[2] if (dx + dy > 16 or max(dx, dy) > 12) else c.get(x, y)[:3])
    c.disc(16, 16, 10, GLASS[0])
    c.disc(16, 17, 8, rgb('#3a7ab0'))
    c.disc(16, 19, 6, rgb('#2a5a90'))
    rng = random.Random(7)
    for i in range(5):
        bx = 11 + rng.randint(0, 10)
        by = 24 - ((frame * 3 + i * 4) % 14)
        c.px(bx, by, GLASS[2])
    c.px(11, 9, GLASS[2])
    c.px(10, 10, GLASS[2])
    c.ring(16, 16, 10, IRON[3], 1.0)
    # Sample flasks in the corners.
    for (fx, fy, liq) in ((4, 4, RED_LIQ), (25, 4, GREEN_LIQ), (4, 25, GREEN_LIQ), (25, 25, RED_LIQ)):
        c.disc(fx + 1.5, fy + 1.5, 2, liq[1])
        c.px(fx + 1, fy + 1, liq[2])
    return c


# ------------------------------------------------------------------ rotational power

def shaft(frame):
    """Vertical iron rod; a highlight sweeps across it to show spin. Brass collar in the middle."""
    c = Canvas(16)
    c.rect(5, 0, 6, 16, IRON[1])
    c.rect(5, 0, 1, 16, IRON[0])
    c.rect(10, 0, 1, 16, IRON[0])
    hl = 6 + frame % 4
    c.rect(hl, 0, 1, 16, IRON[4])
    c.rect(6 + (frame + 2) % 4, 0, 1, 16, IRON[2])
    c.bevel(4, 6, 8, 4, BRASS[2], BRASS[3], BRASS[0])
    c.px(4 + 2 * (frame % 4), 7, BRASS[4])
    return c


def gearbox(frame):
    c = Canvas(16)
    c.bevel(1, 1, 14, 14, WOOD[2], WOOD[3], WOOD[0])
    c.box(0, 0, 16, 16, OUTLINE)
    for x, y in ((1, 1), (13, 1), (1, 13), (13, 13)):
        c.rect(x, y, 2, 2, IRON[2])
    # Axle stubs on all four sides.
    for (x, y, w, h) in ((6, 0, 4, 2), (6, 14, 4, 2), (0, 6, 2, 4), (14, 6, 2, 4)):
        c.rect(x, y, w, h, IRON[3])
    c.gear(8, 8, 6, 4.2, 8, BRASS[2], BRASS[0], BRASS[3], angle=frame * TAU / 32, hole=1.3)
    c.ring(8, 8, 2.6, BRASS[1], 1.0)
    return c


def hand_crank(frame):
    c = Canvas(16)
    c.bevel(1, 1, 14, 14, STONE[2], STONE[3], STONE[0])
    c.box(0, 0, 16, 16, OUTLINE)
    c.gear(8, 8, 6, 4.3, 8, BRASS[2], BRASS[0], BRASS[3], angle=frame * TAU / 32, hole=0)
    c.ring(8, 8, 2.6, BRASS[1], 1.0)
    a = frame * TAU / 4 - math.pi / 2
    hx, hy = 8 + math.cos(a) * 4.5, 8 + math.sin(a) * 4.5
    c.line(8, 8, hx, hy, IRON[3])
    c.disc(hx, hy, 1.6, WOOD[3])
    c.px(hx - 0.5, hy - 0.5, WOOD[4])
    c.disc(8, 8, 1.2, IRON[4])
    return c


def water_wheel(frame):
    c = Canvas(16)
    c.disc(8, 8, 7.5, WOOD[1])
    c.disc(8, 8, 5, rgb('#2e5a7a'))
    a0 = frame * TAU / 32
    for k in range(8):
        a = a0 + k * TAU / 8
        x0, y0 = 8 + math.cos(a) * 2, 8 + math.sin(a) * 2
        x1, y1 = 8 + math.cos(a) * 7.5, 8 + math.sin(a) * 7.5
        c.line(x0, y0, x1, y1, WOOD[3] if k % 2 else WOOD[4])
    c.ring(8, 8, 7.5, WOOD[0], 1.0)
    c.shaded_disc(8, 8, 2, BRASS[4], BRASS[2], BRASS[0])
    # Splash on the waterline.
    c.px(2 + frame % 3, 14, WATER[5])
    c.px(12 - frame % 3, 15, WATER[4])
    return c


def steam_engine(frame):
    c = Canvas(32)
    c.rect(1, 1, 30, 30, SOOT[1])
    c.box(0, 0, 32, 32, OUTLINE)
    # Copper boiler with brass bands across the top half.
    for y in range(3, 15):
        t = (y - 3) / 11
        col = COPPER[3] if t < 0.2 else COPPER[2] if t < 0.6 else COPPER[1]
        c.rect(3, y, 22, 1, col)
    c.box(2, 2, 24, 14, COPPER[0])
    for bx in (8, 16):
        c.rect(bx, 3, 2, 12, BRASS[3])
        c.px(bx, 3, BRASS[4])
    # Chimney with a puff of smoke drifting up.
    c.bevel(26, 2, 4, 9, IRON[1], IRON[2], IRON[0])
    c.rect(27, 2, 2, 2, SOOT[0])
    puff_y = 1 - (frame % 4) // 2
    c.px(27 + frame % 2, max(0, puff_y), SOOT[2])
    # Flywheel with spokes, and a piston rod linking it to the cylinder.
    a = frame * TAU / 4
    c.disc(10, 23, 7, IRON[1])
    c.ring(10, 23, 7, IRON[3], 1.5)
    for k in range(4):
        aa = a + k * TAU / 4
        c.line(10, 23, 10 + math.cos(aa) * 5.5, 23 + math.sin(aa) * 5.5, IRON[2])
    crank_x, crank_y = 10 + math.cos(a) * 3.5, 23 + math.sin(a) * 3.5
    c.disc(10, 23, 1.5, BRASS[3])
    c.bevel(20, 19, 9, 5, IRON[2], IRON[3], IRON[0])
    c.line(crank_x, crank_y, 20, 21, BRASS[3])
    c.disc(crank_x, crank_y, 1, BRASS[4])
    # Firebox glow.
    c.rect(21, 26, 7, 3, FIRE[2] if frame % 2 else FIRE[3])
    c.box(20, 25, 9, 5, IRON[0])
    return c


# ------------------------------------------------------------------ status badges

def status_badge(kind):
    """Small badge drawn over machines: 'nopower' (grey cog) or 'overstress' (red cog with !)."""
    c = Canvas(16)
    if kind == 'overstress':
        c.gear(8, 8, 6, 4.5, 8, RED_LIQ[1], RED_LIQ[0], RED_LIQ[2], hole=0)
        c.rect(7, 4, 2, 5, rgb('#fff0e0'))
        c.rect(7, 10, 2, 2, rgb('#fff0e0'))
    else:
        c.gear(8, 8, 6, 4.5, 8, IRON[2], IRON[0], IRON[3], hole=0)
        c.line(3, 13, 13, 3, RED_LIQ[1])
        c.line(3, 12, 12, 3, RED_LIQ[2])
    c.outline(OUTLINE)
    return [c]


def frames(fn, n):
    return [fn(i) for i in range(n)]


BUILDINGS = {
    # name: (frames, fps)
    'conveyor': (lambda: frames(lambda f: conveyor(f), 4), 8),
    'fast_conveyor': (lambda: frames(lambda f: conveyor(f, fast=True), 4), 16),
    'splitter': (splitter, 0),
    'underground_in': (lambda: underground(True), 0),
    'underground_out': (lambda: underground(False), 0),
    'inserter': (inserter_base, 0),
    'inserter_arm': (inserter_arm, 0),
    'chest': (chest, 0),
    'drill': (lambda: frames(drill, 4), 12),
    'furnace': (lambda: frames(furnace, 4), 8),
    'assembler': (lambda: frames(assembler, 4), 8),
    'lab': (lambda: frames(lab, 4), 6),
    'shaft': (lambda: frames(shaft, 4), 12),
    'gearbox': (lambda: frames(gearbox, 4), 12),
    'hand_crank': (lambda: frames(hand_crank, 4), 8),
    'water_wheel': (lambda: frames(water_wheel, 4), 8),
    'steam_engine': (lambda: frames(steam_engine, 4), 10),
    'status_nopower': (lambda: status_badge('nopower'), 0),
    'status_overstress': (lambda: status_badge('overstress'), 0),
}
