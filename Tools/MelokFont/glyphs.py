# Skeletons of the letters: the path the chalk takes, in font units (1000 per em), y up, baseline 0.
# A stroke is a list of segments ('L', p0, p1) or ('C', p0, c1, c2, p1). The pen is applied later, per style.
import math

X, A, CAP, D, O = 500, 730, 700, -230, 12     # x-height, ascender, cap height, descender, overshoot of round forms
DIG = 690                                     # digit height
AX = 280                                      # math axis: + = × → sit on it


def _p(p):
    return (float(p[0]), float(p[1]))


def _d(a, b):
    return math.hypot(b[0] - a[0], b[1] - a[1])


def line(*pts):
    pts = [_p(p) for p in pts]
    return [('L', a, b) for a, b in zip(pts, pts[1:])]


def spline(*pts, closed=False, alpha=0.5):
    """Centripetal Catmull-Rom through the points, as cubics: a hand drawing a curve through where it means to go."""
    pts = [_p(p) for p in pts]
    n = len(pts)
    if closed:
        ext = [pts[-1]] + pts + [pts[0], pts[1]]
        count = n
    else:
        a = (2 * pts[0][0] - pts[1][0], 2 * pts[0][1] - pts[1][1])
        b = (2 * pts[-1][0] - pts[-2][0], 2 * pts[-1][1] - pts[-2][1])
        ext = [a] + pts + [b]
        count = n - 1
    segs = []
    for i in range(count):
        p0, p1, p2, p3 = ext[i], ext[i + 1], ext[i + 2], ext[i + 3]
        d1 = max(_d(p0, p1), 1e-3) ** alpha
        d2 = max(_d(p1, p2), 1e-3) ** alpha
        d3 = max(_d(p2, p3), 1e-3) ** alpha
        c1 = tuple((d1 * d1 * p2[k] - d2 * d2 * p0[k] + (2 * d1 * d1 + 3 * d1 * d2 + d2 * d2) * p1[k]) / (3 * d1 * (d1 + d2)) for k in (0, 1))
        c2 = tuple((d3 * d3 * p1[k] - d2 * d2 * p3[k] + (2 * d3 * d3 + 3 * d3 * d2 + d2 * d2) * p2[k]) / (3 * d3 * (d3 + d2)) for k in (0, 1))
        segs.append(('C', p1, c1, c2, p2))
    return segs


def arc(cx, cy, rx, ry, a0, a1, rot=0.0):
    """Elliptical arc from angle a0 to a1 (degrees, counter-clockwise positive; a1 < a0 goes clockwise)."""
    n = max(1, math.ceil(abs(a1 - a0) / 90.0 - 1e-9))
    cr, sr = math.cos(math.radians(rot)), math.sin(math.radians(rot))

    def at(t):
        x, y = rx * math.cos(t), ry * math.sin(t)
        return (cx + x * cr - y * sr, cy + x * sr + y * cr)

    def der(t):
        x, y = -rx * math.sin(t), ry * math.cos(t)
        return (x * cr - y * sr, x * sr + y * cr)

    segs = []
    for i in range(n):
        ta = math.radians(a0 + (a1 - a0) * i / n)
        tb = math.radians(a0 + (a1 - a0) * (i + 1) / n)
        k = 4.0 / 3.0 * math.tan((tb - ta) / 4.0)
        pa, pb, da, db = at(ta), at(tb), der(ta), der(tb)
        segs.append(('C', pa, (pa[0] + k * da[0], pa[1] + k * da[1]), (pb[0] - k * db[0], pb[1] - k * db[1]), pb))
    return segs


def join(*parts):
    out = []
    for part in parts:
        if not part:
            continue
        if out:
            e, s = out[-1][-1], part[0][1]
            if _d(e, s) > 0.5:
                out.append(('L', e, s))
            else:
                part = [(part[0][0], e) + tuple(part[0][2:])] + part[1:]
        out.extend(part)
    return out


DOT_R, DOT_DIR = 0.0, 0.0              # set by the style: a flat chalk needs a short dab across it to leave a real dot


def dot(x, y):
    """A dab of chalk: a stroke with (almost) no length, so the pen leaves its own shape."""
    r = DOT_R + 0.5
    dx, dy = r * math.cos(math.radians(DOT_DIR)), r * math.sin(math.radians(DOT_DIR))
    return [('L', (x - dx, y - dy), (x + dx, y + dy))]


def xf(strokes, sx=1.0, sy=1.0, dx=0.0, dy=0.0):
    """Scale then move whole strokes (for fractions, superscripts, composites)."""
    return [[(s[0],) + tuple((p[0] * sx + dx, p[1] * sy + dy) for p in s[1:]) for s in st] for st in strokes]


class Fill(list):
    """A closed shape filled in (a solid star, a play triangle); the pen still draws its edge, so corners stay round."""


def fill(*pts):
    return Fill(line(*(list(pts) + [pts[0]])))


# ----------------------------------------------------------------------------------------------------------------
G = {}          # char -> strokes
SB = {}         # char -> (left, right) side-bearing factors: round and open sides sit closer
TAB = set()     # tabular: same width for all digits, so counters do not jiggle


def g(chars, sb=(1.0, 1.0)):
    def reg(f):
        for c in chars:
            G[c] = f
            SB[c] = sb
        return f
    return reg


ROUND = (0.62, 0.62)

# ---------------- Latin lower case ----------------
def bowl_r(cx=205, rx=205):             # bowl opening to the left, touching a stem at x = cx - rx
    return arc(cx, 250, rx, 250 + O, 158, -160)


def bowl_l(cx=200, rx=200):             # bowl opening to the right, touching a stem at x = cx + rx
    return arc(cx, 250, rx, 250 + O, 22, 340)


G_A_DOUBLE = False                      # the style decides: double-storey a (print) or single (hand)


@g('o', ROUND)
def _o():
    return [arc(205, 250, 205, 250 + O, 95, 455)]


@g('c', (0.62, 0.45))
def _c():
    return [arc(200, 250, 200, 250 + O, 48, 312)]


