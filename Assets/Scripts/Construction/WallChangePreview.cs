using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace JuegoCriminal.Construction
{
    // Mesh-only ghosts: no colliders, lights, scripts or persistent identities.
    public sealed class WallChangePreview
    {
        GameObject root;
        readonly List<MeshPreview> previews=new();
        string signature;
        public void Hide(){if(root)root.SetActive(false);}
        static void Destroy(GameObject go){if(Application.isPlaying)Object.Destroy(go);else Object.DestroyImmediate(go);}
        public void Dispose(){if(root){root.SetActive(false);Destroy(root);}root=null;previews.Clear();}
        public void Show(WallChangePlan plan,BuildCatalog catalog,Material material)
        {
            var key=new StringBuilder(material?"tint":"original");
            foreach(var r in plan.added)key.Append(r.definitionId).Append(r.position.ToString("F3")).Append(r.rotation.ToString("F3")).Append(r.scale.ToString("F3")).Append(r.finish);
            string next=key.ToString();
            if(!root || signature!=next)
            {
                Dispose();
                root=new GameObject("WallChangePreview");root.layer=2;
                foreach(var record in plan.added)
                {
                    var prefab=catalog.Find(record.definitionId).prefab;
                    var paint=material?material:record.finish>=0&&record.finish<catalog.finishes.Length?catalog.finishes[record.finish]:null;
                    previews.Add(MeshPreview.Create(prefab,record.position,record.rotation,record.scale,paint,prefab.name,root.transform));
                }
                signature=next;
            }
            root.SetActive(true);
            if(material)foreach(var preview in previews)preview.SetMaterial(material);
        }
    }
}
