# Sanity checks of the outlines, run after editing glyphs.py:
#  - the merged glyph covers at least its biggest stroke and no more than all of them (pathops dropped nothing),
#  - no stroke covers more than its length times the pen plus one dab (a loop that got filled in),
#  - every character the game's sources use is in the font.
import glob, math, os, re, sys
import glyphs as GL, build as B

HERE = os.path.dirname(os.path.abspath(__file__))
bad = 0


def length(segs):
    L = 0
    for q in segs:
        pts = q[1:]
        if q[0] == 'L':
            L += GL._d(pts[0], pts[1])
            continue
        prev = pts[0]
        for i in range(1, 17):
            t = i / 16
            w = ((1 - t) ** 3, 3 * (1 - t) ** 2 * t, 3 * (1 - t) * t * t, t ** 3)
            x, y = sum(c * p[0] for c, p in zip(w, pts)), sum(c * p[1] for c, p in zip(w, pts))
            L += math.hypot(x - prev[0], y - prev[1])
            prev = (x, y)
    return L


sw = B.HAND['sw']
for ch, fn in GL.G.items():
    strokes = fn()
    if not strokes:
        continue
    bent = B.bend(strokes, ch)
    parts = [B.pen_outline([s]) for s in bent]
    tot = B.pen_outline(bent)
    mx, sm = max(abs(p.area) for p in parts), sum(abs(p.area) for p in parts)
    if abs(tot.area) < mx * 0.999 or abs(tot.area) > sm * 1.001:
        bad += 1
        print(f'merge lost something: {ch!a} {abs(tot.area):.0f} (biggest stroke {mx:.0f}, all {sm:.0f})')
    for s, p in zip(bent, parts):
        if isinstance(s, GL.Fill):
            continue
        cap = length(s) * sw + math.pi * (sw / 2) ** 2
        if abs(p.area) > cap * 1.01:
            bad += 1
            print(f'a loop got filled in: {ch!a} {abs(p.area):.0f} > {cap:.0f}')

used = set()
for f in glob.glob(os.path.join(HERE, '..', '..', 'Assets', '**', '*.cs'), recursive=True):
    for m in re.findall(r'"((?:[^"\\\n]|\\.)*)"', open(f, encoding='utf-8').read()):
        used.update(m)
missing = sorted(c for c in used if c not in GL.G and ord(c) >= 32 and c not in '\\')
if missing:
    bad += 1
    print('used by the game but not in the font:', ' '.join(f'{c!a}' for c in missing))

print(len(GL.G), 'glyphs,', 'all good' if not bad else f'{bad} problems')
sys.exit(1 if bad else 0)