@g('e', (0.62, 0.55))
def _e():
    return [join(line((12, 250), (398, 252)), arc(203, 250, 200, 250 + O, 2, 318))]


@g('a', (0.62, 1.0))
def _a():
    if G_A_DOUBLE:
        top = spline((40, 425), (150, 508), (290, 480), (340, 360), (342, 240))
        return [join(top, line((342, 240), (342, 0))),
                spline((342, 268), (180, 272), (45, 205), (40, 70), (140, -10), (270, 20), (342, 110))]
    return [arc(185, 250, 185, 250 + O, 25, 385), line((370, X + 4), (370, 0))]


@g('b', (1.0, 0.62))
def _b():
    return [line((0, A), (0, 0)), bowl_r(200, 200)]


@g('d', (0.62, 1.0))
def _d_():
    return [bowl_l(200, 200), line((400, A), (400, 0))]


@g('p', (1.0, 0.62))
def _p_():
    return [line((0, X), (0, D)), bowl_r(200, 200)]


@g('q', (0.62, 1.0))
def _q():
    return [bowl_l(200, 200), line((400, X), (400, D))]


@g('g', (0.62, 1.0))
def _g():
    return [bowl_l(195, 195), join(line((390, X), (390, -30)), spline((390, -30), (370, -160), (250, -236), (120, -228), (30, -175)))]


def arch(x0, w, top=X):
    """The shoulder of n/h/m: leaves the stem at x0 and comes down as the next stem at x0 + w."""
    return join(spline((x0, 330), (x0 + w * 0.22, 462), (x0 + w * 0.55, top + 8), (x0 + w * 0.86, 458), (x0 + w, 330), (x0 + w, 200)),
                line((x0 + w, 200), (x0 + w, 0)))


@g('n')
def _n():
    return [line((0, X), (0, 0)), arch(0, 365)]


@g('h')
def _h():
    return [line((0, A), (0, 0)), arch(0, 365)]


@g('m')
def _m():
    return [line((0, X), (0, 0)), arch(0, 262), arch(262, 262)]


@g('r', (1.0, 0.4))
def _r():
    return [line((0, X), (0, 0)), spline((0, 320), (70, 455), (170, 505), (265, 478))]


@g('u')
def _u():
    return [join(line((0, X), (0, 300)), spline((0, 300), (6, 160), (60, 40), (180, -9), (300, 40), (366, 170))), line((366, X), (366, 0))]


@g('v', (0.45, 0.45))
def _v():
    return [line((0, X), (190, 0), (380, X))]


@g('w', (0.4, 0.4))
def _w():
    return [line((0, X), (135, 0), (280, 390), (425, 0), (560, X))]


@g('x', (0.5, 0.5))
def _x():
    return [line((10, X), (360, 0)), line((350, X), (0, 0))]


@g('y', (0.45, 0.45))
def _y():
    return [line((0, X), (222, 45)), join(line((390, X), (175, -110)), spline((175, -110), (130, -195), (60, -232), (0, -222)))]


@g('z', (0.6, 0.6))
def _z():
    return [line((20, X), (350, X), (0, 0), (370, 0))]


@g('s', (0.6, 0.6))
def _s():
    return [spline((330, 430), (235, 502), (115, 500), (35, 430), (50, 335), (175, 275), (300, 220), (350, 125), (300, 25), (175, -9), (60, 10), (5, 80))]


@g('t', (0.5, 0.45))
def _t():
    return [join(line((95, 650), (95, 110)), spline((95, 110), (118, 25), (185, -8), (262, 30))), line((0, 485), (255, 492))]


@g('f', (0.55, 0.3))
def _f():
    return [join(spline((310, 675), (245, 732), (165, 728), (112, 660), (102, 540)), line((102, 540), (102, 0))), line((0, 485), (265, 492))]


@g('i')
def _i():
    return [line((0, X), (0, 0)), dot(0, 680)]


@g('j', (-1.7, 1.0))      # its tail tucks under the letter before
def _j():
    return [join(line((120, X), (120, -60)), spline((120, -60), (100, -170), (40, -230), (-40, -212))), dot(120, 680)]


@g('l', (1.0, 0.5))
def _l():
    return [join(line((0, A), (0, 110)), spline((0, 110), (20, 22), (80, -8), (140, 25)))]


@g('k', (1.0, 0.5))
def _k():
    return [line((0, A), (0, 0)), line((320, X), (22, 228), (345, 0))]


# ---------------- Cyrillic lower case ----------------
for lat, cyr in zip('aeopcyx', 'аеорсух'):
    G[cyr] = G[lat]
    SB[cyr] = SB[lat]


@g('ё', (0.62, 0.55))
def _yo():
    return _e() + [dot(122, 650), dot(292, 650)]


@g('б', ROUND)
def _be():
    return [arc(205, 250, 205, 250 + O, 95, 455), spline((372, 742), (235, 718), (105, 645), (28, 490), (1, 300), (0, 250))]


@g('в', (1.0, 0.62))
def _ve():
    return [line((0, X), (0, 0)),
            spline((0, X), (185, 503), (282, 452), (280, 338), (175, 272), (25, 266)),
            spline((25, 266), (205, 270), (322, 208), (328, 80), (218, 3), (0, 0))]


@g('г', (1.0, 0.45))
def _ge():
    return [line((0, 0), (0, X), (295, X + 6))]


@g('д', (0.45, 0.45))
def _de():
    return [join(spline((40, 0), (92, 170), (112, 360), (115, X)), line((115, X), (360, X), (360, 0))),
            line((0, -140), (0, 0), (440, 0), (440, -140))]


@g('ж', (0.45, 0.45))
def _zhe():
    return [line((250, X), (250, 0)), line((15, X), (225, 255), (0, 0)), line((485, X), (275, 255), (500, 0))]


@g('з', (0.6, 0.62))
def _ze():
    return [join(spline((30, 440), (140, 505), (268, 492), (318, 402), (262, 300), (150, 268)),
                 spline((150, 268), (292, 242), (345, 142), (300, 40), (170, -9), (20, 55)))]


