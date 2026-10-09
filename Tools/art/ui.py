"""UI skin: 9-slice frames, buttons, slots, gauges and the logo, in the same warm industrial palette.

Every stretchable piece keeps its detail in the corners (which 9-slicing never stretches); edges are
uniform bands so they stretch cleanly. Slice sizes are listed in SLICES for the USS.
"""
import math

import font_glyphs
from palette import *
from pixel import Canvas

PANEL = rgb('#2a201c')
PANEL_DARK = rgb('#1e1714')
PANEL_LIGHT = rgb('#3a2c24')
INK = rgb('#140e0c')


def _band(c, inset, light, mid, dark):
    """One ring of a bevelled frame: light on top/left, dark on bottom/right."""
    w, h = c.w, c.h
    for i in range(inset, w - inset):
        c.px(i, inset, light)
        c.px(i, h - 1 - inset, dark)
    for j in range(inset, h - inset):
        c.px(inset, j, light if j < h - 1 - inset else dark)
        c.px(w - 1 - inset, j, dark if j > inset else light)


def _rivet(c, x, y):
    c.px(x, y, BRASS[4])
    c.px(x + 1, y, BRASS[2])
    c.px(x, y + 1, BRASS[2])
    c.px(x + 1, y + 1, BRASS[0])


def _rosette(c, cx, cy, r=4.5):
    """Small brass cog with a dark hub: the corner ornament."""
    c.gear(cx, cy, r, r - 1.5, 8, BRASS[2], BRASS[0], BRASS[4], angle=0.2, hole=0)
    c.disc(cx, cy, 1.6, INK)
    c.px(cx - 0.5, cy - 0.5, BRASS[3])


def _fill(c, x0, y0, x1, y1, col):
    for y in range(y0, y1):
        for x in range(x0, x1):
            c.px(x, y, col)


def window():
    """48x48, slice 16. Brass frame with cog rosettes and rivets in the corners."""
    c = Canvas(48)
    _fill(c, 0, 0, 48, 48, PANEL)
    c.box(0, 0, 48, 48, INK)
    _band(c, 1, BRASS[4], BRASS[3], BRASS[1])
    _band(c, 2, BRASS[3], BRASS[2], BRASS[0])
    _band(c, 3, BRASS[2], BRASS[2], BRASS[0])
    _band(c, 4, BRASS[1], BRASS[1], BRASS[0])
    c.box(5, 5, 38, 38, INK)
    # Inner shadow under the frame.
    for i in range(6, 42):
        c.px(i, 6, PANEL_DARK)
        c.px(6, i, PANEL_DARK)
    # Corner ornaments: a cog rosette seated on the frame corner, flanked by two rivets on the band.
    for cx, cy in ((6, 6), (42, 6), (6, 42), (42, 42)):
        c.disc(cx, cy, 6, INK)
        c.gear(cx, cy, 5.5, 4.0, 8, BRASS[2], BRASS[0], BRASS[4], angle=0.2, hole=0)
        c.disc(cx, cy, 2.0, INK)
        c.px(cx - 1, cy - 1, BRASS[3])
        sx = 1 if cx < 24 else -1
        sy = 1 if cy < 24 else -1
        _rivet(c, cx + sx * 9 - (1 if sx < 0 else 0), 2 if sy > 0 else 44)
        _rivet(c, 2 if sx > 0 else 44, cy + sy * 9 - (1 if sy < 0 else 0))
    return c


def small_frame(highlight=False):
    """24x24, slice 8. Slim brass frame with rivet corners (hotbar, minimap, info badges)."""
    c = Canvas(24)
    _fill(c, 0, 0, 24, 24, rgb('#241b17'))
    c.box(0, 0, 24, 24, INK)
    ramp = BRASS if not highlight else [BRASS[1], BRASS[2], BRASS[3], BRASS[4], rgb('#fff2c0')]
    _band(c, 1, ramp[4], ramp[3], ramp[1])
    _band(c, 2, ramp[2], ramp[2], ramp[0])
    c.box(3, 3, 18, 18, INK)
    for x, y in ((1, 1), (21, 1), (1, 21), (21, 21)):
        _rivet(c, x, y)
    return c


