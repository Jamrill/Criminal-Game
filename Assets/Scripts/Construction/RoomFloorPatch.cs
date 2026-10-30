using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JuegoCriminal.Construction
{
    // Saved planar triangles let edge tiles fit diagonal/concave rooms without
    // extending a full square outside the walls. Geometry is rebuilt on load.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class RoomFloorPatch : MonoBehaviour
    {
        public Vector3[] triangles;
        public Material[] materials;
        public const float Thickness=.02f;
        // The prefab origin is the storey datum, not necessarily its top face.
        // Legacy saved patches have y=0; new patches retain Floor's local top.
        public Vector3 SurfacePoint=>transform.TransformPoint(new Vector3(0,triangles!=null && triangles.Length>0?triangles[0].y:0,0));
        GameObject visual;
        Mesh mesh;
        void OnEnable(){if(triangles!=null)Rebuild();}
        void OnDisable()=>Release();
        void Release()
        {
            if(visual){visual.SetActive(false);if(Application.isPlaying)Destroy(visual);else DestroyImmediate(visual);}
            if(mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
        }
        public void Rebuild()
        {
            Release();if(triangles==null)return;
            // Also sanitise legacy saved patches, not only newly generated tiles.
            var valid=new List<Vector3>();
            for(int i=0;i+2<triangles.Length;i+=3)
            {
                var a=triangles[i];var b=triangles[i+1];var c=triangles[i+2];
                var polygon=new[]{new Vector2(a.x,a.z),new Vector2(b.x,b.z),new Vector2(c.x,c.z)};
                float area=RoomGeometry.SignedArea(polygon);
                if(!RoomGeometry.HasTriangleSurface(polygon[0],polygon[1],polygon[2]))continue;
                valid.Add(a);valid.Add(area<0?c:b);valid.Add(area<0?b:c);
            }
            triangles=valid.ToArray();
            foreach(var renderer in GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            foreach(var collider in GetComponentsInChildren<Collider>(true))collider.enabled=false;
            if(triangles.Length==0)return;
            mesh=CreateMesh(triangles);
            visual=new GameObject("Room tile geometry"){hideFlags=HideFlags.DontSave,layer=gameObject.layer};
            visual.transform.SetParent(transform,false);
            visual.AddComponent<MeshFilter>().sharedMesh=mesh;
            var rendererNew=visual.AddComponent<MeshRenderer>();
            rendererNew.sharedMaterials=new[]{materials!=null && materials.Length>0?materials[0]:null,materials!=null && materials.Length>1?materials[1]:materials!=null && materials.Length>0?materials[0]:null};
            rendererNew.shadowCastingMode=ShadowCastingMode.TwoSided;
            visual.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        internal static Mesh CreateMesh(Vector3[] triangles)
        {
            var vertices=new List<Vector3>();var top=new List<int>();var underside=new List<int>();
            var edges=new Dictionary<(Vector3,Vector3),int>();
            void Face(Vector3 a,Vector3 b,Vector3 c,List<int> indices)
            {int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.Add(start);indices.Add(start+1);indices.Add(start+2);}
            for(int i=0;i+2<triangles.Length;i+=3)
            {
                var a=triangles[i];var b=triangles[i+1];var c=triangles[i+2];var down=Vector3.down*Thickness;
                Face(c,b,a,top);Face(a+down,b+down,c+down,underside);
                foreach(var edge in new[]{(a,b),(b,c),(c,a)})
                    if(edges.ContainsKey((edge.Item2,edge.Item1)))edges.Remove((edge.Item2,edge.Item1));else edges[edge]=1;
            }
            foreach(var edge in edges.Keys)
            {
                var a=edge.Item1;var b=edge.Item2;var down=Vector3.down*Thickness;
                Face(a,b,b+down,underside);Face(a,b+down,a+down,underside);
            }
            var mesh=new Mesh{name="Room tile (generated)",hideFlags=HideFlags.DontSave};
            mesh.SetVertices(vertices);mesh.subMeshCount=2;mesh.SetTriangles(top,0);mesh.SetTriangles(underside,1);
            var uv=new Vector2[vertices.Count];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(vertices[i].x,vertices[i].z);
            mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }
        public IEnumerable<List<Vector2>> PlanarPieces(Transform grid)
        {
            if(triangles==null)yield break;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var polygon=new List<Vector2>();
                for(int j=0;j<3;j++){var p=grid.InverseTransformPoint(transform.TransformPoint(triangles[i+j]));polygon.Add(RoomGeometry.Snap(new Vector2(p.x,p.z)));}
                float area=RoomGeometry.SignedArea(polygon);
                if(!RoomGeometry.HasTriangleSurface(polygon[0],polygon[1],polygon[2]))continue;
                if(area<0)polygon.Reverse();yield return polygon;
            }
        }
    }
}
