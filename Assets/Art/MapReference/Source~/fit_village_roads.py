# Fit traced road centerlines between placed buildings: each vertex slides along its normal (max 4m),
# minimizing building intrusion + offset + bend; junction endpoints re-snapped afterwards.
import sys, json, math
S=sys.argv[1]; SRC='Assets/Art/MapReference/Source~/'
roads=json.load(open(SRC+'village_roads_traced.json'))['roads']
WIDTH={'P1_SouthMain':6,'S2_Street_x-21':4.5,'S3_Street_x9':4.5,'S4_Street_x21':4.5,'S6_West_x-47':4.5,'S7_West_x-61':4.5}
for r in roads: r['width']=WIDTH.get(r['id'],r['width'])
bld=json.load(open(S+'/placed.json'))
MAXOFF=3.0; MARGIN=0.75; MINW={'paved':4.0,'dirt':3.0}
def seg_dist(p,a,b):
    dx,dz=b[0]-a[0],b[1]-a[1]; L=dx*dx+dz*dz
    t=0 if L==0 else max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dz)/L)); return math.hypot(p[0]-a[0]-dx*t,p[1]-a[1]-dz*t)
def cross(a,b,c): return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
def seg_inter(a,b,c,d):
    return (cross(a,b,c)>0)!=(cross(a,b,d)>0) and (cross(c,d,a)>0)!=(cross(c,d,b)>0)
def inside(p,poly):
    s=[cross(poly[i],poly[(i+1)%4],p)>0 for i in range(4)]; return all(s) or not any(s)
def poly_seg_dist(poly,a,b):
    if inside(a,poly) or inside(b,poly): return -1.0
    for i in range(4):
        if seg_inter(a,b,poly[i],poly[(i+1)%4]): return -1.0
    return min(min(seg_dist(a,poly[i],poly[(i+1)%4]) for i in range(4)), min(seg_dist(p,a,b) for p in poly))
polys=[b['poly'] for b in bld]
cent=[(sum(p[0] for p in q)/4,sum(p[1] for p in q)/4) for q in polys]
def near(a,b,r=25):
    mx,mz=(a[0]+b[0])/2,(a[1]+b[1])/2; L=math.hypot(b[0]-a[0],b[1]-a[1])/2+r
    return [polys[i] for i,c in enumerate(cent) if math.hypot(c[0]-mx,c[1]-mz)<L]
def densify(pts,step=6.0):
    out=[pts[0]]
    for a,b in zip(pts,pts[1:]):
        n=max(1,int(math.hypot(b[0]-a[0],b[1]-a[1])/step+0.5))
        for k in range(1,n+1): out.append([a[0]+(b[0]-a[0])*k/n,a[1]+(b[1]-a[1])*k/n])
    return out
def normals(pts):
    N=[]
    for i in range(len(pts)):
        a=pts[max(0,i-1)]; b=pts[min(len(pts)-1,i+1)]; dx,dz=b[0]-a[0],b[1]-a[1]; L=math.hypot(dx,dz); N.append((-dz/L,dx/L))
    return N
def seg_cost(a,b,c,cand):
    cost=0
    for q in cand:
        d=poly_seg_dist(q,a,b)
        if d<c: cost+=(c-d+ (2.0 if d<0 else 0))**2*100
    return cost
def fit(r,hw):
    base=densify(r['pts']); N=normals(base); n=len(base); off=[0.0]*n
    P=lambda i:[base[i][0]+N[i][0]*off[i],base[i][1]+N[i][1]*off[i]]
    cands=[near(base[max(0,i-1)],base[min(n-1,i+1)]) for i in range(n)]
    c=hw+MARGIN; steps=[x*0.25 for x in range(int(-MAXOFF*4),int(MAXOFF*4)+1)]
    for it in range(8):
        for i in range(n):
            best=None
            for o in steps:
                old=off[i]; off[i]=o; p=P(i); cost=0.04*o*o
                if i>0: cost+=seg_cost(P(i-1),p,c,cands[i])
                if i<n-1: cost+=seg_cost(p,P(i+1),c,cands[i])
                if 0<i<n-1: cost+=0.6*(o-(off[i-1]+off[i+1])/2)**2
                off[i]=old
                if best is None or cost<best[0]: best=(cost,o)
            off[i]=best[1]
    pts=[P(i) for i in range(n)]
    bad=sum(1 for a,b in zip(pts,pts[1:]) for q in near(a,b) if poly_seg_dist(q,a,b)<hw+0.25)
    return pts,bad,max(abs(o) for o in off)
res={}
for r in roads:
    hw=r['width']/2; pts,bad,mo=fit(r,hw); w=r['width']
    while False:
        w-=1; pts2,bad2,mo2=fit(r,w/2)
        if bad2<bad: pts,bad,mo=pts2,bad2,mo2
        else: w+=1; break
    res[r['id']]=dict(kind=r['kind'],width=w,orig_width=r['width'],pts=[[round(x,2),round(z,2)] for x,z in pts],bad=bad,maxoff=round(mo,2))
# re-snap endpoints that originally sat on another road (junctions / T-crossings)
orig={r['id']:r for r in roads}
for rid,v in res.items():
    for end in (0,-1):
        p0=orig[rid]['pts'][end]
        for oid,ov in res.items():
            if oid==rid: continue
            op=orig[oid]['pts']
            if min(seg_dist(p0,a,b) for a,b in zip(op,op[1:]))<1.5:
                q=v['pts'][end]; best=None
                for a,b in zip(ov['pts'],ov['pts'][1:]):
                    dx,dz=b[0]-a[0],b[1]-a[1]; L=dx*dx+dz*dz; t=max(0,min(1,((q[0]-a[0])*dx+(q[1]-a[1])*dz)/L)); pp=[a[0]+dx*t,a[1]+dz*t]
                    dd=math.hypot(pp[0]-q[0],pp[1]-q[1])
                    if best is None or dd<best[0]: best=(dd,pp)
                v['pts'][end]=[round(best[1][0],2),round(best[1][1],2)]; break
json.dump({'roads':[dict(id=k,kind=v['kind'],width=v['width'],pts=v['pts']) for k,v in res.items()]},open(S+'/fitted2_roads.json','w'),indent=1)
for rid,v in res.items(): print(f"{rid:18s} width {v['orig_width']}->{v['width']}  maxShift {v['maxoff']}m  stillTouching {v['bad']}")