def plaque():
    """32x16, slice left/right 8, top/bottom 5. Riveted iron title plate with brass rims."""
    c = Canvas(32, 16)
    _fill(c, 0, 0, 32, 16, IRON[1])
    for y in range(16):
        t = y / 15
        col = IRON[2] if t < 0.3 else IRON[1] if t < 0.75 else IRON[0]
        _fill(c, 0, y, 32, y + 1, col)
    c.box(0, 0, 32, 16, INK)
    for x in range(1, 31):
        c.px(x, 1, BRASS[3])
        c.px(x, 14, BRASS[0])
    for y in range(1, 15):
        c.px(1, y, BRASS[2])
        c.px(30, y, BRASS[0])
    _rivet(c, 3, 6)
    _rivet(c, 27, 6)
    return c


def button(state):
    """24x16, slice 6. States: normal, hover, down, primary, primary_hover, disabled."""
    c = Canvas(24, 16)
    if state.startswith('primary'):
        ramp = BRASS if state == 'primary' else [BRASS[1], BRASS[2], BRASS[3], BRASS[4], rgb('#fff2c0')]
        face = ramp[2]
    else:
        ramp = [rgb('#2a2220'), rgb('#3e322c'), rgb('#54443a'), rgb('#6e5a4a'), rgb('#8a745e')]
        if state == 'hover':
            ramp = [ramp[1], ramp[2], ramp[3], ramp[4], rgb('#a68c70')]
        face = ramp[2]
    _fill(c, 0, 0, 24, 16, face)
    c.box(0, 0, 24, 16, INK)
    if state == 'down':
        _band(c, 1, ramp[0], ramp[1], ramp[3])
    else:
        _band(c, 1, ramp[4], ramp[3], ramp[0])
        _band(c, 2, ramp[3], ramp[2], ramp[1])
    # Brass trim pins in each corner.
    pin = BRASS[3] if not state.startswith('primary') else BRASS[0]
    for x, y in ((3, 3), (20, 3), (3, 12), (20, 12)):
        c.px(x, y, pin)
    if state == 'disabled':
        for y in range(16):
            for x in range(24):
                r, g, b, a = c.get(x, y)
                if a:
                    v = (r + g + b) // 3
                    c.px(x, y, (v // 2 + 20, v // 2 + 16, v // 2 + 14))
    return c


def slot(state='normal'):
    """18x18, slice 5. Recessed item slot with brass corner pins. States: normal, hover, selected."""
    c = Canvas(18)
    _fill(c, 0, 0, 18, 18, rgb('#1a1412'))
    c.box(0, 0, 18, 18, INK)
    if state == 'selected':
        _band(c, 1, rgb('#fff2c0'), BRASS[4], BRASS[3])
        _band(c, 2, BRASS[3], BRASS[3], BRASS[2])
    else:
        rim = rgb('#5a4636') if state == 'normal' else rgb('#8a6c4e')
        _band(c, 1, rgb('#0e0a08'), rim, rim)   # shadowed top/left, lit bottom/right: recessed
        _band(c, 2, rgb('#120e0c'), rgb('#2a201a'), rgb('#3a2c22'))
    for x, y in ((1, 1), (16, 1), (1, 16), (16, 16)):
        c.px(x, y, BRASS[3] if state != 'selected' else rgb('#fff2c0'))
    return c


def row_plate(state='normal'):
    """16x16, slice 4. Plank-like row background for crafting and research lists."""
    c = Canvas(16)
    face = {'normal': rgb('#30251f'), 'hover': rgb('#40322a'), 'current': rgb('#2a3036')}[state]
    _fill(c, 0, 0, 16, 16, face)
    c.box(0, 0, 16, 16, INK)
    edge = BRASS[1] if state != 'current' else rgb('#6aa8d0')
    for x in range(1, 15):
        c.px(x, 1, edge if state != 'normal' else rgb('#4a3a30'))
        c.px(x, 14, rgb('#1a1310'))
    # Brass stripe on the left edge.
    for y in range(1, 15):
        c.px(1, y, BRASS[2] if state != 'current' else rgb('#6aa8d0'))
        c.px(2, y, BRASS[0] if state != 'current' else rgb('#3a6a8a'))
    return c


def bar_track():
    """16x8, slice 3. Recessed gauge track with brass end caps."""
    c = Canvas(16, 8)
    _fill(c, 0, 0, 16, 8, rgb('#120d0b'))
    c.box(0, 0, 16, 8, INK)
    for x in range(1, 15):
        c.px(x, 1, rgb('#0a0706'))
        c.px(x, 6, rgb('#3a2c22'))
    for y in range(1, 7):
        c.px(1, y, BRASS[2])
        c.px(14, y, BRASS[1])
    return c


def bar_fill(kind):
    """8x8, slice 2. Gauge fill with a highlight stripe."""
    ramp = {'brass': [BRASS[1], BRASS[3], BRASS[4]], 'fuel': [FIRE[0], FIRE[2], FIRE[3]],
            'research': [rgb('#1e4a6a'), rgb('#3e86b8'), rgb('#8ac6e8')]}[kind]
    c = Canvas(8)
    _fill(c, 0, 0, 8, 8, ramp[1])
    for x in range(8):
        c.px(x, 1, ramp[2])
        c.px(x, 6, ramp[0])
        c.px(x, 7, ramp[0])
    c.px(0, 0, ramp[0])
    return c


def close_button(hover=False):
    """16x16 brass (or red on hover) disc with an engraved X."""
    c = Canvas(16)
    if hover:
        c.shaded_disc(8, 8, 7, RED_LIQ[2], RED_LIQ[1], RED_LIQ[0])
    else:
        c.shaded_disc(8, 8, 7, BRASS[4], BRASS[2], BRASS[0])
    c.ring(8, 8, 7, INK, 1.0)
    for i in range(-3, 4):
        c.px(8 + i - 0.5, 8 + i - 0.5, INK)
        c.px(8 + i - 0.5, 8 - i - 0.5, INK)
    return c


def field(focus=False):
    """16x16, slice 4. Inset text field with a brass underline."""
    c = Canvas(16)
    _fill(c, 0, 0, 16, 16, rgb('#140f0d'))
    c.box(0, 0, 16, 16, INK)
    for x in range(1, 15):
        c.px(x, 1, rgb('#0a0706'))
        c.px(x, 14, BRASS[3] if focus else BRASS[1])
    for y in range(1, 15):
        c.px(1, y, rgb('#0a0706'))
        c.px(14, y, rgb('#3a2c22'))
    return c


def toggle(on):
    """12x12 checkbox: brass box, engraved check when on."""
    c = Canvas(12)
    c.bevel(1, 1, 10, 10, BRASS[2], BRASS[3], BRASS[0])
    c.box(0, 0, 12, 12, INK)
    c.rect(3, 3, 6, 6, rgb('#1a1412'))
    if on:
        for x, y in ((3, 6), (4, 7), (5, 8), (6, 7), (7, 6), (8, 5), (8, 4)):
            c.px(x, y, rgb('#f4dc8c'))
    return c


def scroll_part(kind):
    """8x16, slice 3. 'track' (recessed) or 'thumb' (brass)."""
    c = Canvas(8, 16)
    if kind == 'track':
        _fill(c, 0, 0, 8, 16, rgb('#140f0d'))
        c.box(0, 0, 8, 16, INK)
    else:
        c.bevel(1, 1, 6, 14, BRASS[2], BRASS[3], BRASS[0])
        c.box(0, 0, 8, 16, INK)
        for y in (6, 8, 10):
            c.px(3, y, BRASS[0])
            c.px(4, y, BRASS[0])
    return c


def gear_icon():
    """9x9 cog used as a bullet on section headings."""
    c = Canvas(9)
    c.gear(4.5, 4.5, 4.4, 3.0, 8, BRASS[2], BRASS[0], BRASS[4], angle=0.3, hole=1.1)
    return c


def slider(kind):
    """Slider 'track' (16x6, slice 3) or 'handle' (8x12 brass lever)."""
    if kind == 'track':
        c = Canvas(16, 6)
        _fill(c, 0, 0, 16, 6, rgb('#120d0b'))
        c.box(0, 0, 16, 6, INK)
        for x in range(1, 15):
            c.px(x, 4, rgb('#3a2c22'))
        return c
    c = Canvas(8, 12)
    c.bevel(1, 1, 6, 10, BRASS[3], BRASS[4], BRASS[0])
    c.box(0, 0, 8, 12, INK)
    c.px(3, 5, BRASS[0])
    c.px(4, 5, BRASS[0])
    return c


def logo():
    """COGWORKS in bevelled brass letters at 2x font pixels, with both O's replaced by real cogs."""
    text = 'COGWORKS'
    scale = 2
    widths = [len(font_glyphs.G[ch][0]) for ch in text]
    w = (sum(widths) + len(text) - 1) * scale + 6
    h = 7 * scale + 6
    c = Canvas(w, h)
    x = 3
    for ch, gw in zip(text, widths):
        if ch == 'O':
            cx, cy = x + gw * scale / 2, 3 + 7 * scale / 2
            c.gear(cx, cy, 7.4, 5.4, 10, BRASS[3], BRASS[0], BRASS[4], angle=0.15, hole=2.2)
            c.ring(cx, cy, 3.6, BRASS[1], 1.0)
        else:
            rows = font_glyphs.G[ch]
            for r in range(7):
                for gx in range(gw):
                    if rows[r][gx] != '#':
                        continue
                    for sy in range(scale):
                        yy = r * scale + sy
                        # Vertical gradient: bright top, deep bottom, with a lit top edge on each stroke.
                        col = BRASS[4] if yy < 3 else BRASS[3] if yy < 7 else BRASS[2] if yy < 11 else BRASS[1]
                        for sx in range(scale):
                            c.px(x + gx * scale + sx, 3 + yy, col)
        x += (gw + 1) * scale
    out = Canvas(w, h)
    for yy in range(h):
        for xx in range(w):
            if c.opaque(xx, yy):
                out.px(xx + 1, yy + 2, INK)  # drop shadow
    out.paste(c, 0, 0)
    out.outline(INK)
    return out


PIECES = {
    'ui_window': window,
    'ui_frame': small_frame,
    'ui_frame_highlight': lambda: small_frame(True),
    'ui_plaque': plaque,
    'ui_button': lambda: button('normal'),
    'ui_button_hover': lambda: button('hover'),
    'ui_button_down': lambda: button('down'),
    'ui_button_primary': lambda: button('primary'),
    'ui_button_primary_hover': lambda: button('primary_hover'),
    'ui_button_disabled': lambda: button('disabled'),
    'ui_slot': lambda: slot('normal'),
    'ui_slot_hover': lambda: slot('hover'),
    'ui_slot_selected': lambda: slot('selected'),
    'ui_row': lambda: row_plate('normal'),
    'ui_row_hover': lambda: row_plate('hover'),
    'ui_row_current': lambda: row_plate('current'),
    'ui_bar_track': bar_track,
    'ui_bar_fill': lambda: bar_fill('brass'),
    'ui_bar_fill_fuel': lambda: bar_fill('fuel'),
    'ui_bar_fill_research': lambda: bar_fill('research'),
    'ui_close': close_button,
    'ui_close_hover': lambda: close_button(True),
    'ui_field': field,
    'ui_field_focus': lambda: field(True),
    'ui_toggle_off': lambda: toggle(False),
    'ui_toggle_on': lambda: toggle(True),
    'ui_scroll_track': lambda: scroll_part('track'),
    'ui_scroll_thumb': lambda: scroll_part('thumb'),
    'ui_gear': gear_icon,
    'ui_slider_track': lambda: slider('track'),
    'ui_slider_handle': lambda: slider('handle'),
    'ui_logo': logo,
}
