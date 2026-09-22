# The itch.io page art, put together from what the game drew (Build/ItchArt, see Assets/Editor/ItchArt.cs):
# the logo and the buddy with their alpha (matte.py), the board texture, and frames of real play.
#   python compose.py [out_dir]
# The raw pictures, in play mode: ItchArt.Begin(1920, 1080) → Snap("shot_menu") and Board(); Begin(3840, 2160) →
# Solo("logo", 2) / Solo("buddy:Happy", 4), each snapped on Background(false) as name_k and Background(true) as
# name_w, then `python matte.py name`; with the AutoPlayBot on, Record("rec", 300, 10) early and Record("r2", 420,
# 12) late in the game, and Snap("t_alchemy") on GameRoot.I.ShowAlchemy(). The frames picked below (r2_322,
# rec_144, ...) belong to that one recording: a new recording needs new picks.
import os, sys
import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, '..', '..', 'Build', 'ItchArt')
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(RAW, 'page')
BOARD = np.array([27, 36, 32], np.float32)          # ChalkTex.BoardColor


def raw(name):
    return Image.open(os.path.join(RAW, name + '.png')).convert('RGBA')


def board(w, h, seed=0):
    """The board as the game shows it: its 512 px texture stretched over a 1920 px screen."""
    tile = Image.open(os.path.join(RAW, 'board_tile.png')).convert('RGB')
    big = Image.new('RGB', (1536, 1536))
    for y in range(3):
        for x in range(3):
            big.paste(tile, (x * 512, y * 512))
    k = 3.75                                        # 1920 / 512
    big = big.resize((int(1536 * k), int(1536 * k)), Image.BICUBIC)
    ox, oy = 512 * k + seed * 97 % 400, 512 * k + seed * 53 % 300
    out = big.crop((int(ox), int(oy), int(ox) + w, int(oy) + h))
    if out.size != (w, h):
        out = out.resize((w, h))
    return out.convert('RGBA')


def key(img, knee=10.0):
    """Lifts chalk off the board: whatever is brighter than the board becomes the drawing, with its alpha."""
    c = np.asarray(img.convert('RGB')).astype(np.float32)
    m = c.max(axis=2)
    a = np.clip((m - BOARD.max() - knee) / (235.0 - BOARD.max() - knee), 0, 1)
    f = np.where(a[..., None] > 1e-3, (c - BOARD * (1 - a[..., None])) / np.maximum(a[..., None], 1e-3), 0)
    return Image.fromarray(np.dstack([np.clip(f, 0, 255), a * 255]).astype(np.uint8), 'RGBA')


def fade(img, left=0, right=0, top=0, bottom=0):
    """Soft edges, so a lifted piece dissolves into the board."""
    a = np.asarray(img.getchannel('A')).astype(np.float32)
    h, w = a.shape
    x, y = np.arange(w)[None, :], np.arange(h)[:, None]
    m = np.ones((h, w), np.float32)
    if left: m *= np.clip(x / left, 0, 1)
    if right: m *= np.clip((w - 1 - x) / right, 0, 1)
    if top: m *= np.clip(y / top, 0, 1)
    if bottom: m *= np.clip((h - 1 - y) / bottom, 0, 1)
    out = img.copy()
    out.putalpha(Image.fromarray((a * m).astype(np.uint8)))
    return out


def solid(img, wall=0.35):
    """The buddy is an outline: fill what it encloses with the board, so nothing shows through him."""
    from PIL import ImageDraw
    a = np.asarray(img.getchannel('A')) > wall * 255
    walls = Image.fromarray(np.where(a, 255, 0).astype(np.uint8))
    ImageDraw.floodfill(walls, (0, 0), 128)                 # everything reachable from outside
    inside = np.asarray(walls) == 0
    px = np.asarray(img).copy()
    px[inside] = [BOARD[0], BOARD[1], BOARD[2], 255]
    return Image.fromarray(px, 'RGBA')


def fit(img, w=None, h=None):
    if w and not h: h = round(img.height * w / img.width)
    if h and not w: w = round(img.width * h / img.height)
    return img.resize((w, h), Image.LANCZOS)


def put(canvas, img, x, y):
    canvas.alpha_composite(img, (int(x), int(y)))


def save(img, name, size=None, jpg=False):
    os.makedirs(OUT, exist_ok=True)
    if size:
        img = img.resize(size, Image.LANCZOS)
    img = img.convert('RGB')
    path = os.path.join(OUT, name)
    if jpg:
        img.save(path, quality=92, subsampling=0, optimize=True)
    else:
        img.save(path, optimize=True)
    print(f'{name:28} {img.size[0]}x{img.size[1]}  {os.path.getsize(path) // 1024} KB')
    return path


