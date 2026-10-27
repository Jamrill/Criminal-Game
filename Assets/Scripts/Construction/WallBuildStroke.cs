using System.Collections.Generic;
using UnityEngine;

namespace JuegoCriminal.Construction
{
    // Fill consecutive authored wall spans, never arbitrary one-unit overlapping
    // previews. Commit only after a successful paid placement.
    public sealed class WallBuildStroke
    {
        readonly HashSet<int> placed=new();
        Vector3 origin,step,up;
        public bool Active {get;private set;}
        public BuildPlot Plot {get;private set;}
        public void Reset(){Active=false;Plot=null;placed.Clear();}
        public bool Begin(BuildEntry entry,BuildPlot plot,Vector3 position,Quaternion rotation)
        {
            Reset();if(entry.IsOpening)return false;
            var spans=WallGeometry.Local(entry.prefab.GetComponent<WallTopology>());
            if(spans.Count!=1)return false;
            step=rotation*Vector3.Scale(spans[0].b-spans[0].a,entry.prefab.transform.localScale);
            if(step.sqrMagnitude<.01f)return false;
            origin=position;up=plot.Grid.up;placed.Add(0);Plot=plot;Active=true;return true;
        }
        public Vector3 Aim(Ray ray,Vector3 fallback)
        {
            if(new Plane(up,origin).Raycast(ray,out float distance) && distance<=100)return ray.GetPoint(distance);
            return fallback-up*Vector3.Dot(fallback-origin,up);
        }
        public bool Next(Vector3 desired,out Vector3 position,out int index)
        {
            index=Mathf.Clamp(Mathf.RoundToInt(Vector3.Dot(desired-origin,step)/step.sqrMagnitude),-4096,4096);
            int target=index,sign=target<0?-1:1;
            for(int i=sign;sign>0?i<=target:i>=target;i+=sign)
                if(!placed.Contains(i)){index=i;position=origin+step*i;return true;}
            position=origin+step*index;return false;
        }
        public void Commit(int index)=>placed.Add(index);
    }
}