@g('и')
def _ii():
    return [line((0, X), (0, 0), (360, X), (360, 0))]


@g('й')
def _iikr():
    return _ii() + [arc(180, 720, 80, 70, 200, 340)]


@g('к', (1.0, 0.5))
def _ka():
    return [line((0, X), (0, 0)), line((305, X), (22, 250), (330, 0))]


@g('л', (0.45, 1.0))
def _el():
    return [join(spline((0, 5), (58, 115), (88, 310), (95, X)), line((95, X), (345, X), (345, 0)))]


@g('м')
def _em():
    return [line((0, 0), (0, X), (235, 105), (470, X), (470, 0))]


@g('н')
def _en():
    return [line((0, X), (0, 0)), line((350, X), (350, 0)), line((0, 255), (350, 255))]


@g('п')
def _pe():
    return [line((0, 0), (0, X), (345, X), (345, 0))]


@g('т', (0.45, 0.45))
def _te():
    return [line((0, X), (360, X + 6)), line((180, X + 3), (180, 0))]


@g('ф', ROUND)
def _ef():
    return [line((235, A), (235, D)), arc(235, 250, 235, 245, 90, 450)]


@g('ц', (1.0, 0.5))
def _tse():
    return [line((0, X), (0, 0), (405, 0), (405, -140)), line((345, X), (345, 0))]


@g('ч')
def _che():
    return [spline((0, X), (4, 350), (60, 255), (185, 238), (338, 282)), line((340, X), (340, 0))]


@g('ш')
def _sha():
    return [line((0, X), (0, 0), (540, 0), (540, X)), line((270, X), (270, 0))]


@g('щ', (1.0, 0.5))
def _shcha():
    return [line((0, X), (0, 0), (600, 0), (600, -140)), line((270, X), (270, 0)), line((540, X), (540, 0))]


def soft(x0, w):
    return spline((x0, 280), (x0 + w * 0.62, 292), (x0 + w, 205), (x0 + w * 0.97, 70), (x0 + w * 0.6, 0), (x0, 0))


@g('ь', (1.0, 0.62))
def _soft():
    return [line((0, X), (0, 0)), soft(0, 295)]


@g('ъ', (0.45, 0.62))
def _hard():
    return [line((0, X), (95, X), (95, 0)), soft(95, 295)]


@g('ы')
def _yery():
    return [line((0, X), (0, 0)), soft(0, 270), line((395, X), (395, 0))]


@g('э', (0.45, 0.62))
def _e_rev():
    return [arc(195, 250, 195, 250 + O, 132, -132), line((105, 256), (388, 256))]


@g('ю', (1.0, 0.62))
def _yu():
    return [line((0, X), (0, 0)), line((0, 255), (112, 255)), arc(292, 250, 180, 250 + O, 95, 455)]


@g('я')
def _ya():
    return [line((330, X), (330, 0)), spline((330, X), (140, 505), (32, 440), (32, 330), (140, 268), (330, 264)), spline((195, 268), (80, 150), (0, 0))]


# ---------------- Latin capitals ----------------
@g('A', (0.4, 0.4))
def _A():
    return [line((0, 0), (240, CAP + 10), (480, 0)), line((95, 250), (385, 250))]


@g('C', (0.62, 0.45))
def _C():
    return [arc(290, 350, 290, 350 + O, 50, 310)]


@g('С', (0.62, 0.45))
def _Cc():
    return _C()


@g('E', (1.0, 0.5))
def _E():
    return [line((375, CAP), (0, CAP), (0, 0), (385, 0)), line((0, 362), (310, 362))]


@g('F', (1.0, 0.45))
def _F():
    return [line((360, CAP + 5), (0, CAP), (0, 0)), line((0, 380), (290, 385))]


@g('I', (0.6, 0.6))
def _I():
    return [line((0, CAP), (240, CAP)), line((120, CAP), (120, 0)), line((0, 0), (240, 0))]


@g('T', (0.4, 0.4))
def _T():
    return [line((0, CAP), (480, CAP + 6)), line((240, CAP + 3), (240, 0))]


@g('Ф', ROUND)
def _Ef():
    return [line((275, CAP + 20), (275, -20)), arc(275, 375, 275, 225, 90, 450)]


@g('Д', (0.45, 0.45))
def _De():
    return [join(spline((45, 0), (115, 260), (142, 520), (145, CAP)), line((145, CAP), (455, CAP), (455, 0))),
            line((0, -160), (0, 0), (545, 0), (545, -160))]


@g('У', (0.45, 0.45))
def _U_cyr():
    return [line((0, CAP), (302, 255)), join(line((525, CAP), (210, 55)), spline((210, 55), (160, 5), (90, -10), (30, 10)))]


@g('P', (1.0, 0.62))
def _P():
    return [line((0, 0), (0, CAP)), spline((0, CAP), (230, 702), (345, 625), (345, 505), (240, 395), (0, 385))]


@g('М', (1.0, 1.0))
def _Em():
    return [line((0, 0), (0, CAP), (285, 170), (570, CAP), (570, 0))]


@g('П', (1.0, 1.0))
def _Pe():
    return [line((0, 0), (0, CAP), (465, CAP + 5), (465, 0))]


def upper_b(x0=0):
    return spline((x0, CAP), (x0 + 230, 705), (x0 + 330, 640), (x0 + 325, 500), (x0 + 200, 388), (x0 + 20, 385))


def lower_b(x0=0, w=385, top=390):
    """The bottom bowl of Б В Ь Ъ Ы: from the stem at x0 out to x0 + w and back along the baseline."""
    return spline((x0 + 20, top), (x0 + w * 0.68, top + 5), (x0 + w, top * 0.77), (x0 + w * 0.99, top * 0.28), (x0 + w * 0.65, 3), (x0, 0))


@g('B', (1.0, 0.62))
def _B():
    return [line((0, 0), (0, CAP)), upper_b(), lower_b()]


@g('D', (1.0, 0.62))
def _D():
    return [line((0, 0), (0, CAP)), spline((0, CAP), (230, 690), (400, 560), (445, 350), (400, 140), (230, 10), (0, 0))]


