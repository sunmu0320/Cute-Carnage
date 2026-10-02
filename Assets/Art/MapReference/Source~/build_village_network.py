# Design the south-village road network from the placed buildings (grid 0.5 m):
# main roads = A* through anchors, kept away from buildings (TL_Road);
# door paths = cheapest route from each door to the network built so far (TL_Grass worn paths).
import sys, json, math, heapq
import numpy as np
from PIL import Image, ImageDraw

S = sys.argv[1]
X0, X1, Z0, Z1, C = -168.0, 128.0, -168.0, 16.0, 0.5
NX, NZ = int((X1 - X0) / C), int((Z1 - Z0) / C)
B = json.load(open(S + '/placed.json'))


def cell(x, z): return int((z - Z0) / C), int((x - X0) / C)
def world(i, j): return X0 + (j + 0.5) * C, Z0 + (i + 0.5) * C
def pix(x, z): return ((x - X0) / C, (Z1 - z) / C)  # image coords, row 0 = north


m = Image.new('L', (NX, NZ), 0)
d = ImageDraw.Draw(m)
for b in B:
    d.polygon([pix(*p) for p in b['poly']], fill=255)
bld = np.flipud(np.asarray(m) > 0)  # row 0 = south

mask = np.asarray(Image.open('Assets/Art/MapReference/MapTerrainMask.png').convert('RGB'))
MH, MW = mask.shape[:2]
zz, xx = np.mgrid[0:NZ, 0:NX]
wx = X0 + (xx + 0.5) * C
wz = Z0 + (zz + 0.5) * C
mi = MH - 1 - ((wz + 175) / 0.5).astype(int)
mj = ((wx + 205) / 0.5).astype(int)
inb = (mi >= 0) & (mi < MH) & (mj >= 0) & (mj < MW)
water = ~inb
mic, mjc = np.clip(mi, 0, MH - 1), np.clip(mj, 0, MW - 1)
water |= (mask[mic, mjc, 0] > 127) | (mask[mic, mjc, 1] > 127)

# distance (m) to nearest building: brute force over building samples is too slow -> chamfer passes
INF = 1e9
dist = np.where(bld, 0.0, INF)
for _ in range(2):
    for i in range(NZ):
        if i > 0:
            up = dist[i - 1]
            dist[i] = np.minimum(dist[i], np.minimum(up + C, np.minimum(np.roll(up, 1) + C * 1.414, np.roll(up, -1) + C * 1.414)))
        row = dist[i]
        for j in range(1, NX):
            if row[j - 1] + C < row[j]: row[j] = row[j - 1] + C
    for i in range(NZ - 1, -1, -1):
        if i < NZ - 1:
            dn = dist[i + 1]
            dist[i] = np.minimum(dist[i], np.minimum(dn + C, np.minimum(np.roll(dn, 1) + C * 1.414, np.roll(dn, -1) + C * 1.414)))
        row = dist[i]
        for j in range(NX - 2, -1, -1):
            if row[j + 1] + C < row[j]: row[j] = row[j + 1] + C

NB = [(-1, 0, 1), (1, 0, 1), (0, -1, 1), (0, 1, 1), (-1, -1, 1.414), (-1, 1, 1.414), (1, -1, 1.414), (1, 1, 1.414)]


def route(starts, goal_fn, need, prefer=None, h_goal=None, maxcost=INF):
    g = {}
    pq = []
    for s in starts:
        g[s] = 0
        heapq.heappush(pq, ((h_goal(s) if h_goal else 0), 0, s, None))
    parent = {}
    while pq:
        f, cst, u, par = heapq.heappop(pq)
        if u in parent: continue
        parent[u] = par
        if cst > maxcost: return None
        if goal_fn(u):
            path = [u]
            while parent[path[-1]] is not None: path.append(parent[path[-1]])
            return path[::-1]
        i, j = u
        for di, dj, l in NB:
            v = (i + di, j + dj)
            if not (0 <= v[0] < NZ and 0 <= v[1] < NX) or v in parent: continue
            if water[v] or dist[v] < need: continue
            pen = 1 + 3.0 * max(0.0, need + 2.5 - dist[v]) / 2.5
            if prefer is not None and prefer[v]: pen *= 0.35
            nc = cst + l * C * pen
            if nc < g.get(v, INF):
                g[v] = nc
                heapq.heappush(pq, (nc + (h_goal(v) if h_goal else 0), nc, v, u))
    return None


def smooth(pts, it=2):
    for _ in range(it):
        out = [pts[0]]
        for a, b in zip(pts, pts[1:]):
            out += [(0.75 * a[0] + 0.25 * b[0], 0.75 * a[1] + 0.25 * b[1]), (0.25 * a[0] + 0.75 * b[0], 0.25 * a[1] + 0.75 * b[1])]
        out.append(pts[-1])
        pts = out
    return pts


