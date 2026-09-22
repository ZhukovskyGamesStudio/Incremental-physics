# Builds Melok, the game's chalk hand, from the skeletons in glyphs.py: each skeleton is bent by the hand (slant,
# wobble, a jump off the baseline), stroked with a round pen, overlaps are merged and the curves become TrueType
# quadratics. Writes Assets/Resources/Fonts/Melok-Regular.ttf.
#   python build.py [out.ttf]          needs: pip install fonttools skia-pathops
import math, random, sys, os
import pathops
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.cu2quPen import Cu2QuPen
from fontTools.pens.transformPen import TransformPen
import glyphs as GL

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', '..', 'Assets', 'Resources', 'Fonts', 'Melok-Regular.ttf')

# sw = pen width; W = width factor; slant in degrees; jit = wobble of the stroke; rot/bounce/size = per-letter tilt,
# baseline jump and size change; sb = side bearing; seed = what the hand's randomness grows from (kept from the
# approved sample "B", so the letters keep the exact bends that were chosen)
HAND = dict(family='Melok', sw=58, W=0.97, slant=8, jit=13, rot=3.2, bounce=17, size=0.045, sb=34, seed='Chalk B Boiky')


def seg_points(strokes, fn):
    out = []
    for st in strokes:
        pts = [(s[0],) + tuple(fn(p) for p in s[1:]) for s in st]
        out.append(GL.Fill(pts) if isinstance(st, GL.Fill) else pts)
    return out


def bbox(strokes):
    xs = [p[0] for st in strokes for s in st for p in s[1:]]
    ys = [p[1] for st in strokes for s in st for p in s[1:]]
    return (min(xs), min(ys), max(xs), max(ys)) if xs else (0, 0, 0, 0)


def bend(strokes, ch, st=HAND):
    """The hand: wobble, tilt, jump off the baseline, then slant and width."""
    rnd = random.Random(f"{ch}-{st['seed']}")
    ph = [rnd.uniform(0, 6.283) for _ in range(6)]
    jit = st['jit']
    rot = math.radians(rnd.uniform(-st['rot'], st['rot']))
    dy = rnd.uniform(-st['bounce'], st['bounce'])
    sc = 1 + rnd.uniform(-st['size'], st['size'])
    x0, y0, x1, y1 = bbox(strokes)
    cx, cy = (x0 + x1) / 2, GL.X / 2
    cr, sr = math.cos(rot), math.sin(rot)
    k = 2 * math.pi / 620
    sl = math.tan(math.radians(st['slant']))

    def f(p):
        x, y = p
        if jit:
            x += jit * math.sin(x * k + ph[0]) * math.cos(y * k * 0.8 + ph[1]) + jit * 0.4 * math.sin(y * k * 2.1 + ph[4])
            y += jit * math.cos(x * k * 0.9 + ph[2]) * math.sin(y * k + ph[3]) + jit * 0.4 * math.sin(x * k * 2.3 + ph[5])
        x, y = cx + (x - cx) * sc, y * sc
        x, y = cx + (x - cx) * cr - (y - cy) * sr, cy + (x - cx) * sr + (y - cy) * cr
        y += dy
        x += y * sl
        return (x * st['W'], y)

    return seg_points(strokes, f)


def to_path(segs):
    p = pathops.Path()
    p.moveTo(*segs[0][1])
    for q in segs:
        if q[0] == 'L':
            p.lineTo(*q[2])
        else:
            p.cubicTo(*q[2], *q[3], *q[4])
    return p


