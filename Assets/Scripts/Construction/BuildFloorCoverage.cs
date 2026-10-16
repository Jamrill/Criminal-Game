using System.Collections.Generic;
using UnityEngine;
namespace JuegoCriminal.Construction
{
    // Explicit floor marker: furniture above a wall must not remove its top cap.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BuildFloorCoverage : MonoBehaviour
    {
        public static readonly HashSet<BuildFloorCoverage> Active=new HashSet<BuildFloorCoverage>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry()=>Active.Clear();
        void OnEnable(){ Active.Add(this);WallTopology.MarkDirty(); }
        void OnDisable(){ Active.Remove(this);WallTopology.MarkDirty(); }
        void LateUpdate(){ if(transform.hasChanged){ transform.hasChanged=false;WallTopology.MarkDirty(); } }

        public static bool Covers(WallTopology wall)
        {
            bool tested=false;
            foreach(var box in wall.GetComponentsInChildren<BoxCollider>())
            {
                if(!box.enabled || box.isTrigger)continue;
                var top=box.transform.TransformPoint(box.center+Vector3.up*box.size.y*.5f);
                if(Mathf.Abs(Vector3.Dot(top-wall.Bottom,wall.transform.up)-wall.WorldHeight)>.06f)continue;
                tested=true;
                var min=box.center-box.size*.5f;var max=box.center+box.size*.5f;
                var regions=new List<Bounds>();var xs=new List<float>{min.x,max.x};var zs=new List<float>{min.z,max.z};
                foreach(var floor in Active)
                {
                    if(!floor || floor.gameObject.scene!=wall.gameObject.scene)continue;
                    foreach(var slab in floor.GetComponentsInChildren<BoxCollider>())
                    {
                        if(!slab.enabled || slab.isTrigger)continue;
                        var m=box.transform.worldToLocalMatrix*slab.transform.localToWorldMatrix;
                        // Modular floors are aligned with the grid (including quarter turns).
                        var right=m.MultiplyVector(Vector3.right).normalized;
                        var up=m.MultiplyVector(Vector3.up).normalized;
                        if(Mathf.Abs(up.y)<.999f || Mathf.Max(Mathf.Abs(right.x),Mathf.Abs(right.z))<.999f)continue;
                        var low=slab.center-slab.size*.5f;var high=slab.center+slab.size*.5f;
                        var b=new Bounds(m.MultiplyPoint3x4(low),Vector3.zero);
                        foreach(float x in new[]{low.x,high.x})foreach(float y in new[]{low.y,high.y})foreach(float z in new[]{low.z,high.z})b.Encapsulate(m.MultiplyPoint3x4(new Vector3(x,y,z)));
                        if(b.min.y>max.y+.06f || b.max.y<max.y-.06f)continue;
                        regions.Add(b);
                        xs.Add(Mathf.Clamp(b.min.x,min.x,max.x));xs.Add(Mathf.Clamp(b.max.x,min.x,max.x));
                        zs.Add(Mathf.Clamp(b.min.z,min.z,max.z));zs.Add(Mathf.Clamp(b.max.z,min.z,max.z));
                    }
                }
                xs.Sort();zs.Sort();
                // Partition at every slab edge: partial coverage and gaps remain capped.
                for(int x=1;x<xs.Count;x++)for(int z=1;z<zs.Count;z++)
                {
                    if(xs[x]-xs[x-1]<.0001f || zs[z]-zs[z-1]<.0001f)continue;
                    float px=(xs[x]+xs[x-1])*.5f,pz=(zs[z]+zs[z-1])*.5f;
                    if(!regions.Exists(b=>px>=b.min.x && px<=b.max.x && pz>=b.min.z && pz<=b.max.z))return false;
                }
            }
            return tested;
        }
    }
}
