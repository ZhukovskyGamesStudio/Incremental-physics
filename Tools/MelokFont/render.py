# Renders Melok through FreeType (as Unity does) on the board colour: every glyph, and sample lines.
#   python render.py [font.ttf] [out.png]          needs: pip install pillow
import os, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import glyphs as GL
import build as B

BOARD = (27, 36, 32)
CHALK = (237, 237, 227)
DIM = (140, 150, 144)
YELLOW = (255, 214, 82)
CYAN = (115, 219, 255)
BASIC = ImageFont.Layout.BASIC

LINES = [
    (118, [('Физика  Incremental', CHALK)]),
    (52, [('Съешь же ещё этих мягких французских булок, да выпей чаю.', CHALK)]),
    (52, [('СЪЕШЬ ЖЕ ЕЩЁ ЭТИХ МЯГКИХ ФРАНЦУЗСКИХ БУЛОК!', CHALK)]),
    (52, [('The quick brown fox jumps over the lazy dog. THE FIVE BOXING WIZARDS', CHALK)]),
    (52, [('F = m·g    A = F·s    E = ½mv²    ', CHALK), ('ρ, μ, Δt, η, λ, ω, Ψ', CYAN), (' → ', CHALK), ('42 Дж', YELLOW)]),
    (34, [('▶ Эксперимент 3 · действий: 8 · ', CHALK), ('+36 идей', YELLOW), (' · ⚗ Теории · ⚙ ★ ✓ · 0123456789 · «кавычки» — 3–5 ч…', CHALK)]),
    (24, [('24 px: Маятник — период растёт с длиной нити. Пружина: +12 % к энергии за уровень. T = 2π·√(l/g) ≈ 1,4 с', CHALK)]),
]


def board(w, h):
    base = Image.new('RGB', (w, h), BOARD)
    noise = Image.effect_noise((w // 8 + 1, h // 8 + 1), 40).resize((w, h), Image.BICUBIC).filter(ImageFilter.GaussianBlur(18))
    img = Image.composite(Image.new('RGB', (w, h), (36, 46, 41)), base, noise.point(lambda v: max(0, (v - 128)) * 2))
    return Image.blend(img, Image.merge('RGB', [Image.effect_noise((w, h), 12)] * 3), 0.025)


def specimen(ttf, out, W=1800):
    chars = [c for c in GL.G if c not in GL.SPACES]
    size, cols = 80, 22
    cw, ch = (W - 40) // cols, int(size * 1.45)
    rows = (len(chars) + cols - 1) // cols
    top = sum(int(s * 1.4) for s, _ in LINES) + 60
    img = board(W, top + rows * ch + 40)
    d = ImageDraw.Draw(img)
    y = 30
    for s, parts in LINES:
        f = ImageFont.truetype(ttf, s, layout_engine=BASIC)
        x = 40
        for text, col in parts:
            d.text((x, y), text, font=f, fill=col)
            x += d.textlength(text, font=f)
        y += int(s * 1.4)
    f = ImageFont.truetype(ttf, size, layout_engine=BASIC)
    for i, c in enumerate(chars):
        x, yy = 20 + (i % cols) * cw, top + (i // cols) * ch
        base = yy + int(size * 1.05)
        d.line([(x, base), (x + cw - 8, base)], fill=(52, 64, 58))
        d.text((x + 8, base), c, font=f, fill=CHALK, anchor='ls')
    img.save(out)
    return out


if __name__ == '__main__':
    ttf = sys.argv[1] if len(sys.argv) > 1 else B.OUT
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(os.path.abspath(__file__)), 'specimen.png')
    print(specimen(ttf, out))
