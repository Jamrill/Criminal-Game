using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace JuegoCriminal.Construction
{
    // Shared visual-only preview for placement, wall strokes and replacements.
    // Cache renderers/slots once; unchanged tint updates allocate nothing.
    internal sealed class MeshPreview : IDisposable
    {
        public GameObject Root { get; private set; }
        readonly MeshRenderer[] renderers;
        readonly Material[][] slots;
        Material currentMaterial;
        bool assigned;
        Mesh ownedMesh;
        internal static MeshPreview FromTriangles(BuildRecord record,Vector3[] triangles,Material material,string name,bool overlay=false)
        {
            var mesh=RoomFloorPatch.CreateMesh(triangles);
            if(overlay)
            {
                var vertices=mesh.vertices;var normals=mesh.normals;
                for(int i=0;i<vertices.Length;i++)vertices[i]+=normals[i]*.002f;
                mesh.vertices=vertices;mesh.RecalculateBounds();
            }
            var root=new GameObject(name){layer=2,hideFlags=HideFlags.DontSave};
            root.transform.SetPositionAndRotation(record.position,record.rotation);root.transform.localScale=record.scale;
            root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            var preview=new MeshPreview(root){ownedMesh=mesh};preview.SetMaterial(material);return preview;
        }

        MeshPreview(GameObject root)
        {
            Root=root;
            renderers=root.GetComponentsInChildren<MeshRenderer>(true);
            slots=new Material[renderers.Length][];
            for(int i=0;i<renderers.Length;i++)
                slots[i]=new Material[Mathf.Max(1,renderers[i].GetComponent<MeshFilter>().sharedMesh.subMeshCount)];
        }

        public static MeshPreview Create(GameObject prefab,Vector3 position,Quaternion rotation,Vector3 scale,Material material,string name,Transform parent=null)
        {
            var root=new GameObject(name);
            root.SetActive(false);
            root.transform.SetParent(parent,false);
            root.transform.SetPositionAndRotation(position,rotation);
            root.transform.localScale=scale;
            Clone(prefab.transform,root.transform);
            var preview=new MeshPreview(root);
            preview.SetMaterial(material);
            root.SetActive(true);
            return preview;
        }

        public void SetMaterial(Material material)
        {
            if(assigned && currentMaterial==material)return;
            currentMaterial=material;assigned=true;
            for(int i=0;i<renderers.Length;i++)
            {
                if(!renderers[i])continue;
                for(int j=0;j<slots[i].Length;j++)slots[i][j]=material;
                renderers[i].sharedMaterials=slots[i];
            }
        }

        static void Clone(Transform source,Transform target)
        {
            target.gameObject.layer=2;
            var sourceRenderer=source.GetComponent<Renderer>();
            if(sourceRenderer && sourceRenderer.shadowCastingMode==ShadowCastingMode.ShadowsOnly)return;
            var mesh=source.GetComponent<MeshFilter>();
            if(mesh && mesh.sharedMesh)
            {
                target.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
                target.gameObject.AddComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            }
            foreach(Transform child in source)
            {
                var next=new GameObject(child.name).transform;
                next.SetParent(target,false);
                next.localPosition=child.localPosition;next.localRotation=child.localRotation;next.localScale=child.localScale;
                Clone(child,next);
            }
        }

        public void Dispose()
        {
            if(Root)
            {
                Root.SetActive(false);
                if(Application.isPlaying)UnityEngine.Object.Destroy(Root);else UnityEngine.Object.DestroyImmediate(Root);
            }
            Root=null;
            if(ownedMesh){if(Application.isPlaying)UnityEngine.Object.Destroy(ownedMesh);else UnityEngine.Object.DestroyImmediate(ownedMesh);ownedMesh=null;}
        }
    }
}
