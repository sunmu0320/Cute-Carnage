# Plans South-village building lots: faces each lot toward its nearest road, picks prefabs (no repeat within 20m),
# separates overlaps (max 2m shift), flags road clearance, renders overlays. Usage: python plan_village_lots.py <outdir>
import sys, json, math
from PIL import Image, ImageDraw
S=sys.argv[1]; SRC='Assets/Art/MapReference/Source~/'
FP=json.load(open(SRC+'building_footprints.json'))
roads=json.load(open(SRC+'village_roads.json'))['roads']
lots=json.load(open(SRC+'village_lots.json'))['lots']
segs=[(r['id'],a,b,r['width']/2) for r in roads for a,b in zip(r['pts'],r['pts'][1:])]
FRONT={'house':180,'Church':180,'Barn':0}
HOUSES=['HouseRoot']+[f'HouseRoot ({i})' for i in range(1,16)]
LARGE=[f'HouseRoot ({i})' for i in (11,12,13,14,15)]
def bearing(x,z): return math.degrees(math.atan2(x,z))
def nearest(x,z):
    best=None
    for rid,(x0,z0),(x1,z1),hw in segs:
        dx,dz=x1-x0,z1-z0; t=max(0,min(1,((x-x0)*dx+(z-z0)*dz)/(dx*dx+dz*dz))); px,pz=x0+dx*t,z0+dz*t
        d=math.hypot(x-px,z-pz)-hw
        if best is None or d<best[0]: best=(d,px-x,pz-z,rid)
    return best
def rect(x,z,yaw,fp):
    x0,x1,z0,z1=fp; c,s=math.cos(math.radians(yaw)),math.sin(math.radians(yaw))
    return [(x+lx*c+lz*s, z-lx*s+lz*c) for lx,lz in ((x0,z0),(x1,z0),(x1,z1),(x0,z1))]
def seg_dist(p,a,b):
    dx,dz=b[0]-a[0],b[1]-a[1]; L=dx*dx+dz*dz
    t=0 if L==0 else max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dz)/L)); return math.hypot(p[0]-a[0]-dx*t,p[1]-a[1]-dz*t)
def inside(p,poly):
    sgn=None
    for i in range(4):
        a,b=poly[i],poly[(i+1)%4]; cr=(b[0]-a[0])*(p[1]-a[1])-(b[1]-a[1])*(p[0]-a[0])
        if sgn is None: sgn=cr>0
        elif (cr>0)!=sgn: return False
    return True
def poly_seg_dist(poly,a,b):
    if inside(a,poly) or inside(b,poly): return 0
    d=min(seg_dist(a,poly[i],poly[(i+1)%4]) for i in range(4)); d=min(d,min(seg_dist(p,a,b) for p in poly)); return d
def overlap(P,Q,m=0.3):
    for poly in (P,Q):
        for i in range(4):
            a,b=poly[i],poly[(i+1)%4]; nx,nz=-(b[1]-a[1]),b[0]-a[0]; L=math.hypot(nx,nz); nx,nz=nx/L,nz/L
            pp=[p[0]*nx+p[1]*nz for p in P]; qq=[q[0]*nx+q[1]*nz for q in Q]
            if max(pp)+m<=min(qq) or max(qq)+m<=min(pp): return False
    return True
out=[]; used={}
for L in lots:
    cls=L['cls']; x,z=L['x'],L['z']
    if cls=='C': pf='Church'
    elif cls=='B': pf='Barn'
    elif cls=='W': pf='WaterFountain'
    elif cls=='F': pf='Farm2'  # Farm1 is an empty fenced plot; reference fields are plowed
    else:
        pool=LARGE if cls=='L' else HOUSES
        near=[o['prefab'] for o in out if math.hypot(o['x']-x,o['z']-z)<20]
        pool2=[p for p in pool if p not in near] or pool
        pf=min(pool2,key=lambda p:(used.get(p,0),pool.index(p)))
    used[pf]=used.get(pf,0)+1
    d,fx,fz,rid=nearest(x,z)
    if 'yaw' in L and cls in ('F',): yaw=L['yaw']
    elif pf=='WaterFountain' or pf.startswith('Farm'): yaw=L.get('yaw',0)
    else:
        key='Church' if pf=='Church' else 'Barn' if pf=='Barn' else 'house'
        yaw=bearing(fx,fz)-FRONT[key]
        if 'yawFix' in L:
            fb=math.atan2(fx,fz); yaw=max((L['yawFix']+k*180 for k in range(2)),key=lambda y:math.cos(math.radians(y+FRONT[key])-fb))
        elif 'yaw' in L:  # reference hint fixes the footprint angle; pick the 90-degree turn whose front faces the road best
            fb=math.atan2(fx,fz)
            yaw=max((L['yaw']+k*90 for k in range(4)),key=lambda y:math.cos(math.radians(y+FRONT[key])-fb))
    yaw=round(((yaw+180)%360)-180,1)
    out.append(dict(id=L['id'],cls=cls,prefab=pf,x=x,z=z,yaw=yaw,road=rid,gap=round(d,1)))
