"""Tiny pixel-art toolkit: an RGBA canvas with drawing primitives tuned for 16px sprites."""
import math
import os

import numpy as np
from PIL import Image


def rgb(h):
    """'#rrggbb' -> (r, g, b)."""
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


class Canvas:
    def __init__(self, w, h=None):
        self.w = w
        self.h = h if h is not None else w
        self.a = np.zeros((self.h, self.w, 4), np.uint8)

    # ------------------------------------------------------------- pixels

    def px(self, x, y, c, alpha=255):
        x, y = int(x), int(y)
        if 0 <= x < self.w and 0 <= y < self.h:
            if alpha >= 255:
                self.a[y, x] = (*c[:3], 255)
            else:
                self.blend(x, y, c, alpha)

    def blend(self, x, y, c, alpha):
        if not (0 <= x < self.w and 0 <= y < self.h):
            return
        dst = self.a[y, x].astype(float)
        a = alpha / 255.0
        if dst[3] == 0:
            self.a[y, x] = (*c[:3], alpha)
            return
        out = dst[:3] * (1 - a) + np.array(c[:3], float) * a
        self.a[y, x] = (*out.astype(np.uint8), max(int(dst[3]), alpha))

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return tuple(int(v) for v in self.a[y, x])
        return (0, 0, 0, 0)

    def opaque(self, x, y):
        return self.get(x, y)[3] > 0

    # ------------------------------------------------------------- shapes

    def rect(self, x, y, w, h, c):
        for j in range(y, y + h):
            for i in range(x, x + w):
                self.px(i, j, c)

    def box(self, x, y, w, h, c):
        """Rectangle outline."""
        for i in range(x, x + w):
            self.px(i, y, c)
            self.px(i, y + h - 1, c)
        for j in range(y, y + h):
            self.px(x, j, c)
            self.px(x + w - 1, j, c)

    def bevel(self, x, y, w, h, base, light, dark, outline=None):
        """Filled block lit from the top-left."""
        self.rect(x, y, w, h, base)
        for i in range(x, x + w):
            self.px(i, y, light)
            self.px(i, y + h - 1, dark)
        for j in range(y, y + h):
            self.px(x, j, light)
            self.px(x + w - 1, j, dark)
        if outline:
            self.box(x - 1, y - 1, w + 2, h + 2, outline)

    def line(self, x0, y0, x1, y1, c):
        x0, y0, x1, y1 = int(round(x0)), int(round(y0)), int(round(x1)), int(round(y1))
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err = dx + dy
        while True:
            self.px(x0, y0, c)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def disc(self, cx, cy, r, c):
        """Filled circle; cx/cy may be fractional (use .5 for pixel-centred even sizes)."""
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= r * r:
                    self.px(x, y, c)

    def ring(self, cx, cy, r, c, width=1.0):
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                d = math.sqrt((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2)
                if r - width <= d <= r:
                    self.px(x, y, c)

    def shaded_disc(self, cx, cy, r, light, base, dark):
        """Sphere-ish disc: light toward top-left, dark toward bottom-right."""
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                dx, dy = x + 0.5 - cx, y + 0.5 - cy
                if dx * dx + dy * dy > r * r:
                    continue
                t = (dx + dy) / (2 * r)  # -0.7 .. 0.7
                self.px(x, y, light if t < -0.25 else dark if t > 0.3 else base)

    def gear(self, cx, cy, r_outer, r_inner, teeth, c, dark, light=None, angle=0.0, hole=1.5):
        """Cog: solid body to r_inner, square teeth out to r_outer, lit from the top-left, rotated by angle."""
        light = light or c
        for y in range(int(cy - r_outer - 1), int(cy + r_outer + 2)):
            for x in range(int(cx - r_outer - 1), int(cx + r_outer + 2)):
                dx, dy = x + 0.5 - cx, y + 0.5 - cy
                d = math.sqrt(dx * dx + dy * dy)
                if d > r_outer or (hole and d <= hole):
                    continue
                a = (math.atan2(dy, dx) - angle) % (2 * math.pi)
                on_tooth = (a / (2 * math.pi) * teeth) % 1.0 < 0.5
                if d > r_inner and not on_tooth:
                    continue
                lit = (dx + dy) / max(d, 0.01)  # -1.4 (top-left) .. 1.4 (bottom-right)
                if d > r_inner - 1 and lit > 0.2:
                    col = dark
                elif lit < -0.6 and d > hole + 0.5:
                    col = light
                else:
                    col = c
                self.px(x, y, col)

    def outline(self, c, diagonal=False):
        """Add a 1px outline around every opaque pixel (on transparent neighbours only)."""
        src = self.a.copy()
        h, w = self.h, self.w
        dirs = [(1, 0), (-1, 0), (0, 1), (0, -1)]
        if diagonal:
            dirs += [(1, 1), (-1, -1), (1, -1), (-1, 1)]
        for y in range(h):
            for x in range(w):
                if src[y, x, 3] > 0:
                    continue
                for dx, dy in dirs:
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h and src[ny, nx, 3] > 0:
                        self.a[y, x] = (*c, 255)
                        break

    def paste(self, other, ox, oy):
        for y in range(other.h):
            for x in range(other.w):
                r, g, b, a = (int(v) for v in other.a[y, x])
                if a:
                    self.px(ox + x, oy + y, (r, g, b), a)

    def rotated(self, quarter_turns):
        out = Canvas(self.w, self.h)
        out.a = np.rot90(self.a, -quarter_turns).copy()
        out.w, out.h = out.a.shape[1], out.a.shape[0]
        return out

    def mirrored(self):
        out = Canvas(self.w, self.h)
        out.a = self.a[:, ::-1].copy()
        return out

    def copy(self):
        out = Canvas(self.w, self.h)
        out.a = self.a.copy()
        return out

    def save(self, path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        Image.fromarray(self.a, 'RGBA').save(path)


def periodic_noise(x, y, size, phases):
    """Seamless (period = size) smooth noise in about [-1, 1] from a list of (kx, ky, phase) waves."""
    v = 0.0
    for kx, ky, ph in phases:
        v += math.sin(2 * math.pi * (kx * x + ky * y) / size + ph)
    return v / max(1, len(phases))


def lerp(c0, c1, t):
    return tuple(int(round(a + (b - a) * t)) for a, b in zip(c0, c1))