@g('G', (0.62, 0.8))
def _G():
    return [join(arc(300, 350, 300, 350 + O, 50, 355), line((599, 318), (365, 330)))]


@g('H')
def _H():
    return [line((0, 0), (0, CAP)), line((440, 0), (440, CAP)), line((0, 360), (440, 366))]


@g('J', (0.5, 1.0))
def _J():
    return [join(line((330, CAP), (330, 170)), spline((330, 170), (300, 40), (180, -9), (60, 30), (0, 140)))]


@g('K', (1.0, 0.5))
def _K():
    return [line((0, 0), (0, CAP)), line((400, CAP), (22, 330), (430, 0))]


@g('L', (1.0, 0.45))
def _L():
    return [line((0, CAP), (0, 0), (380, 0))]


@g('N')
def _N():
    return [line((0, 0), (0, CAP), (440, 0), (440, CAP))]


@g('O', ROUND)
def _O():
    return [arc(320, 350, 320, 350 + O, 95, 455)]


@g('Q', (0.62, 0.62))
def _Q():
    return _O() + [line((350, 140), (570, -70))]


@g('R', (1.0, 0.5))
def _R():
    return _P() + [line((150, 392), (410, 0))]


@g('S', (0.6, 0.6))
def _S():
    return [spline((440, 600), (330, 700), (170, 707), (60, 630), (62, 490), (220, 390), (380, 300), (452, 170), (390, 40), (230, -9), (90, 20), (10, 110))]


@g('U')
def _U():
    return [join(line((0, CAP), (0, 250)), spline((0, 250), (40, 70), (220, -9), (400, 70), (440, 250)), line((440, 250), (440, CAP)))]


@g('V', (0.4, 0.4))
def _V():
    return [line((0, CAP), (250, 0), (500, CAP))]


@g('W', (0.4, 0.4))
def _W():
    return [line((0, CAP), (160, 0), (350, 560), (540, 0), (700, CAP))]


@g('X', (0.45, 0.45))
def _X():
    return [line((10, CAP), (480, 0)), line((470, CAP), (0, 0))]


@g('Y', (0.4, 0.4))
def _Y():
    return [line((0, CAP), (240, 340)), line((490, CAP), (240, 340), (240, 0))]


@g('Z', (0.6, 0.6))
def _Z():
    return [line((20, CAP), (460, CAP), (0, 0), (480, 0))]


# ---------------- Cyrillic capitals ----------------
@g('Б', (1.0, 0.62))
def _Be():
    return [line((400, CAP + 5), (0, CAP), (0, 0)), lower_b(0, 380, 420)]


@g('Г', (1.0, 0.45))
def _Ge():
    return [line((0, 0), (0, CAP), (380, CAP + 5))]


@g('Ё', (1.0, 0.5))
def _Yo():
    return _E() + [dot(110, 840), dot(280, 840)]


@g('Ж', (0.45, 0.45))
def _Zhe():
    return [line((300, CAP), (300, 0)), line((20, CAP), (270, 370), (0, 0)), line((580, CAP), (330, 370), (600, 0))]


@g('З', (0.6, 0.62))
def _Ze():
    return [join(spline((40, 610), (160, 705), (320, 690), (390, 580), (320, 430), (180, 382)),
                 spline((180, 382), (350, 352), (420, 210), (370, 50), (210, -9), (20, 70)))]


@g('И')
def _Ii():
    return [line((0, CAP), (0, 0), (440, CAP), (440, 0))]


@g('Й')
def _Iikr():
    return _Ii() + [arc(220, 850, 90, 65, 200, 340)]


@g('Л', (0.45, 1.0))
def _El():
    return [join(spline((0, 5), (80, 160), (120, 450), (125, CAP)), line((125, CAP), (440, CAP), (440, 0)))]


@g('Ц', (1.0, 0.5))
def _Tse():
    return [line((0, CAP), (0, 0), (500, 0), (500, -160)), line((430, CAP), (430, 0))]


@g('Ч')
def _Che():
    return [spline((0, CAP), (5, 480), (80, 352), (220, 332), (400, 380)), line((400, CAP), (400, 0))]


@g('Ш')
def _Sha():
    return [line((0, CAP), (0, 0), (640, 0), (640, CAP)), line((320, CAP), (320, 0))]


@g('Щ', (1.0, 0.5))
def _Shcha():
    return [line((0, CAP), (0, 0), (710, 0), (710, -160)), line((320, CAP), (320, 0)), line((640, CAP), (640, 0))]


@g('Ь', (1.0, 0.62))
def _Soft():
    return [line((0, CAP), (0, 0)), lower_b(0, 360, 405)]


@g('Ъ', (0.45, 0.62))
def _Hard():
    return [line((0, CAP), (110, CAP), (110, 0)), lower_b(110, 350, 405)]


@g('Ы')
def _Yery():
    return [line((0, CAP), (0, 0)), lower_b(0, 330, 405), line((480, CAP), (480, 0))]


@g('Э', (0.45, 0.62))
def _E_rev():
    return [arc(270, 350, 280, 350 + O, 130, -130), line((140, 356), (545, 356))]


@g('Ю', (1.0, 0.62))
def _Yu():
    return [line((0, CAP), (0, 0)), line((0, 360), (130, 360)), arc(350, 350, 220, 350 + O, 95, 455)]


@g('Я')
def _Ya():
    return [line((400, CAP), (400, 0)), spline((400, CAP), (170, 705), (40, 620), (40, 480), (170, 395), (400, 390)),
            spline((230, 395), (100, 210), (0, 0))]


# the same letter in two alphabets is drawn once (the hand still bends each on its own)
for lat, cyr in zip('ABEKHMOPCTX', 'АВЕКНМОРСТХ'):
    if lat in G and cyr not in G:
        G[cyr], SB[cyr] = G[lat], SB[lat]
    elif cyr in G and lat not in G:
        G[lat], SB[lat] = G[cyr], SB[cyr]


