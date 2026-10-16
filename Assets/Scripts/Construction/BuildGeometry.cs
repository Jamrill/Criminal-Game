using UnityEngine;

namespace JuegoCriminal.Construction
{
    public static class BuildGeometry
    {
        // Bounds in the placement frame: strip the saved prefab translation and
        // rotation, but retain its authored scale, just like the preview/spawn.
        public static Bounds Bounds(GameObject prefab)
        {
            var result=new Bounds();bool first=true;
            var root=prefab.transform;
            var toPlacement=Matrix4x4.Rotate(Quaternion.Inverse(root.rotation))*Matrix4x4.Translate(-root.position);
            void Include(UnityEngine.Bounds bounds,Transform child)
            {
                var matrix=toPlacement*child.localToWorldMatrix;
                foreach(float x in new[]{bounds.min.x,bounds.max.x})
                    foreach(float y in new[]{bounds.min.y,bounds.max.y})
                        foreach(float z in new[]{bounds.min.z,bounds.max.z})
                        {
                            var point=matrix.MultiplyPoint3x4(new Vector3(x,y,z));
                            if(first){result=new Bounds(point,Vector3.zero);first=false;}else result.Encapsulate(point);
                        }
            }
            foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                if(filter.sharedMesh)Include(filter.sharedMesh.bounds,filter.transform);
            foreach(var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Include(renderer.localBounds,renderer.transform);
            return result;
        }
    }
}
