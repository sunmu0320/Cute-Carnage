"""Rebuilds ../MapTerrainMask.png from reference_map.webp.

Output is world-aligned (x -205..200, z -175..232, 0.5 m/px, north-up):
  red   = water (river, branches, waterfall, pond, sea)
  green = outside the playable rim
Image -> world mapping anchors the image's home base (665, 560 px) on HomeBase (-2.19, 28.04),
0.305 m/px horizontally, 0.335 m/px vertically. Needs Pillow + numpy.  Run: python build_map_mask.py
"""
from collections import deque
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
X0, X1, Z0, Z1, R = -205.0, 200.0, -175.0, 232.0, 0.5
NX, NZ = int((X1 - X0) / R), int((Z1 - Z0) / R)
xs = X0 + (np.arange(NX) + 0.5) * R
zs = Z0 + (np.arange(NZ) + 0.5) * R  # row 0 = south


def to_img(x, z):
    return 665 + (x + 2.19) / 0.305, 560 - (z - 28.04) / 0.335


def px2w(px, py):
    return -2.19 + (px - 665) * 0.305, 28.04 - (py - 560) * 0.335


def cell(x, z):
    return int((z - Z0) / R), int((x - X0) / R)


def fill_rect(m, x0, z0, x1, z1, val=True):
    i0, j0 = cell(x0, z0)
    i1, j1 = cell(x1, z1)
    m[i0:i1 + 1, j0:j1 + 1] = val


# 1. water pixels (teal/blue), close small gaps, drop blobs under ~150 m2
im = np.asarray(Image.open(os.path.join(HERE, 'reference_map.webp')).convert('RGB')).astype(int)
r, g, b = im[..., 0], im[..., 1], im[..., 2]
w = ((b > r + 25) & (g > r + 10) & (b > 70)).astype(np.uint8) * 255
w = np.asarray(Image.fromarray(w).filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.MinFilter(9))) > 0
H, W = w.shape
seen = np.zeros(w.shape, bool)
keep = np.zeros(w.shape, bool)
for y0, x0 in zip(*np.nonzero(w)):
    if seen[y0, x0]:
        continue
    q = deque([(y0, x0)])
    seen[y0, x0] = True
    pts = []
    while q:
        y, x = q.popleft()
        pts.append((y, x))
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            yy, xx = y + dy, x + dx
            if 0 <= yy < H and 0 <= xx < W and w[yy, xx] and not seen[yy, xx]:
                seen[yy, xx] = True
                q.append((yy, xx))
    if len(pts) >= 1500:
        ys_, xs_ = zip(*pts)
        keep[list(ys_), list(xs_)] = True

# 2. resample into world cells
PX, PY = to_img(xs[None, :], zs[:, None])
PX = np.clip(np.round(PX).astype(int), 0, W - 1)
PY = np.round(PY).astype(int)
water = keep[np.clip(PY, 0, H - 1), PX] & (PY >= 0) & (PY < H)

# 3. hand fixes: close the image's bridge gaps under our two road bridges, join the white-water cascade,
#    trim the east river tip so the factory's NE corner stays clear
fill_rect(water, -9, 71, 2, 84)
fill_rect(water, 39, 20, 51, 41)
fill_rect(water, -113, 138, -104, 176)
fill_rect(water, 170, -10, 205, 52, False)

# 4. outer rim traced by hand from the image (px), structures kept inside it
rim_px = [(110, 40), (600, 10), (1000, 20), (1230, 90), (1300, 280), (1300, 620), (1270, 760), (1300, 1000), (1300, 1199),
          (850, 1199), (600, 1180), (330, 1140), (250, 1060), (110, 900), (50, 760), (10, 600), (10, 300), (40, 130)]
inside = Image.new('L', (NX, NZ), 0)
ImageDraw.Draw(inside).polygon([((x - X0) / R, (z - Z0) / R) for x, z in (px2w(*p) for p in rim_px)], fill=255)
outside = np.asarray(inside) == 0
fill_rect(outside, -205, 0, -120, 60, False)   # mall
fill_rect(outside, 100, -35, 200, 50, False)   # factory
water &= ~outside

rgb = np.zeros((NZ, NX, 3), np.uint8)
rgb[..., 0] = water * 255
rgb[..., 1] = outside * 255
Image.fromarray(rgb[::-1]).save(os.path.join(HERE, '..', 'MapTerrainMask.png'))
print(f'{NX}x{NZ}, water {water.sum() * R * R:.0f} m2')