# ---------------- digits: tabular ----------------
DIGW = 340


def _dg(c):
    TAB.add(c)
    return g(c)


@_dg('0')
def _0():
    return [arc(170, DIG / 2, 170, DIG / 2 + O, 95, 455)]


@_dg('1')
def _1():
    return [line((50, 545), (215, DIG), (215, 0))]


@_dg('2')
def _2():
    return [join(spline((25, 545), (100, 660), (210, DIG + 5), (305, 625), (318, 505), (250, 385), (120, 220), (5, 5)), line((5, 5), (345, 0)))]


@_dg('3')
def _3():
    return [join(spline((30, 600), (140, DIG + 5), (268, 672), (320, 572), (268, 452), (150, 405)),
                 spline((150, 405), (300, 372), (350, 240), (318, 88), (190, -9), (20, 60)))]


@_dg('4')
def _4():
    return [line((215, DIG), (0, 215), (350, 215)), line((275, 470), (275, 0))]


@_dg('5')
def _5():
    return [join(line((312, DIG), (62, DIG), (40, 405)),
                 spline((40, 405), (160, 448), (292, 418), (348, 298), (322, 115), (200, -9), (28, 42)))]


@_dg('6')
def _6():
    return [spline((292, 672), (185, DIG + 5), (72, 620), (12, 430), (12, 235), (62, 55), (172, -9), (292, 40), (342, 190),
                   (292, 332), (172, 372), (62, 330), (14, 240))]


@_dg('7')
def _7():
    return [line((0, DIG), (340, DIG), (115, 0))]


@_dg('8')
def _8():
    return [arc(170, 525, 132, 165, 90, 450), arc(170, 192, 165, 202, 90, 450)]


@_dg('9')
def _9():
    return [arc(168, 470, 158, 222, 0, 360), join(line((326, 470), (322, 250)), spline((322, 250), (278, 70), (168, -9), (48, 40)))]


# ---------------- punctuation, math, pictograms ----------------
@g(' ')
def _space():
    return []


@g('.', (0.8, 0.8))
def _period():
    return [dot(0, 30)]


@g(',', (0.8, 0.8))
def _comma():
    return [spline((40, 45), (32, -30), (0, -110))]


@g(':', (0.8, 0.8))
def _colon():
    return [dot(0, 30), dot(0, 330)]


@g('!', (0.8, 0.8))
def _excl():
    return [line((0, CAP), (0, 210)), dot(0, 30)]


@g('?', (0.6, 0.6))
def _q_mark():
    return [spline((10, 560), (90, 675), (200, 705), (300, 640), (310, 520), (220, 420), (160, 330), (158, 215)), dot(158, 30)]


@g('%', (0.5, 0.5))
def _pct():
    return [arc(95, 555, 95, 135, 90, 450), arc(395, 145, 95, 135, 90, 450), line((40, 0), (450, DIG))]


@g('+', (0.7, 0.7))
def _plus():
    return [line((0, AX), (340, AX)), line((170, AX - 170), (170, AX + 170))]


@g('=', (0.7, 0.7))
def _eq():
    return [line((0, AX + 85), (340, AX + 85)), line((0, AX - 85), (340, AX - 85))]


@g('-', (0.7, 0.7))
def _hyph():
    return [line((0, 260), (220, 260))]


@g('–', (0.5, 0.5))
def _endash():
    return [line((0, 260), (440, 260))]


@g('—', (0.3, 0.3))
def _emdash():
    return [line((0, 260), (760, 260))]


@g('·', (0.9, 0.9))
def _mdot():
    return [dot(0, AX)]


@g('×', (0.7, 0.7))
def _times():
    return [line((0, AX - 150), (300, AX + 150)), line((300, AX - 150), (0, AX + 150))]


@g('→', (0.6, 0.6))
def _arrow():
    return [line((0, AX), (560, AX)), line((410, AX + 140), (560, AX), (410, AX - 140))]


@g('(', (0.8, 0.4))
def _lpar():
    return [spline((175, 770), (45, 570), (0, 270), (45, -40), (175, -240))]


@g(')', (0.4, 0.8))
def _rpar():
    return [spline((0, 770), (130, 570), (175, 270), (130, -40), (0, -240))]


@g('½', (0.5, 0.5))
def _half():
    return xf(_1(), 0.46, 0.46, 0, 330) + xf(_2(), 0.46, 0.46, 330, -10) + [line((110, -10), (470, 700))]


@g('²', (0.5, 0.5))
def _sq():
    return xf(_2(), 0.5, 0.5, 0, 380)


def star_pts():
    pts = []
    for i in range(10):
        r = 300 if i % 2 == 0 else 125
        a = math.radians(90 + i * 36)
        pts.append((300 + r * math.cos(a), 300 + r * math.sin(a)))
    return pts


@g('★', (0.5, 0.5))
def _star():
    return [fill(*star_pts())]


@g('☆', (0.5, 0.5))
def _star_open():
    p = star_pts()
    return [line(*(p + [p[0]]))]


@g('✓', (0.5, 0.5))
def _check():
    return [line((0, 300), (150, 60), (430, 620))]


# ---------------- Greek (what the formulas use) ----------------
@g('ρ', (1.0, 0.62))
def _rho():
    return [line((0, D), (0, 250)), arc(190, 250, 190, 250 + O, 180, 540)]


@g('μ')
def _mu():
    return [line((0, X), (0, D)), spline((0, 230), (40, 70), (160, -9), (290, 30), (362, 170)), line((366, X), (366, 0))]


@g('Δ', (0.4, 0.4))
def _Delta():
    return [line((0, 0), (270, CAP + 10), (540, 0), (0, 0))]


# ---------------- the rest of ASCII ----------------
@g(';', (0.8, 0.8))
def _semi():
    return [dot(30, 330)] + _comma()


@g("'", (0.8, 0.8))
def _apos():
    return [line((5, 730), (0, 560))]


@g('"', (0.8, 0.8))
def _quot():
    return [line((5, 730), (0, 560)), line((135, 730), (130, 560))]


