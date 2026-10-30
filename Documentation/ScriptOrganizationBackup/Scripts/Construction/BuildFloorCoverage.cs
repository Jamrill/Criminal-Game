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
            try{return CoversGeometry(wall);}
            catch(RoomGeometry.GeometryLimitException){return false;} // Keep the cap when coverage cannot be proved safely.
        }
        static bool CoversGeometry(WallTopology wall)
        {
            bool tested=false;
            foreach(var box in wall.GetComponentsInChildren<BoxCollider>())
            {
                if(!box.enabled || box.isTrigger)continue;
                var top=box.transform.TransformPoint(box.center+Vector3.up*box.size.y*.5f);
                if(Mathf.Abs(Vector3.Dot(top-wall.Bottom,wall.transform.up)-wall.WorldHeight)>.06f)continue;
                tested=true;
                var min=box.center-box.size*.5f;var max=box.center+box.size*.5f;
                var pieces=new List<List<Vector2>>();
                foreach(var floor in Active)
                {
                    if(!floor || !floor.isActiveAndEnabled || floor.gameObject.scene!=wall.gameObject.scene)continue;
                    var patch=floor.GetComponent<RoomFloorPatch>();
                    if(patch)
                    {
                        float patchTop=box.transform.InverseTransformPoint(patch.transform.position).y;
                        if(patchTop>=max.y-.06f && patchTop-RoomFloorPatch.Thickness<=max.y+.06f)pieces.AddRange(patch.PlanarPieces(box.transform));
                        continue;
                    }
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
                        pieces.Add(new List<Vector2>{new Vector2(b.min.x,b.min.z),new Vector2(b.max.x,b.min.z),new Vector2(b.max.x,b.max.z),new Vector2(b.min.x,b.max.z)});
                    }
                }
                var region=new RoomGeometry.Region{min=new Vector2(min.x,min.z),max=new Vector2(max.x,max.z),outline=new List<Vector2>{new Vector2(min.x,min.z),new Vector2(max.x,min.z),new Vector2(max.x,max.z),new Vector2(min.x,max.z)}};
                if(RoomGeometry.Missing(region,pieces).Count>0)return false;
            }
            return tested;
        }
    }
}
