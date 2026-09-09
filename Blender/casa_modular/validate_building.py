"""Read-only geometry/clearance checks; writes a report beside the blend.
Door passage checks assume leaves open and use a 2 x 2 x 4 character.
This is a conservative furniture-envelope check, not a Unity navigation test.
"""
import bpy, json, math
from pathlib import Path
from mathutils import Vector
from collections import deque

OUT=Path(__file__).resolve().parent
sc=bpy.data.scenes['02_Edificio_4_plantas']; bpy.context.window.scene=sc; bpy.context.view_layer.update()
result={'scene':sc.name,'character':[2,2,4],'checks':[],'passages':{}}
def check(name,value):
    result['checks'].append({'name':name,'passed':bool(value)})
    assert value,name
check('four floors',len([c for c in bpy.data.collections if c.name.startswith('Planta_')])==4)
check('eight apartments',len([c for c in bpy.data.collections if c.name.startswith('Vivienda_')])==8)
for i in range(4): check('floor height %d'%i,abs(bpy.data.collections['Planta_%02d'%(i+1)]['cota_suelo']-8.4*i)<1e-5)
def roots(resource): return [o for o in sc.objects if o.type=='EMPTY' and o.get('recurso')==resource]
for resource,count in [('Stairs_U_Floor',3),('Lift_Landing_Doors',4),('Lift_Cabin',1),('Frigorifico_Interior',8),('Cama_Doble',8),('Cama_Individual',8),('Ducha',8),('Vater',8),('Lavabo_Mueble',8),('Buzon_Individual',8)]:
    check(resource+' count',len(roots(resource))==count)
for r in roots('Frigorifico_Interior'):
    ds=[o for o in r.children if 'Puerta_' in o.name]
    check(r.name+' separate hinged doors',len(ds)==2 and all(abs(o.location.x+1.39)<1e-5 and abs(o.location.y+1.39)<1e-5 for o in ds))
    check(r.name+' no animation',all(o.animation_data is None for o in ds))
for r in roots('Corredera_Balcon_8x8'):
    check(r.name+' separate sliding leaf',len([o for o in r.children if 'Hoja_Movil' in o.name])==1)
check('shared original stair mesh',all(r.children[0].data==bpy.data.objects['Stairs_U_Floor'].data for r in roots('Stairs_U_Floor')))
def bounds(obs):
    vs=[o.matrix_world@Vector(v) for o in obs if o.type=='MESH' for v in o.bound_box]
    return ([min(v[i] for v in vs) for i in range(3)],[max(v[i] for v in vs) for i in range(3)])

# Floor 1 represents identical layouts repeated at the other three floor elevations.
for side,ax in [('A',-32),('B',8)]:
    co=bpy.data.collections['01'+side+'_Mobiliario']; obstacles=[]
    for r in co.objects:
        if r.type!='EMPTY': continue
        lo,hi=bounds(r.children)
        if hi[2]>.15 and lo[2]<4: obstacles.append((lo[0]-ax,lo[1],hi[0]-ax,hi[1],r.name))
    # Interior walls, with actual 2.56-wide frame clearances. Leaves assumed open.
    walls=[(12-.15,20,12+.15,28,'bedroom divider'),(16-.15,16,16+.15,20,'bathroom wall'),
           (16,12-.15,24,12+.15,'bathroom kitchen wall')]
    # Both bedroom doors connect to the hall, never through the bathroom.
    # Local X = 4.72..7.28 and 12.72..15.28.
    for a,b in [(0,4.72),(7.28,12.72),(15.28,24)]: walls.append((a,19.81,b,20.19,'bedroom front'))
    for a,b in [(12,12.72),(15.28,16)]: walls.append((15.81,a,16.19,b,'bathroom jamb'))
    obstacles+=walls
    def clear(x,y):
        if not (1.16<=x<=22.84 and 1.16<=y<=26.84): return False
        return not any(x+1>a+.00001 and x-1<c-.00001 and y+1>b+.00001 and y-1<d-.00001 for a,b,c,d,_ in obstacles)
    # 0.1-unit grid resolves the narrow original kit openings without aliasing.
    step=.1; seed=(int((22.5 if side=='A' else 1.5)/step),60)
    seen=set(); q=deque([seed])
    while q:
        pt=q.popleft()
        if pt in seen or not clear(pt[0]*step,pt[1]*step): continue
        seen.add(pt)
        for dx,dy in [(1,0),(-1,0),(0,1),(0,-1)]:
            p=(pt[0]+dx,pt[1]+dy)
            if p not in seen and 10<=p[0]<=230 and 10<=p[1]<=270: q.append(p)
    destinations={'double bedroom aisle':(8.2,23.4),'single bedroom / desk aisle':(18,23.3),'bathroom entry':(18.2,14.6),'balcony approach':(10,1.5),'kitchen work aisle':(20,7.7)}
    result['passages'][side]={'reachable_cells':len(seen),'destinations':{}}
    for name,(x,y) in destinations.items():
        good=(round(x/step),round(y/step)) in seen
        result['passages'][side]['destinations'][name]=good
        check(side+' accessible '+name,good)
    # Body footprints do not sit in the entry clear zones.
    check(side+' entrance clear',clear(22.5 if side=='A' else 1.5,6))
result['limitations']='Furniture envelope / open-door plan check only. No physics, colliders or animation simulation.'
(OUT/'edificio_validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print('VALIDATION_PASSED',len(result['checks']),flush=True)
