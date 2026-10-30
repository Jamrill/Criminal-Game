using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JuegoCriminal.Construction
{
    public static class WallGeometry
    {
        public struct Span
        {
            public Vector3 a,b;
            public Span(Vector3 a,Vector3 b){this.a=a;this.b=b;}
            public float Length=>Vector3.Distance(a,b);
            public bool Same(Span other)=>Near(a,other.a)&&Near(b,other.b) || Near(a,other.b)&&Near(b,other.a);
            public float Distance(Vector3 point)
            {
                point.y=a.y;
                return Distance3D(point);
            }
            // Selection projects a hit onto a wall's base. Structural coverage
            // must NOT do that: a lower-storey wall cannot fill an upper gap.
            public float Distance3D(Vector3 point)=>Vector3.Distance(point,
                (b-a).sqrMagnitude<1e-12f?a:Vector3.Lerp(a,b,Mathf.Clamp01(Vector3.Dot(point-a,b-a)/(b-a).sqrMagnitude)));
        }
        public static bool Near(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<.06f;
        public static List<Span> Local(WallTopology wall)
        {
            var result=new List<Span>();
            if(!wall)return result;
            var points=wall.segmentPoints;
            if(points!=null && points.Length>=2)
            {
                for(int i=1;i<points.Length;i+=2)result.Add(new Span(points[i-1],points[i]));
                return result;
            }
            // Compatibility for authored scenes which still have an added-component
            // override. New prefab metadata explicitly describes the same geometry.
            var ends=wall.connections;
            if(ends==null || ends.Length<2)return result;
            if(ends.Length>2)
            {
                var xs=ends.Select(p=>p.x).OrderBy(x=>x).ToArray();var zs=ends.Select(p=>p.z).OrderBy(z=>z).ToArray();
                var joint=new Vector3(xs[xs.Length/2],ends[0].y,zs[zs.Length/2]);
                foreach(var end in ends)result.Add(new Span(joint,end));
            }
            else if(wall.name.StartsWith("Corner",System.StringComparison.OrdinalIgnoreCase))
            {
                var joint=new Vector3(ends[1].x,ends[0].y,ends[0].z);
                result.Add(new Span(ends[0],joint));result.Add(new Span(joint,ends[1]));
            }
            else result.Add(new Span(ends[0],ends[1]));
            return result;
        }
        public static List<Span> At(WallTopology wall,Vector3 position,Quaternion rotation,Vector3 scale)
            =>Local(wall).Select(s=>new Span(position+rotation*Vector3.Scale(s.a,scale),position+rotation*Vector3.Scale(s.b,scale))).ToList();
        public static List<Span> World(WallTopology wall)=>Local(wall).Select(s=>new Span(wall.transform.TransformPoint(s.a),wall.transform.TransformPoint(s.b))).ToList();
        public static IEnumerable<Vector3> Contacts(WallTopology wall)=>Local(wall).SelectMany(s=>new[]{s.a,s.b});
        public static bool Solid(BuildEntry entry)=>entry!=null && entry.prefab && !entry.IsOpening &&
            !entry.prefab.name.StartsWith("Glass",System.StringComparison.OrdinalIgnoreCase) && entry.prefab.GetComponent<WallTopology>();
        public static string Family(BuildEntry entry)
        {
            if(entry==null || !entry.prefab)return "";
            string name=entry.prefab.name.ToLowerInvariant();
            if(name.StartsWith("glass_wall_door"))return "glass-door";
            if(name.StartsWith("glass"))return "glass";
            if(name.StartsWith("window"))return "window";
            if(name.StartsWith("wall_door_hole"))return "doorway";
            return Solid(entry)?"solid":name;
        }
        public static bool CoveredAbove(WallTopology wall,IEnumerable<WallTopology> walls)
        {
            var above=walls.Where(w=>w && w.isActiveAndEnabled && w!=wall && w.gameObject.scene==wall.gameObject.scene &&
                Mathf.Abs(Vector3.Dot(w.Bottom-wall.Bottom,wall.transform.up)-wall.WorldHeight)<.06f).SelectMany(World).ToArray();
            var spans=World(wall);
            if(spans.Count==0)return false;
            foreach(var span in spans)
            {
                Vector3 a=span.a+wall.transform.up*wall.WorldHeight, direction=(span.b-span.a).normalized;
                var intervals=new List<Vector2>();
                foreach(var cover in above)
                {
                    if(Vector3.Cross(cover.a-a,direction).magnitude>.06f || Vector3.Cross(cover.b-a,direction).magnitude>.06f)continue;
                    float t0=Vector3.Dot(cover.a-a,direction),t1=Vector3.Dot(cover.b-a,direction);
                    intervals.Add(new Vector2(Mathf.Min(t0,t1),Mathf.Max(t0,t1)));
                }
                float end=0;
                foreach(var interval in intervals.OrderBy(i=>i.x))
                {
                    if(interval.x>end+.06f)break;
                    end=Mathf.Max(end,interval.y);
                }
                if(end<span.Length-.06f)return false;
            }
            return true;
        }
    }
}