def pen_outline(strokes, st=HAND):
    acc = pathops.Path()
    n = 0
    for i, segs in enumerate(strokes):
        if not segs:
            continue
        # strokes that start where another ends would give pathops coincident caps, which it can drop: nudge each
        # stroke by a hair (a fraction of a unit, invisible)
        e = (0.17 + 0.23 * i, 0.11 + 0.19 * i)
        pts = [(q[0],) + tuple((p[0] + e[0], p[1] + e[1]) for p in q[1:]) for q in segs]
        closed = GL._d(pts[0][1], pts[-1][-1]) < 0.5
        p = to_path(pts)
        if closed and len(pts) > 1:
            p.close()
        elif closed:
            p.lineTo(pts[0][1][0] + 0.01, pts[0][1][1])
        if isinstance(segs, GL.Fill):
            inside = to_path(pts)
            inside.close()
            inside.simplify(fix_winding=True, keep_starting_points=False, clockwise=True)
            inside.draw(acc.getPen())
        p.stroke(st['sw'], pathops.LineCap.ROUND_CAP, pathops.LineJoin.ROUND_JOIN, 4)
        p.convertConicsToQuads()
        p.simplify(fix_winding=True, keep_starting_points=False, clockwise=True)
        p.draw(acc.getPen())       # all outlines one way round: the non-zero winding of the lot is their union
        n += 1
    if not n:
        return None
    acc.simplify(fix_winding=True, keep_starting_points=False, clockwise=True)
    return acc


def build(out=OUT, st=HAND):
    order = ['.notdef']
    cmap, glyf, hmtx = {}, {}, {}

    nd = TTGlyphPen(None)
    for pts in ([(50, 0), (50, 700), (450, 700), (450, 0)], [(100, 50), (400, 50), (400, 650), (100, 650)]):
        nd.moveTo(pts[0]); [nd.lineTo(q) for q in pts[1:]]; nd.closePath()
    glyf['.notdef'] = nd.glyph()
    hmtx['.notdef'] = (500, 50)

    digit = int(round(GL.DIGW * st['W'] + st['sw'] + 2 * st['sb'] * 0.7))
    for ch, fn in GL.G.items():
        name = 'space' if ch == ' ' else f'uni{ord(ch):04X}'
        order.append(name)
        cmap[ord(ch)] = name
        strokes = fn()
        if not strokes:
            w = GL.SPACES.get(ch, 260)
            glyf[name] = TTGlyphPen(None).glyph()
            hmtx[name] = (digit if w == 'digit' else int(w * st['W']), 0)
            continue
        path = pen_outline(bend(strokes, ch, st), st)
        bx0, by0, bx1, by1 = path.bounds
        lf, rf = GL.SB.get(ch, (1, 1))
        sb = st['sb']
        if ch in GL.TAB:
            adv = digit
            shift = (adv - (bx1 - bx0)) / 2 - bx0
        else:
            shift = sb * lf - bx0
            adv = (bx1 - bx0) + sb * lf + sb * rf
        tp = TTGlyphPen(None)
        path.draw(TransformPen(Cu2QuPen(tp, max_err=1.0, reverse_direction=False), (1, 0, 0, 1, shift, 0)))
        glyf[name] = tp.glyph()
        hmtx[name] = (int(round(adv)), int(round(bx0 + shift)))

    fb = FontBuilder(1000, isTTF=True)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap(cmap)
    fb.setupGlyf(glyf)
    fb.setupHorizontalMetrics(hmtx)
    fb.setupHorizontalHeader(ascent=900, descent=-300)
    fb.setupNameTable({'familyName': st['family'], 'styleName': 'Regular', 'version': 'Version 1.000',
                       'copyright': 'Zhukovsky Games', 'uniqueFontIdentifier': 'ZhukovskyGames: Melok Regular'})
    fb.setupOS2(sTypoAscender=820, sTypoDescender=-280, sTypoLineGap=0, usWinAscent=940, usWinDescent=330,
                sxHeight=GL.X, sCapHeight=GL.CAP, fsType=0, achVendID='ZHUK')
    fb.setupPost()
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    fb.save(out)
    return out


if __name__ == '__main__':
    p = build(sys.argv[1] if len(sys.argv) > 1 else OUT)
    print(os.path.normpath(p), os.path.getsize(p), 'bytes')