issues=[]
for o in out: o['x0'],o['z0']=o['x'],o['z']
for it in range(60):
    moved=False
    for o in out: o['poly']=rect(o['x'],o['z'],o['yaw'],FP[o['prefab']])
    for i,o in enumerate(out):
        for p in out[i+1:]:
            if math.hypot(o['x']-p['x'],o['z']-p['z'])<25 and overlap(o['poly'],p['poly'],0.4):
                dx,dz=p['x']-o['x'],p['z']-o['z']; L=math.hypot(dx,dz) or 1; st=0.15
                for q,sg in ((o,-1),(p,1)):
                    if math.hypot(q['x']+sg*dx/L*st-q['x0'],q['z']+sg*dz/L*st-q['z0'])<=2.0: q['x']+=sg*dx/L*st; q['z']+=sg*dz/L*st; moved=True
    if not moved: break
for o in out: o['shift']=round(math.hypot(o['x']-o['x0'],o['z']-o['z0']),2); o['x']=round(o['x'],2); o['z']=round(o['z'],2)
for o in out:
    o['poly']=rect(o['x'],o['z'],o['yaw'],FP[o['prefab']])
    o['flags']=[]
    for rid,a,b,hw in segs:
        dd=poly_seg_dist(o['poly'],a,b)
        if dd<hw+0.5: o['flags'].append(f'road {rid} ({dd-hw:+.1f}m)')
for i,o in enumerate(out):
    for p in out[i+1:]:
        if math.hypot(o['x']-p['x'],o['z']-p['z'])<30 and overlap(o['poly'],p['poly']): o['flags'].append(f"overlaps #{p['id']}"); p['flags'].append(f"overlaps #{o['id']}")
json.dump([{k:v for k,v in o.items() if k!='poly'} for o in out],open(S+'/plan_out.json','w'),indent=1)
bad=[o for o in out if o['flags']]
print('lots',len(out),'flagged',len(bad))
ov=[o for o in out if any(f.startswith('overlaps') for f in o['flags'])]; rd=[o for o in out if any(f.startswith('road') for f in o['flags'])]
print('still overlapping:',[o['id'] for o in ov]); print('road conflicts:',len(rd)); print('shifted >0.5m:',[(o['id'],o['shift']) for o in out if o['shift']>0.5])

# render
ref=Image.open(SRC+'reference_map.webp').convert('RGB')
def render(name,x0,x1,z0,z1,ppm):
    W,H=int((x1-x0)*ppm),int((z1-z0)*ppm)
    img=ref.transform((W,H),Image.AFFINE,(1/ppm/0.305,0,665+(x0+2.19)/0.305,0,1/ppm/0.335,560-(z1-28.04)/0.335),resample=Image.BICUBIC)
    d=ImageDraw.Draw(img,'RGBA'); P=lambda x,z:((x-x0)*ppm,(z1-z)*ppm)
    for r in roads: d.line([P(*p) for p in r['pts']],fill=(255,0,255,140),width=max(1,int(ppm*0.4)))
    for o in out:
        col=(255,60,60) if o['flags'] else (60,255,90)
        d.polygon([P(*p) for p in o['poly']],outline=col+(255,),fill=col+(45,))
        if not o['prefab'].startswith('Farm') and o['prefab']!='WaterFountain':
            key='Church' if o['prefab']=='Church' else 'Barn' if o['prefab']=='Barn' else 'house'
            fb=math.radians(o['yaw']+FRONT[key]); fx,fz=o['x']+math.sin(fb)*4,o['z']+math.cos(fb)*4
            d.line([P(o['x'],o['z']),P(fx,fz)],fill=(255,255,255,255),width=2)
        d.text((P(o['x'],o['z'])[0]+3,P(o['x'],o['z'])[1]-12),str(o['id']),fill=(255,255,0,255))
    img.save(f'{S}/{name}.png')
render('lots_all',-155,65,-145,12,6.56)
render('lots_c1',-80,-10,-140,0,9); render('lots_c2',-15,58,-140,0,9)