@g('`', (0.8, 0.8))
def _grave():
    return [line((0, 740), (90, 620))]


@g('#', (0.5, 0.5))
def _hash():
    return [line((150, 690), (100, 0)), line((330, 690), (280, 0)), line((10, 470), (430, 470)), line((0, 225), (410, 225))]


@g('$', (0.6, 0.6))
def _dollar():
    return xf(_S(), 0.78, 0.88, 0, 30) + [line((175, 760), (170, -80))]


@g('&', (0.5, 0.5))
def _amp():
    return [spline((510, 0), (330, 210), (200, 380), (150, 515), (190, 650), (290, 695), (362, 612), (330, 500), (230, 420), (100, 320),
                   (40, 180), (100, 40), (230, -9), (370, 40), (470, 180), (520, 300))]


@g('*', (0.6, 0.6))
def _ast():
    c, r = (150, 560), 140
    return [line((c[0] + r * math.cos(math.radians(a)), c[1] + r * math.sin(math.radians(a))),
                 (c[0] - r * math.cos(math.radians(a)), c[1] - r * math.sin(math.radians(a)))) for a in (90, 30, -30)]


@g('/', (0.5, 0.5))
def _slash():
    return [line((0, -60), (330, 740))]


@g(chr(92), (0.5, 0.5))                 # backslash
def _bslash():
    return [line((0, 740), (330, -60))]


@g('|', (0.9, 0.9))
def _bar():
    return [line((0, 770), (0, -230))]


@g('[', (0.9, 0.4))
def _lbr():
    return [line((150, 770), (0, 770), (0, -230), (150, -230))]


@g(']', (0.4, 0.9))
def _rbr():
    return [line((0, 770), (150, 770), (150, -230), (0, -230))]


@g('{', (0.8, 0.4))
def _lbrace():
    return [join(spline((200, 770), (110, 740), (100, 560), (95, 360), (10, 270)), spline((10, 270), (95, 180), (100, -20), (110, -200), (200, -235)))]


@g('}', (0.4, 0.8))
def _rbrace():
    return xf(_lbrace(), -1, 1, 200, 0)


@g('<', (0.6, 0.6))
def _lt():
    return [line((330, AX + 190), (0, AX), (330, AX - 190))]


@g('>', (0.6, 0.6))
def _gt():
    return [line((0, AX + 190), (330, AX), (0, AX - 190))]


@g('^', (0.6, 0.6))
def _caret():
    return [line((0, 480), (150, 700), (300, 480))]


@g('_', (0.3, 0.3))
def _under():
    return [line((0, -130), (460, -130))]


@g('~', (0.6, 0.6))
def _tilde():
    return [spline((0, AX - 20), (90, AX + 60), (200, AX), (310, AX - 60), (400, AX + 20))]


@g('@', (0.5, 0.5))
def _at():
    return [arc(300, 330, 110, 130, 0, 360),
            spline((410, 450), (410, 250), (480, 200), (560, 260), (590, 380), (540, 560), (400, 680), (250, 690), (110, 610), (40, 440),
                   (60, 230), (170, 90), (330, 50), (470, 90))]


# ---------------- typography ----------------
@g('«', (0.5, 0.5))
def _laquo():
    return [line((140, AX + 125), (0, AX), (140, AX - 125)), line((300, AX + 125), (160, AX), (300, AX - 125))]


@g('»', (0.5, 0.5))
def _raquo():
    return xf(_laquo(), -1, 1, 300, 0)


@g('‹', (0.5, 0.5))
def _lsaquo():
    return [line((140, AX + 125), (0, AX), (140, AX - 125))]


@g('›', (0.5, 0.5))
def _rsaquo():
    return [line((0, AX + 125), (140, AX), (0, AX - 125))]


def _rq(x=0, y=0):                      # ’ a comma hung at the top
    return [spline((x + 40, y + 730), (x + 34, y + 650), (x, y + 575))]


def _lq(x=0, y=0):                      # ‘ the same turned round
    return [spline((x, y + 575), (x + 6, y + 655), (x + 40, y + 730))]


@g('’', (0.8, 0.8))
def _rsquo():
    return _rq()


@g('‘', (0.8, 0.8))
def _lsquo():
    return _lq()


@g('”', (0.8, 0.8))
def _rdquo():
    return _rq() + _rq(130)


@g('“', (0.8, 0.8))
def _ldquo():
    return _lq() + _lq(130)


@g('„', (0.8, 0.8))
def _bdquo():
    return _rq(0, -690) + _rq(130, -690)


@g('…', (0.8, 0.8))
def _hellip():
    return [dot(0, 30), dot(190, 30), dot(380, 30)]


@g('•', (0.8, 0.8))
def _bull():
    return [Fill(arc(0, AX, 45, 45, 90, 450))]


@g('№', (1.0, 0.5))
def _numero():
    return xf(_N(), 0.95, 1, 0, 0) + [arc(560, 520, 75, 100, 90, 450), line((480, 330), (640, 330))]


@g('°', (0.6, 0.6))
def _deg():
    return [arc(80, 620, 80, 80, 90, 450)]


@g('©', ROUND)
def _copy():
    return [arc(320, 350, 320, 350 + O, 95, 455), arc(330, 350, 130, 150, 50, 310)]


@g('§', (0.6, 0.6))
def _sect():
    return [spline((300, 640), (200, 705), (80, 660), (80, 560), (210, 470), (320, 390), (320, 280), (200, 220)),
            spline((110, 480), (0, 400), (10, 290), (130, 200), (250, 130), (250, 30), (130, -20), (20, 40))]


# ---------------- math ----------------
@g('−', (0.7, 0.7))
def _minus():
    return [line((0, AX), (340, AX))]


@g('±', (0.7, 0.7))
def _plusminus():
    return [line((0, AX + 60), (340, AX + 60)), line((170, AX - 90), (170, AX + 210)), line((0, AX - 170), (340, AX - 170))]


@g('÷', (0.7, 0.7))
def _divide():
    return [line((0, AX), (340, AX)), dot(170, AX + 150), dot(170, AX - 150)]