def rdp(pts, eps):
    if len(pts) < 3: return list(pts)
    a, b = pts[0], pts[-1]
    dx, dz = b[0] - a[0], b[1] - a[1]
    L = math.hypot(dx, dz) or 1
    dmax, k = 0, 0
    for i in range(1, len(pts) - 1):
        dd = abs(dx * (a[1] - pts[i][1]) - dz * (a[0] - pts[i][0])) / L
        if dd > dmax: dmax, k = dd, i
    if dmax > eps: return rdp(pts[:k + 1], eps)[:-1] + rdp(pts[k:], eps)
    return [a, b]


def stamp(pts, hw):
    im = Image.new('L', (NX, NZ), 0)
    dd = ImageDraw.Draw(im)
    dd.line([pix(*p) for p in pts], fill=255, width=max(1, int(2 * hw / C)), joint='curve')
    r = hw / C
    for p in pts:
        q = pix(*p)
        dd.ellipse([q[0] - r, q[1] - r, q[0] + r, q[1] + r], fill=255)
    return np.flipud(np.asarray(im) > 0)


def snap_free(c, need):
    for r in range(0, 60):
        for di in range(-r, r + 1):
            for dj in range(-r, r + 1):
                if max(abs(di), abs(dj)) != r: continue
                v = (c[0] + di, c[1] + dj)
                if 0 <= v[0] < NZ and 0 <= v[1] < NX and not water[v] and dist[v] >= need: return v
    return c


net = np.zeros((NZ, NX), bool)
net_path = np.zeros((NZ, NX), bool)
roads = []
MAIN = json.load(open(S + '/main_anchors.json'))
for rid, hw, anch in MAIN:
    need = hw + 0.4
    full = []
    for a, b in zip(anch, anch[1:]):
        sa, gb = snap_free(cell(*a), need), snap_free(cell(*b), need)
        hg = lambda u, gb=gb: math.hypot(u[0] - gb[0], u[1] - gb[1]) * C
        p = route([sa], lambda u, gb=gb: u == gb, need, prefer=net, h_goal=hg)
        if p is None:
            print('NO ROUTE', rid, a, b)
            continue
        full += [world(*c) for c in p]
    if not full: continue
    pts = rdp(smooth(rdp(full, 1.2), 2), 0.4)
    roads.append(dict(id=rid, kind=('path' if hw < 2 else 'road'), width=hw * 2, pts=[[round(x, 2), round(z, 2)] for x, z in pts]))
    net |= stamp(pts, hw)

doors = json.load(open(S + '/doors.json'))
PATHW = 2.6
# nearest-to-network doors first so later paths can join earlier ones
def dnet(dr):
    i, j = cell(dr[0], dr[1])
    ys, xs = np.nonzero(net[max(0, i - 120):i + 120, max(0, j - 120):j + 120])
    return 1e9 if len(ys) == 0 else float(np.min(np.hypot(ys + max(0, i - 120) - i, xs + max(0, j - 120) - j)))
pending = list(doors)
while pending:
    pending.sort(key=dnet)
    dx, dz, ang, bid = pending.pop(0)
    fx, fz = math.sin(math.radians(ang)), math.cos(math.radians(ang))
    s = cell(dx + fx * 1.2, dz + fz * 1.2)
    if not (0 <= s[0] < NZ and 0 <= s[1] < NX): continue
    if net[s]: continue
    p = route([s], lambda u: net[u], 0.6, prefer=net_path, maxcost=90)
    if p is None:
        print('door without route', bid)
        continue
    pts = [(dx, dz)] + [world(*c) for c in p]
    pts = rdp(smooth(rdp(pts, 0.7), 2), 0.3)
    roads.append(dict(id='door_' + bid, kind='path', width=PATHW, pts=[[round(x, 2), round(z, 2)] for x, z in pts]))
    st_ = stamp(pts, PATHW / 2); net |= st_; net_path |= stamp(pts, PATHW * 1.5)

json.dump({'roads': roads}, open(S + '/network.json', 'w'), indent=1)
np.save(S + '/dist.npy', dist)
print('main', [(r['id'], len(r['pts'])) for r in roads if r['kind'] == 'road'])
print('door paths', sum(1 for r in roads if r['kind'] == 'path'), 'total path m', round(sum(sum(math.hypot(b[0]-a[0], b[1]-a[1]) for a, b in zip(r['pts'], r['pts'][1:])) for r in roads if r['kind'] == 'path')))
