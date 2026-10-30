using System.Linq;
using System.Text;
using UnityEngine;

namespace JuegoCriminal.Construction
{
    // Mesh-only ghosts: no colliders, lights, scripts or persistent identities.
    public sealed class WallChangePreview
    {
        GameObject root;
        string signature;
        public void Hide(){if(root)root.SetActive(false);}
        static void Destroy(GameObject go){if(Application.isPlaying)Object.Destroy(go);else Object.DestroyImmediate(go);}
        public void Dispose(){if(root)Destroy(root);root=null;}
        public void Show(WallChangePlan plan,BuildCatalog catalog,Material material)
        {
            var key=new StringBuilder();
            foreach(var r in plan.added)key.Append(r.definitionId).Append(r.position.ToString("F3")).Append(r.rotation.ToString("F3")).Append(r.scale.ToString("F3"));
            string next=key.ToString();
            if(!root || signature!=next)
            {
                if(root){root.SetActive(false);Destroy(root);}
                root=new GameObject("WallChangePreview");root.layer=2;
                foreach(var record in plan.added)
                {
                    var prefab=catalog.Find(record.definitionId).prefab;
                    var child=new GameObject(prefab.name);child.transform.SetParent(root.transform,false);
                    child.transform.SetPositionAndRotation(record.position,record.rotation);child.transform.localScale=record.scale;
                    Clone(prefab.transform,child.transform);
                }
                signature=next;
            }
            root.SetActive(true);
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
        }
        static void Clone(Transform source,Transform target)
        {
            target.gameObject.layer=2;
            var renderer=source.GetComponent<Renderer>();
            if(renderer && renderer.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)return;
            var filter=source.GetComponent<MeshFilter>();
            if(filter && filter.sharedMesh)
            {
                target.gameObject.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                var mesh=target.gameObject.AddComponent<MeshRenderer>();mesh.sharedMaterials=new Material[Mathf.Max(1,filter.sharedMesh.subMeshCount)];
                mesh.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach(Transform sourceChild in source)
            {
                var child=new GameObject(sourceChild.name);child.transform.SetParent(target,false);
                child.transform.localPosition=sourceChild.localPosition;child.transform.localRotation=sourceChild.localRotation;child.transform.localScale=sourceChild.localScale;
                Clone(sourceChild,child.transform);
            }
        }
    }
}
