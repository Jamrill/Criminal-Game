"""Localized, topology-preserving hand correction. Run once on original blend."""
import bpy
import numpy as np
from pathlib import Path
import shutil

path = Path(bpy.data.filepath)
backup = path.with_name(path.stem + '_antes_de_manos.blend')
if backup.exists():
    raise RuntimeError('Backup already exists: refusing to apply correction twice.')
shutil.copy2(path, backup)

def smoothstep(a, b, v):
    t = np.clip((v-a)/(b-a), 0, 1)
    return t*t*(3-2*t)

for name in ('Personaje_femenino', 'Personaje_masculino'):
    ob = bpy.data.objects[name]
    mesh = ob.data
    original = np.empty((len(mesh.vertices),3), dtype=np.float64)
    mesh.vertices.foreach_get('co', original.ravel())
    p = original * np.array(ob.scale) + np.array(ob.location)
    side = np.where(p[:,0] >= 0, 1., -1.)
    p[:,0] *= side
    u = ((p[:,0]-1.095) - (p[:,2]-1.814))/np.sqrt(2)
    weight = smoothstep(.025,.105,u) * smoothstep(.97,1.065,p[:,0]) * (p[:,2]<1.9)
    ids = np.flatnonzero(weight>0)
    edges = np.empty((len(mesh.edges),2),dtype=np.int32)
    mesh.edges.foreach_get('vertices',edges.ravel())
    edges = edges[np.any(weight[edges]>0,axis=1)]
    src = np.concatenate((edges[:,0],edges[:,1]))
    dst = np.concatenate((edges[:,1],edges[:,0]))
    count = np.maximum(np.bincount(src,minlength=len(p)),1)
    # Local relaxation softens the existing cut-like creases, retaining topology.
    for _ in range(28):
        average = np.column_stack([np.bincount(src,weights=p[dst,k],minlength=len(p))/count for k in range(3)])
        p[ids] += .46 * weight[ids,None] * (average[ids]-p[ids])
    u = ((p[:,0]-1.095) - (p[:,2]-1.814))/np.sqrt(2)
    w = (p[:,0]+p[:,2])/np.sqrt(2)
    y = p[:,1].copy()
    displacement = np.zeros_like(p)
    # Four fingers: taper sides gently while leaving webbing untouched.
    bands = [(-.205,-.09),(-.09,-.004),(-.004,.067),(.067,.16)]
    for lo,hi in bands:
        mask = (weight>0)&(u>.285)&(y>lo)&(y<hi)&(p[:,0]>1.225)
        if mask.sum()<50:
            continue
        end = np.percentile(u[mask],99.5)
        center = np.median(y[mask & (u<max(.32,end-.055))])
        taper = smoothstep(.285,end,u)
        amount = .13 if name.endswith('masculino') else .075
        displacement[mask,1] -= (y[mask]-center)*amount*taper[mask]
        # Broad low relief over the metacarpal heads and dorsal tendons.
        dorsal = smoothstep(2.105,2.15,w)
        knuckle = np.exp(-((u-.255)/.033)**2-((y-center)/.026)**2)
        tendon = np.exp(-((u-.165)/.075)**2-((y-center)/.010)**2)
        relief = (.0038*knuckle+.0018*tendon)*dorsal*weight
        displacement[:,0] += relief/np.sqrt(2)
        displacement[:,2] += relief/np.sqrt(2)
    # Reduce the overly inflated palm slightly, without moving wrist or fingertips.
    flatten = .055*np.exp(-((u-.18)/.095)**2)*weight
    dw = -(w-2.115)*flatten
    displacement[:,0] += dw/np.sqrt(2)
    displacement[:,2] += dw/np.sqrt(2)
    p += displacement
    p[:,0] *= side
    result = (p-np.array(ob.location))/np.array(ob.scale)
    # Preserve every coordinate outside the hands bit-for-bit.
    result[weight==0] = original[weight==0]
    assert np.array_equal(result[weight==0],original[weight==0])
    mesh.vertices.foreach_set('co',result.ravel())
    mesh.update()
    print(name,'hand vertices:',len(ids),'max world displacement:',float(np.max(np.linalg.norm((result-original)*np.array(ob.scale),axis=1))))

bpy.ops.wm.save_as_mainfile(filepath=str(path))
print('Saved hand-only correction; backup:',backup)