@g('≈', (0.6, 0.6))
def _approx():
    return xf(_tilde(), 0.9, 1, 0, 85) + xf(_tilde(), 0.9, 1, 0, -85)


@g('≠', (0.7, 0.7))
def _ne():
    return _eq() + [line((90, AX - 190), (250, AX + 190))]


@g('≤', (0.6, 0.6))
def _le():
    return [line((330, AX + 230), (0, AX + 70), (330, AX - 90)), line((0, AX - 190), (330, AX - 190))]


@g('≥', (0.6, 0.6))
def _ge_():
    return [line((0, AX + 230), (330, AX + 70), (0, AX - 90)), line((0, AX - 190), (330, AX - 190))]


@g('√', (0.5, 0.3))
def _sqrt():
    return [line((0, 330), (70, 370), (180, -10), (330, 740), (520, 740))]


@g('∞', (0.5, 0.5))
def _infty():
    pts = []
    for i in range(16):
        t = 2 * math.pi * i / 16
        d = 1 + math.sin(t) ** 2
        pts.append((300 + 300 * math.cos(t) / d, AX + 360 * math.sin(t) * math.cos(t) / d))
    return [spline(*pts, closed=True)]


@g('¹', (0.5, 0.5))
def _sup1():
    return xf(_1(), 0.5, 0.5, 0, 380)


@g('³', (0.5, 0.5))
def _sup3():
    return xf(_3(), 0.5, 0.5, 0, 380)


@g('¼', (0.5, 0.5))
def _quarter():
    return xf(_1(), 0.46, 0.46, 0, 330) + xf(_4(), 0.46, 0.46, 330, -10) + [line((110, -10), (470, 700))]


@g('¾', (0.5, 0.5))
def _3quarters():
    return xf(_3(), 0.46, 0.46, 0, 330) + xf(_4(), 0.46, 0.46, 330, -10) + [line((130, -10), (490, 700))]


# ---------------- arrows and shapes ----------------
@g('←', (0.6, 0.6))
def _larrow():
    return xf(_arrow(), -1, 1, 560, 0)


@g('↑', (0.6, 0.6))
def _uarrow():
    return [line((150, -20), (150, 700)), line((0, 550), (150, 700), (300, 550))]


@g('↓', (0.6, 0.6))
def _darrow():
    return [line((150, 700), (150, -20)), line((0, 130), (150, -20), (300, 130))]


@g('↔', (0.6, 0.6))
def _lrarrow():
    return [line((0, AX), (640, AX)), line((490, AX + 140), (640, AX), (490, AX - 140)), line((150, AX + 140), (0, AX), (150, AX - 140))]


@g('▶', (0.6, 0.6))
def _play():
    return [fill((0, AX - 250), (0, AX + 250), (430, AX))]


@g('►', (0.6, 0.6))
def _pointer():
    return [fill((0, AX - 190), (0, AX + 190), (380, AX))]


@g('◀', (0.6, 0.6))
def _play_l():
    return [fill((430, AX - 250), (430, AX + 250), (0, AX))]


@g('◄', (0.6, 0.6))
def _pointer_l():
    return [fill((380, AX - 190), (380, AX + 190), (0, AX))]


@g('▲', (0.6, 0.6))
def _up_tri():
    return [fill((0, AX - 200), (460, AX - 200), (230, AX + 220))]


@g('▼', (0.6, 0.6))
def _down_tri():
    return [fill((0, AX + 200), (460, AX + 200), (230, AX - 220))]


@g('✗', (0.5, 0.5))
def _cross():
    return [spline((20, 600), (200, 310), (400, 30)), spline((380, 620), (210, 340), (0, 40))]


@g('✔', (0.5, 0.5))
def _check_heavy():
    return _check()


@g('⚙', ROUND)
def _gear():
    c, pts = (300, 330), []
    for i in range(8):
        a = i * 45
        for r, da in ((215, -16), (285, -9), (285, 9), (215, 16)):
            t = math.radians(a + da)
            pts.append((c[0] + r * math.cos(t), c[1] + r * math.sin(t)))
    return [line(*(pts + [pts[0]])), arc(c[0], c[1], 80, 80, 90, 450)]


@g('⚗', (0.6, 0.6))
def _flask():
    return [line((215, 720), (385, 720)),
            join(line((240, 720), (240, 470)), spline((240, 470), (100, 390), (45, 220), (110, 50), (300, -9), (490, 50), (555, 220), (500, 390), (360, 470)),
                 line((360, 470), (360, 720))),
            spline((95, 250), (200, 285), (300, 240), (400, 200), (505, 245)), dot(250, 130), dot(360, 170)]


# ---------------- money ----------------
@g('¤', (0.6, 0.6))
def _curr():
    c = (230, AX)
    return [arc(c[0], c[1], 150, 150, 90, 450)] + [line((c[0] + 150 * math.cos(math.radians(a)), c[1] + 150 * math.sin(math.radians(a))),
                                                        (c[0] + 250 * math.cos(math.radians(a)), c[1] + 250 * math.sin(math.radians(a)))) for a in (45, 135, 225, 315)]


@g('€', (0.62, 0.45))
def _euro():
    return xf(_C(), 0.9, 1, 60, 0) + [line((0, 420), (380, 420)), line((0, 280), (350, 280))]


@g('₽', (0.4, 0.62))
def _ruble():
    return [line((90, 0), (90, CAP)), spline((90, CAP), (320, 702), (435, 625), (435, 505), (330, 395), (90, 385)), line((0, 200), (330, 200))]


# ---------------- Greek, the rest ----------------
@g('α', (0.62, 0.8))
def _alpha():
    return [arc(185, 250, 185, 250 + O, 35, 385), spline((385, 515), (365, 300), (372, 100), (405, 15), (455, 0))]


@g('β', (1.0, 0.62))
def _beta():
    return [line((0, D), (0, 560)),
            join(spline((0, 560), (40, 690), (150, 740), (260, 700), (290, 600), (240, 500), (120, 462)),
                 spline((120, 462), (280, 440), (340, 320), (330, 150), (230, 40), (100, 20), (0, 70)))]