# ---------------------------------------------------------------------------------------------------------------
def cover():
    """630x500, the picture in every listing: the title over the machine at work, the buddy cheering."""
    W, H = 1260, 1000                               # drawn at 2x, saved at 630x500
    c = board(W, H, 1)
    bench = key(raw('r2_322').crop((560, 380, 1450, 740)))      # spring, dynamometer, table, pendulum, record
    bench = fade(fit(bench, w=1260), left=60, right=60, top=30)
    put(c, bench, 0, H - bench.height - 18)
    logo = fit(raw('logo'), w=1060)
    put(c, logo, 20, 50)
    buddy = solid(fit(raw('buddy'), h=240))               # cheering where the arrow of "Incremental" points
    put(c, buddy, 1098, 96)
    save(c, 'cover_630x500.png', (630, 500))
    save(c, 'cover_1260x1000.png')


def banner():
    """The page header, 960 wide: the title on the board, the buddy beside it, the instruments faint around."""
    W, H = 1920, 560                                # drawn at 2x, saved at 960x280
    c = board(W, H, 2)
    menu = raw('shot_menu')
    for box, at in (((120, 190, 300, 310), (40, 70)), ((1640, 130, 1760, 270), (1780, 40)),
                    ((220, 400, 400, 470), (90, 400)), ((1520, 395, 1720, 465), (300, 480)),
                    ((215, 770, 300, 900), (1800, 340))):
        put(c, key(menu.crop(box)), *at)
    logo = fit(raw('logo'), h=440)
    put(c, logo, (W - logo.width) // 2 - 110, 50)
    buddy = solid(fit(raw('buddy'), h=330))
    put(c, buddy, (W + logo.width) // 2 - 40, 150)
    save(c, 'banner_960x280.png', (960, 280))
    save(c, 'banner_1920x560.png')


def background():
    """The page background: the board, tileable (the texture is seamless), at the game's grain."""
    tile = Image.open(os.path.join(RAW, 'board_tile.png')).convert('RGB')
    big = Image.new('RGB', (1536, 1536))
    for y in range(3):
        for x in range(3):
            big.paste(tile, (x * 512, y * 512))
    big = big.resize((3072, 3072), Image.BICUBIC).crop((1024, 1024, 2048, 2048))   # 2x, still seamless
    save(big.convert('RGBA'), 'background_tile_1024.png')


def screenshots():
    # recorded frame -> what it shows
    for i, (name, what) in enumerate((('r2_322', 'machine'), ('rec_144', 'record'), ('r2_063', 'laboratory'),
                                      ('t_alchemy', 'theories'), ('r2_413', 'results'), ('shot_menu', 'menu')), 1):
        save(raw(name), f'screenshot_{i}_{what}.png')


def logo():
    img = raw('logo')
    os.makedirs(OUT, exist_ok=True)
    for w in (2066, 960):
        fit(img, w=w).save(os.path.join(OUT, f'logo_transparent_{w}.png'), optimize=True)
        print(f'logo_transparent_{w}.png')


def gif(first=230, last=330, width=960, name='gameplay.gif'):
    """The machine at work, as a looping GIF: frames of the recording, the board flattened so it packs small."""
    frames = []
    for i in range(first, last):
        f = Image.open(os.path.join(RAW, f'r2_{i:03d}.png')).convert('RGB')
        f = f.crop((0, 300, 1920, 900))                          # the bench, without the HUD and the button
        f = f.resize((width, round(600 * width / 1920)), Image.LANCZOS)
        a = np.asarray(f).astype(np.float32)
        d = np.abs(a - BOARD).max(axis=2)
        a[d < 14] = BOARD                                        # the board's dust would cost megabytes
        frames.append(Image.fromarray(a.astype(np.uint8)))
    pal = frames[len(frames) // 2].quantize(colors=48, method=Image.Quantize.MEDIANCUT)
    q = [f.quantize(palette=pal, dither=Image.Dither.NONE) for f in frames]
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name)
    q[0].save(path, save_all=True, append_images=q[1:], duration=round(1000 / 12), loop=0, optimize=True, disposal=1)
    print(f'{name:28} {q[0].size[0]}x{q[0].size[1]}  {len(q)} frames  {os.path.getsize(path) // 1024} KB')


if __name__ == '__main__':
    cover(); banner(); background(); screenshots(); logo(); gif()
