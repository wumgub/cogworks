"""Minimal TrueType writer for pixel fonts: every ink pixel run becomes a square-cornered contour.

Writes the required tables (cmap, glyf, head, hhea, hmtx, loca, maxp, name, OS/2, post) by hand so no
font library is needed.
"""
import struct

PX = 128            # font units per pixel
ASCENT_PX = 7       # rows 0-6 are on/above the baseline
DESCENT_PX = 2
EM = 1024           # 8 pixels per em


def _checksum(data):
    data += b'\0' * ((4 - len(data) % 4) % 4)
    return sum(struct.unpack('>%dI' % (len(data) // 4), data)) & 0xFFFFFFFF


def _glyph_contours(rows):
    """Horizontal ink runs per row -> rectangles in font units (y up, baseline at 0)."""
    rects = []
    for r, row in enumerate(rows):
        top = (ASCENT_PX - r) * PX          # row 0 top edge is the ascent line
        x = 0
        while x < len(row):
            if row[x] != '#':
                x += 1
                continue
            start = x
            while x < len(row) and row[x] == '#':
                x += 1
            rects.append((start * PX, top - PX, x * PX, top))
    return rects


def _encode_glyph(rects):
    if not rects:
        return b''
    xs = [v for r in rects for v in (r[0], r[2])]
    ys = [v for r in rects for v in (r[1], r[3])]
    out = struct.pack('>hhhhh', len(rects), min(xs), min(ys), max(xs), max(ys))
    ends, pts, n = [], [], 0
    for x0, y0, x1, y1 in rects:
        # Clockwise (TrueType outer contours): bottom-left, top-left, top-right, bottom-right.
        pts += [(x0, y0), (x0, y1), (x1, y1), (x1, y0)]
        n += 4
        ends.append(n - 1)
    out += struct.pack('>%dH' % len(ends), *ends)
    out += struct.pack('>H', 0)  # no instructions
    out += bytes([1] * len(pts))  # on-curve, 16-bit signed deltas
    px = py = 0
    xd, yd = b'', b''
    for x, y in pts:
        xd += struct.pack('>h', x - px)
        yd += struct.pack('>h', y - py)
        px, py = x, y
    out += xd + yd
    return out


def _name_table(family):
    records = [(1, family), (2, 'Regular'), (3, family + ' Regular'), (4, family), (5, 'Version 1.0'),
               (6, family.replace(' ', '') + '-Regular')]
    strings = b''
    recs = b''
    for name_id, text in records:
        data = text.encode('utf-16-be')
        recs += struct.pack('>HHHHHH', 3, 1, 0x409, name_id, len(data), len(strings))
        strings += data
    header = struct.pack('>HHH', 0, len(records), 6 + 12 * len(records))
    return header + recs + strings


def _cmap_table(char_to_gid):
    # Format 4, one segment per code point (simple and fine for ~100 glyphs) plus the 0xFFFF terminator.
    codes = sorted(char_to_gid)
    segs = [(c, c, (char_to_gid[c] - c) & 0xFFFF) for c in codes] + [(0xFFFF, 0xFFFF, 1)]
    n = len(segs)
    search = 2 * (1 << (n.bit_length() - 1))
    body = struct.pack('>HHHH', 2 * n, search, (n.bit_length() - 1), 2 * n - search)
    body += struct.pack('>%dH' % n, *[s[1] for s in segs])
    body += struct.pack('>H', 0)
    body += struct.pack('>%dH' % n, *[s[0] for s in segs])
    body += struct.pack('>%dH' % n, *[s[2] for s in segs])
    body += struct.pack('>%dH' % n, *([0] * n))
    sub = struct.pack('>HHH', 4, 6 + len(body), 0) + body
    return struct.pack('>HHHHI', 0, 1, 3, 1, 12) + sub


def write(path, glyphs, family='Cogworks Pixel'):
    """glyphs: dict char -> list of 9 row strings."""
    chars = sorted(glyphs, key=ord)
    # Glyph 0 is .notdef: a hollow box.
    notdef = ['#####', '#...#', '#...#', '#...#', '#...#', '#...#', '#####', '.....', '.....']
    order = [notdef] + [glyphs[c] for c in chars]
    char_to_gid = {ord(c): i + 1 for i, c in enumerate(chars)}

    glyf = b''
    loca = []
    hmtx = b''
    max_pts = max_contours = 0
    x_max_all = 0
    for rows in order:
        rects = _glyph_contours(rows)
        loca.append(len(glyf))
        data = _encode_glyph(rects)
        data += b'\0' * ((4 - len(data) % 4) % 4)
        glyf += data
        width = len(rows[0])
        advance = (width + 1) * PX  # one pixel of spacing
        lsb = min((r[0] for r in rects), default=0)
        hmtx += struct.pack('>Hh', advance, lsb)
        max_pts = max(max_pts, 4 * len(rects))
        max_contours = max(max_contours, len(rects))
        x_max_all = max(x_max_all, width * PX)
    loca.append(len(glyf))
    num = len(order)
    ascender, descender = ASCENT_PX * PX, -DESCENT_PX * PX

    head = struct.pack('>IIIIHHqqhhhhHHhhh', 0x00010000, 0x00010000, 0, 0x5F0F3CF5, 0x000B, EM, 0, 0,
                       0, descender, x_max_all, ascender, 0, 8, 2, 1, 0)
    hhea = struct.pack('>IhhhHhhhhhhhhhhhH', 0x00010000, ascender, descender, PX, x_max_all + PX, 0, 0,
                       x_max_all, 1, 0, 0, 0, 0, 0, 0, 0, num)
    maxp = struct.pack('>IHHHHHHHHHHHHHH', 0x00010000, num, max_pts, max_contours, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0)
    os2 = struct.pack('>HhHHH' + 'h' * 11, 4, 6 * PX, 400, 5, 0, 650, 700, 0, 140, 650, 700, 0, 480, 50, 300, 0) \
        + bytes(10) + struct.pack('>IIII', 1, 0, 0, 0) + b'NONE' \
        + struct.pack('>HHHhhhHHII', 0x40, 32, 0xFFFF if max(char_to_gid) > 0xFFFF else max(char_to_gid),
                      ascender, descender, PX, ascender, -descender, 1, 0) \
        + struct.pack('>hhHHH', 5 * PX, 7 * PX, 0, 32, 1)
    post = struct.pack('>IIhhIIIII', 0x00030000, 0, -PX, PX, 0, 0, 0, 0, 0)
    loca_t = struct.pack('>%dI' % len(loca), *loca)

    tables = {
        b'OS/2': os2, b'cmap': _cmap_table(char_to_gid), b'glyf': glyf, b'head': head, b'hhea': hhea,
        b'hmtx': hmtx, b'loca': loca_t, b'maxp': maxp, b'name': _name_table(family), b'post': post,
    }
    tags = sorted(tables)
    n = len(tags)
    search = 16 * (1 << (n.bit_length() - 1))
    offset = 12 + 16 * n
    directory = struct.pack('>IHHHH', 0x00010000, n, search, n.bit_length() - 1, 16 * n - search)
    body = b''
    for tag in tags:
        data = tables[tag]
        directory += struct.pack('>4sIII', tag, _checksum(data), offset + len(body), len(data))
        body += data + b'\0' * ((4 - len(data) % 4) % 4)
    font = bytearray(directory + body)
    # head.checkSumAdjustment so the whole file sums to 0xB1B0AFBA.
    head_offset = 12 + 16 * n + sum(len(tables[t]) + (-len(tables[t])) % 4 for t in tags[:tags.index(b'head')])
    adjust = (0xB1B0AFBA - _checksum(bytes(font))) & 0xFFFFFFFF
    struct.pack_into('>I', font, head_offset + 8, adjust)
    with open(path, 'wb') as f:
        f.write(font)