@g('γ', (0.45, 0.45))
def _gamma():
    return [spline((0, 470), (70, 500), (130, 410), (200, 160), (215, -60), (200, D + 10)), line((400, X), (205, 40))]


@g('δ', ROUND)
def _delta():
    return [arc(190, 230, 190, 240, 90, 450), spline((190, 470), (90, 560), (110, 660), (250, 725), (360, 700))]


@g('ε', (0.62, 0.62))
def _eps():
    return [spline((330, 440), (230, 505), (100, 490), (50, 395), (130, 285), (250, 265)),
            spline((250, 265), (110, 250), (30, 160), (60, 40), (190, -9), (345, 45))]


@g('ζ', (0.6, 0.5))
def _zeta():
    return [spline((60, 730), (320, 735), (120, 500), (20, 300), (40, 100), (200, 20), (290, -60), (230, D))]


@g('η')
def _eta():
    return [line((0, X), (0, 0)), join(spline((0, 330), (80, 462), (200, 508), (318, 458), (365, 330), (365, 200)), line((365, 200), (365, D)))]


@g('θ', ROUND)
def _theta():
    return [arc(200, 365, 200, 375, 90, 450), line((0, 365), (400, 365))]


@g('ι', (1.0, 0.5))
def _iota():
    return [join(line((0, X), (0, 90)), spline((0, 90), (20, 15), (80, -8), (130, 25)))]


@g('κ', (1.0, 0.5))
def _kappa():
    return [line((0, X), (0, 0)), line((300, X), (22, 250), (320, 0))]


@g('λ', (0.45, 0.45))
def _lambda():
    return [line((30, 730), (100, 700), (390, 0)), line((210, 380), (0, 0))]


@g('ν', (0.45, 0.6))
def _nu():
    return [line((0, X), (180, 0)), spline((180, 0), (300, 200), (370, 400), (360, 510))]


@g('ξ', (0.6, 0.5))
def _xi():
    return [line((60, 730), (300, 735)),
            spline((280, 735), (90, 680), (60, 560), (200, 470)),
            spline((200, 470), (40, 390), (20, 200), (150, 60), (290, -40), (230, D))]


@g('π', (0.5, 0.5))
def _pi():
    return [line((0, X), (420, X + 5)), line((110, X), (100, 0)), join(line((300, X), (300, 90)), spline((300, 90), (320, 15), (380, 0)))]


@g('σ', (0.62, 0.4))
def _sigma():
    return [arc(190, 240, 190, 255, 90, 450), line((190, 495), (430, 500))]


@g('ς', (0.62, 0.5))
def _sigma_final():
    return [spline((330, 470), (200, 505), (60, 430), (30, 250), (120, 110), (260, 40), (290, -80), (220, -200))]


@g('τ', (0.5, 0.45))
def _tau():
    return [line((0, X), (380, X + 5)), join(line((190, X), (190, 90)), spline((190, 90), (210, 15), (270, -8), (320, 25)))]


@g('υ')
def _upsilon():
    return [spline((0, X), (0, 250), (40, 70), (180, -9), (320, 70), (370, 300), (360, X))]


@g('χ', (0.5, 0.5))
def _chi():
    return [line((0, X), (360, D)), line((360, X), (0, D))]


@g('ψ')
def _psi():
    return [spline((0, X), (0, 250), (60, 80), (220, 20), (380, 80), (440, 250), (440, X)), line((220, 730), (220, D))]


@g('ω')
def _omega():
    return [spline((90, X), (20, 330), (40, 90), (140, -9), (230, 80), (245, 260)),
            spline((245, 260), (255, 80), (350, -9), (450, 90), (470, 330), (400, X))]


@g('Γ', (1.0, 0.45))
def _Gamma():
    return [line((0, 0), (0, CAP), (380, CAP + 5))]


@g('Θ', ROUND)
def _Theta():
    return _O() + [line((150, 350), (490, 350))]


@g('Λ', (0.4, 0.4))
def _Lambda():
    return [line((0, 0), (260, CAP + 10), (520, 0))]


@g('Ξ', (0.6, 0.6))
def _Xi():
    return [line((20, CAP), (440, CAP)), line((70, 360), (390, 360)), line((0, 0), (460, 0))]


@g('Π')
def _Pi():
    return _Pe()


@g('Σ', (0.6, 0.6))
def _Sigma():
    return [line((440, CAP), (20, CAP), (260, 350), (0, 0), (460, 0))]


@g('Ψ')
def _Psi():
    return [spline((0, CAP), (0, 450), (80, 250), (270, 200), (460, 250), (540, 450), (540, CAP)), line((270, CAP), (270, 0))]


@g('Ω', (0.5, 0.5))
def _Omega():
    return [join(line((0, 5), (190, 5)), spline((190, 5), (170, 110), (60, 270), (40, 460), (130, 640), (300, 712), (470, 640), (560, 460), (540, 270),
                                             (430, 110), (410, 5)), line((410, 5), (600, 5)))]


# Greek capitals that are Latin letters, and code points that are the same sign under another name
for a, b in {'Α': 'A', 'Β': 'B', 'Ε': 'E', 'Ζ': 'Z', 'Η': 'H', 'Ι': 'I', 'Κ': 'K', 'Μ': 'M', 'Ν': 'N', 'Ο': 'O', 'Ρ': 'P', 'Τ': 'T',
             'Υ': 'Y', 'Χ': 'X', 'Φ': 'Ф', 'φ': 'ф', 'ο': 'o', 'µ': 'μ', '∆': 'Δ', 'Ω': 'Ω', '⋅': '·', '∙': '·'}.items():
    if a not in G:
        G[a], SB[a] = G[b], SB[b]

# spaces: the ordinary one, the unbreakable one number formatting uses, thin ones, and one as wide as a digit
SPACES = {' ': 260, ' ': 260, ' ': 130, ' ': 130, ' ': 'digit'}
for c in SPACES:
    G[c], SB[c] = _space, (1.0, 1.0)
