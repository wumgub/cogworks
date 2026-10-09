"""App icon (64x64 pixel art, exported at 256 and 1024) and a large README logo."""
import os

from palette import *
from pixel import Canvas
import ui


def icon():
    c = Canvas(64)
    # Rounded dark-wood tile with a brass rim.
    for y in range(64):
        for x in range(64):
            dx, dy = min(x, 63 - x), min(y, 63 - y)
            if dx < 4 and dy < 4 and (4 - dx) ** 2 + (4 - dy) ** 2 > 18:
                continue
            c.px(x, y, rgb('#2a201c'))
    for i in range(64):
        for (x, y) in ((i, 2), (i, 61), (2, i), (61, i)):
            if c.opaque(x, y):
                c.px(x, y, BRASS[3] if (x < 32 and y < 32) else BRASS[1])
    # Big brass cog with a smaller iron cog meshing at the lower right, and a belt strip below.
    c.disc(27, 27, 19, ui.INK)
    c.gear(27, 27, 18, 13.5, 12, BRASS[2], BRASS[0], BRASS[4], angle=0.12, hole=0)
    c.disc(27, 27, 7, ui.INK)
    c.disc(27, 27, 5, BRASS[1])
    c.disc(26, 26, 2, BRASS[4])
    c.disc(45, 45, 11, ui.INK)
    c.gear(45, 45, 10, 7.2, 9, IRON[3], IRON[1], IRON[4], angle=0.4, hole=2.5)
    return c


def main():
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    ic = icon()
    from PIL import Image
    img = Image.fromarray(ic.a, 'RGBA')
    out_dir = os.path.join(root, 'Assets', '_Game', 'Art', 'Icon')
    os.makedirs(out_dir, exist_ok=True)
    img.resize((1024, 1024), Image.NEAREST).save(os.path.join(out_dir, 'app_icon.png'))
    docs = os.path.join(root, 'Docs')
    img.resize((256, 256), Image.NEAREST).save(os.path.join(docs, 'icon.png'))
    logo = Image.fromarray(ui.logo().a, 'RGBA')
    logo.resize((logo.width * 8, logo.height * 8), Image.NEAREST).save(os.path.join(docs, 'logo.png'))


if __name__ == '__main__':
    main()
