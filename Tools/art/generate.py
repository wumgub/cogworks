"""Regenerates every sprite into Assets/_Game/Art/Resources/Art plus manifest.json.

    python3 Tools/art/generate.py

Each sprite is written as <name>_<frame>.png (single images are frame 0). The manifest lists frame
counts and animation speeds, which the game reads at startup. PNGs can also be hand-edited directly;
re-running this script overwrites them, so keep hand edits out of names it generates.
"""
import json
import os
import sys

import buildings
import characters
import items
import terrain
import ttf
import font_glyphs
import ui

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, 'Assets', '_Game', 'Art', 'Resources', 'Art')
# UI skin and font are referenced from USS by path, so they live outside Resources.
UI_OUT = os.path.join(ROOT, 'Assets', '_Game', 'Art', 'UI')


def write(name, canvases, fps, manifest):
    for i, c in enumerate(canvases):
        c.save(os.path.join(OUT, f'{name}_{i}.png'))
    manifest[name] = {'frames': len(canvases), 'fps': fps}


def main():
    os.makedirs(OUT, exist_ok=True)
    manifest = {}

    write('terrain_grass', [terrain.grass(v) for v in range(4)], 0, manifest)
    write('terrain_dirt', [terrain.dirt(v) for v in range(4)], 0, manifest)
    write('terrain_sand', [terrain.sand(v) for v in range(4)], 0, manifest)
    write('terrain_rock', [terrain.rock(v) for v in range(4)], 0, manifest)
    write('terrain_water', [terrain.water(f) for f in range(4)], 3, manifest)
    for kind in ('iron', 'copper', 'coal', 'stone'):
        write(f'ore_{kind}', [terrain.ore(kind, v) for v in range(6)], 0, manifest)

    for key, fn in items.ITEMS.items():
        write(f'item_{key}', [fn()], 0, manifest)

    for key, (fn, fps) in buildings.BUILDINGS.items():
        write(f'building_{key}', fn(), fps, manifest)

    for slot in range(4):
        for d in ('down', 'up', 'left'):
            write(f'player_{slot}_{d}', [characters.engineer(d, f, slot) for f in range(3)], 8, manifest)
        # Right is an exact mirror of left so the two profiles always match.
        write(f'player_{slot}_right', [characters.engineer('left', f, slot).mirrored() for f in range(3)], 8, manifest)

    os.makedirs(UI_OUT, exist_ok=True)
    for name, fn in ui.PIECES.items():
        fn().save(os.path.join(UI_OUT, f'{name}.png'))
    ttf.write(os.path.join(UI_OUT, 'CogworksPixel.ttf'), font_glyphs.G)

    with open(os.path.join(OUT, 'manifest.json'), 'w') as f:
        json.dump(manifest, f, indent=1, sort_keys=True)
    print(f'wrote {sum(m["frames"] for m in manifest.values())} sprites to {OUT}')


if __name__ == '__main__':
    sys.exit(main())
