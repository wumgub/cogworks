"""Contact sheet of generated sprites at high zoom, for reviewing the art."""
import os
import sys

from PIL import Image, ImageDraw


def sheet(src_dir, out_path, prefixes=None, scale=6, cols=12):
    files = sorted(f for f in os.listdir(src_dir) if f.endswith('.png'))
    if prefixes:
        files = [f for f in files if any(f.startswith(p) for p in prefixes)]
    imgs = [Image.open(os.path.join(src_dir, f)).convert('RGBA') for f in files]
    cell = max(max(i.width, i.height) for i in imgs) * scale + 8
    rows = (len(imgs) + cols - 1) // cols
    out = Image.new('RGBA', (cols * cell, rows * (cell + 12)), (40, 38, 44, 255))
    d = ImageDraw.Draw(out)
    for n, (f, im) in enumerate(zip(files, imgs)):
        x, y = (n % cols) * cell, (n // cols) * (cell + 12)
        # Checker behind transparent sprites.
        for cy in range(0, im.height * scale, scale * 2):
            for cx in range(0, im.width * scale, scale * 2):
                d.rectangle([x + 4 + cx, y + 4 + cy, x + 4 + cx + scale - 1, y + 4 + cy + scale - 1], fill=(56, 54, 60, 255))
        big = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
        out.alpha_composite(big, (x + 4, y + 4))
        d.text((x + 4, y + cell - 4), f[:-4][:18], fill=(200, 200, 200, 255))
    out.save(out_path)


if __name__ == '__main__':
    sheet(sys.argv[1], sys.argv[2], sys.argv[3:] or None)
