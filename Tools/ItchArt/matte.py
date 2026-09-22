# The same still rendered on black and on white gives its alpha: a = 1 - (white - black), colour = black / a.
#   python matte.py name [pad]    reads Build/ItchArt/name_k.png and name_w.png, writes name.png cropped to the art
import os, sys
import numpy as np
from PIL import Image

RAW = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Build', 'ItchArt')


def matte(name, pad=24):
    k = np.asarray(Image.open(os.path.join(RAW, name + '_k.png')).convert('RGB')).astype(np.float32) / 255
    w = np.asarray(Image.open(os.path.join(RAW, name + '_w.png')).convert('RGB')).astype(np.float32) / 255
    a = np.clip(1 - (w - k).mean(axis=2), 0, 1)
    rgb = np.where(a[..., None] > 1e-3, k / np.maximum(a[..., None], 1e-3), 0)
    out = np.dstack([np.clip(rgb, 0, 1), a])
    img = Image.fromarray((out * 255 + 0.5).astype(np.uint8), 'RGBA')
    ys, xs = np.nonzero(a > 0.02)
    box = (max(0, xs.min() - pad), max(0, ys.min() - pad), min(img.width, xs.max() + pad), min(img.height, ys.max() + pad))
    img = img.crop(box)
    path = os.path.join(RAW, name + '.png')
    img.save(path)
    return path, img.size


if __name__ == '__main__':
    print(*matte(sys.argv[1], int(sys.argv[2]) if len(sys.argv) > 2 else 24))
