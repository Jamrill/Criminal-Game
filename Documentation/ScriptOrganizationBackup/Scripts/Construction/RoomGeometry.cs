using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JuegoCriminal.Construction
{
    // Planar wall graph, independent of physics, colliders and camera distance.
    // Splitting intersections also handles T/cross pieces and diagonal walls.
    public static class RoomGeometry
    {
        const float Epsilon=.001f;
        public const float MinimumArea=.000001f;
        public sealed class GeometryLimitException : InvalidOperationException
        {public GeometryLimitException():base("Room geometry exceeded its safe calculation budget.") {}}
        sealed class Budget
        {
            readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
            int work;
            public void Spend(int amount)
            {
                work+=amount;
                if(work>1000000 || clock.ElapsedMilliseconds>1000)throw new GeometryLimitException();
            }
        }
        public static List<Vector2> Clean(IList<Vector2> source)
        {
            var result=new List<Vector2>();
            foreach(var point in source)
            {
                if(float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y))throw new GeometryLimitException();
                if(result.Count==0 || (result[result.Count-1]-point).sqrMagnitude>1e-12f)result.Add(point);
            }
            if(result.Count>1 && (result[0]-result[result.Count-1]).sqrMagnitude<=1e-12f)result.RemoveAt(result.Count-1);
            return result;
        }
        public static Vector2 Snap(Vector2 p)=>new Vector2(Mathf.Round(p.x*1000)/1000,Mathf.Round(p.y*1000)/1000);
        public struct Edge
        {
            public Vector2 a,b;
            public Edge(Vector2 a,Vector2 b){this.a=a;this.b=b;}
        }
        public sealed class Region
        {
            public List<Vector2> outline;
            public readonly List<List<Vector2>> holes=new();
            public Vector2 min,max;
            public float Area=>SignedArea(outline)-holes.Sum(SignedArea);
            public bool Contains(Vector2 point)=>Inside(outline,point) && !holes.Any(h=>Inside(h,point));
            public Vector2 InteriorPoint
            {
                get
                {
                    var center=(min+max)*.5f;
                    if(Contains(center))return center;
                    // Try wide interior slices first; a concave room's AABB centre
                    // can be outside it. Never capture in an adjacent room.
                    var xs=outline.Concat(holes.SelectMany(h=>h)).Select(p=>p.x).Distinct().OrderBy(x=>x).ToArray();
                    var zs=outline.Concat(holes.SelectMany(h=>h)).Select(p=>p.y).Distinct().OrderBy(z=>z).ToArray();
                    float best=0;Vector2 result=outline[0];
                    for(int x=1;x<xs.Length;x++)for(int z=1;z<zs.Length;z++)
                    {
                        var p=new Vector2((xs[x]+xs[x-1])*.5f,(zs[z]+zs[z-1])*.5f);
                        float width=Mathf.Min(xs[x]-xs[x-1],zs[z]-zs[z-1]);
                        if(width>best && Contains(p)){best=width;result=p;}
                    }
                    if(best>0)return result;
                    for(int i=0;i<outline.Count;i++)
                    {
                        var a=outline[i];var b=outline[(i+1)%outline.Count];var d=(b-a).normalized;
                        var p=(a+b)*.5f+new Vector2(-d.y,d.x)*.1f;
                        if(Contains(p))return p;
                    }
                    return result;
                }
            }
            public string Key=>string.Join(";",outline.Select(p=>new Vector2Int(Mathf.RoundToInt(p.x*1000),Mathf.RoundToInt(p.y*1000)))
                .OrderBy(p=>p.x).ThenBy(p=>p.y).Select(p=>p.x+","+p.y));
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        public static float SignedArea(IList<Vector2> polygon)
        {double area=0;for(int i=0;i<polygon.Count;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Count];area+=(double)a.x*b.y-(double)a.y*b.x;}return (float)(area*.5);}
        public static bool Inside(IList<Vector2> polygon,Vector2 point)
        {
            bool inside=false;
            for(int i=0,j=polygon.Count-1;i<polygon.Count;j=i++)
            {
                var a=polygon[i];var b=polygon[j];
                if((a.y>point.y)!=(b.y>point.y) && point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
            }
            return inside;
        }
        public static List<Region> Find(IEnumerable<Edge> source)
        {
            var edges=source.Select(e=>new Edge(Snap(e.a),Snap(e.b))).Where(e=>(e.b-e.a).sqrMagnitude>Epsilon*Epsilon).ToArray();
            var points=new List<Vector2>();var neighbours=new Dictionary<int,HashSet<int>>();
            int Vertex(Vector2 p)
            {
                for(int i=0;i<points.Count;i++)if((points[i]-p).sqrMagnitude<Epsilon*Epsilon)return i;
                int index=points.Count;points.Add(p);neighbours[index]=new HashSet<int>();return index;
            }
            foreach(var edge in edges)
            {
                var d=edge.b-edge.a;var cuts=new List<float>{0,1};
                foreach(var other in edges)
                {
                    foreach(var p in new[]{other.a,other.b})
                    {
                        float t=Vector2.Dot(p-edge.a,d)/d.sqrMagnitude;
                        if(t>Epsilon && t<1-Epsilon && Vector2.Distance(p,edge.a+t*d)<Epsilon)cuts.Add(t);
                    }
                    var q=other.a-edge.a;var e=other.b-other.a;float determinant=Cross(d,e);
                    if(Mathf.Abs(determinant)<Epsilon)continue;
                    float u=Cross(q,d)/determinant,t0=Cross(q,e)/determinant;
                    if(u>=0 && u<=1 && t0>0 && t0<1)cuts.Add(t0);
                }
                cuts.Sort();
                for(int i=1;i<cuts.Count;i++)
                {
                    int a=Vertex(edge.a+cuts[i-1]*d),b=Vertex(edge.a+cuts[i]*d);
                    if(a==b)continue;neighbours[a].Add(b);neighbours[b].Add(a);
                }
            }
            var ordered=neighbours.ToDictionary(k=>k.Key,k=>k.Value.OrderBy(v=>Mathf.Atan2(points[v].y-points[k.Key].y,points[v].x-points[k.Key].x)).ToArray());
            var visited=new HashSet<(int,int)>();var result=new List<Region>();
            foreach(var pair in ordered)foreach(int end in pair.Value)
            {
                if(visited.Contains((pair.Key,end)))continue;
                int a=pair.Key,b=end;var polygon=new List<Vector2>();bool closed=false;
                for(int steps=0;steps<=edges.Length*edges.Length*4+4;steps++)
                {
                    if(!visited.Add((a,b))){closed=a==pair.Key && b==end;break;}
                    polygon.Add(points[a]);var next=ordered[b];int incoming=Array.IndexOf(next,a);
                    int c=next[(incoming+next.Length-1)%next.Length];a=b;b=c;
                }
                // Remove dead-end excursions (a,b,a) before computing the face.
                bool changed=true;
                while(changed && polygon.Count>=3)
                {
                    changed=false;
                    for(int i=0;i<polygon.Count;i++)
                        if(Vector2.Distance(polygon[(i+polygon.Count-1)%polygon.Count],polygon[(i+1)%polygon.Count])<Epsilon)
                        {polygon.RemoveAt(i);polygon.RemoveAt(i%polygon.Count);changed=true;break;}
                }
                if(!closed || polygon.Count<3 || SignedArea(polygon)<.01f)continue;
                result.Add(new Region{outline=polygon,min=new Vector2(polygon.Min(p=>p.x),polygon.Min(p=>p.y)),max=new Vector2(polygon.Max(p=>p.x),polygon.Max(p=>p.y))});
            }
            // Disconnected nested loops represent a room inside another room.
            // Subtract only immediate children, not the same hole repeatedly.
            foreach(var child in result)
            {
                var parent=result.Where(r=>r!=child && SignedArea(r.outline)>SignedArea(child.outline)+Epsilon && Inside(r.outline,child.InteriorPoint))
                    .OrderBy(r=>SignedArea(r.outline)).FirstOrDefault();
                if(parent!=null)parent.holes.Add(child.outline);
            }
            return result;
        }
        // Clip against each rectangular slab partition. A missing narrow strip or
        // a corner of a diagonal room must not pass a few point samples.
        public static float AreaInRectangle(Region region,Vector2 min,Vector2 max)
        {
            float ClipArea(List<Vector2> polygon)
            {
                var output=new List<Vector2>(polygon);
                for(int side=0;side<4;side++)
                {
                    var input=output;output=new List<Vector2>();if(input.Count==0)break;
                    float Distance(Vector2 p)=>side==0?p.x-min.x:side==1?max.x-p.x:side==2?p.y-min.y:max.y-p.y;
                    var a=input[input.Count-1];float da=Distance(a);
                    foreach(var b in input)
                    {
                        float db=Distance(b);
                        if((da>=0)!=(db>=0))output.Add(Vector2.LerpUnclamped(a,b,da/(da-db)));
                        if(db>=0)output.Add(b);a=b;da=db;
                    }
                }
                return output.Count<3?0:Mathf.Abs(SignedArea(output));
            }
            return Mathf.Max(0,ClipArea(region.outline)-region.holes.Sum(ClipArea));
        }
        public static bool Covered(Region region,IEnumerable<Rect> surfaces)
        {
            var rectangles=surfaces.Where(r=>r.xMax>region.min.x && r.xMin<region.max.x && r.yMax>region.min.y && r.yMin<region.max.y).ToArray();
            var xs=rectangles.SelectMany(r=>new[]{Mathf.Clamp(r.xMin,region.min.x,region.max.x),Mathf.Clamp(r.xMax,region.min.x,region.max.x)})
                .Concat(new[]{region.min.x,region.max.x}).Distinct().OrderBy(v=>v).ToArray();
            var zs=rectangles.SelectMany(r=>new[]{Mathf.Clamp(r.yMin,region.min.y,region.max.y),Mathf.Clamp(r.yMax,region.min.y,region.max.y)})
                .Concat(new[]{region.min.y,region.max.y}).Distinct().OrderBy(v=>v).ToArray();
            for(int x=1;x<xs.Length;x++)for(int z=1;z<zs.Length;z++)
            {
                var min=new Vector2(xs[x-1],zs[z-1]);var max=new Vector2(xs[x],zs[z]);
                if(AreaInRectangle(region,min,max)<.000001f)continue;
                var p=(min+max)*.5f;if(!rectangles.Any(r=>r.Contains(p)))return false;
            }
            return true;
        }
        public static List<Vector2> Clip(List<Vector2> polygon,Vector2 a,Vector2 b,bool inside=true)
        {
            var output=new List<Vector2>();if(polygon.Count==0)return output;
            // A zero-length edge is not a half-plane. Previously it copied the
            // entire remainder to BOTH inside and outside, duplicating geometry.
            if((b-a).sqrMagnitude<=1e-12f)return Clean(polygon);
            double Distance(Vector2 p)
            {
                double value=(((double)b.x-a.x)*(p.y-a.y)-((double)b.y-a.y)*(p.x-a.x))*(inside?1:-1);
                return Math.Abs(value)<.000001f*(b-a).magnitude?0:value;
            }
            var previous=polygon[polygon.Count-1];double da=Distance(previous);
            foreach(var point in polygon)
            {
                double db=Distance(point);
                if((da>=0)!=(db>=0))
                {
                    double fraction=(double)da/(da-db);
                    var intersection=new Vector2((float)(previous.x+((double)point.x-previous.x)*fraction),(float)(previous.y+((double)point.y-previous.y)*fraction));
                    // Exact coordinates on modular tile edges avoid accumulating
                    // false hairline gaps after subtracting many neighbouring tiles.
                    if(a.x==b.x)intersection.x=a.x;if(a.y==b.y)intersection.y=a.y;
                    if(previous.x==point.x)intersection.x=previous.x;if(previous.y==point.y)intersection.y=previous.y;
                    output.Add(intersection);
                }
                if(db>=0)output.Add(point);previous=point;da=db;
            }
            return Clean(output);
        }
        public static List<List<Vector2>> Subtract(List<List<Vector2>> source,IList<Vector2> convex)
            =>Subtract(source,convex,new Budget());
        static List<List<Vector2>> Subtract(List<List<Vector2>> source,IList<Vector2> convex,Budget budget)
        {
            budget.Spend(convex.Count);
            if(source.Count>4096 || convex.Count>4096)throw new GeometryLimitException();
            var cutter=Clean(convex);float area=SignedArea(cutter);
            // Old saves can contain collapsed fan triangles. They remove no
            // surface, so must never participate in polygon subtraction.
            if(cutter.Count<3 || Mathf.Abs(area)<=MinimumArea)return source;
            if(area<0)cutter.Reverse();convex=cutter;
            float minX=cutter.Min(p=>p.x),maxX=cutter.Max(p=>p.x),minY=cutter.Min(p=>p.y),maxY=cutter.Max(p=>p.y);
            var result=new List<List<Vector2>>();
            foreach(var polygon in source)
            {
                budget.Spend(polygon.Count);
                if(polygon.Count<3 || Mathf.Abs(SignedArea(polygon))<=MinimumArea)continue;
                if(polygon.Max(p=>p.x)<=minX || polygon.Min(p=>p.x)>=maxX ||
                    polygon.Max(p=>p.y)<=minY || polygon.Min(p=>p.y)>=maxY)
                {result.Add(polygon);continue;}
                var remainder=polygon;
                for(int i=0;i<convex.Count && remainder.Count>=3;i++)
                {
                    var a=convex[i];var b=convex[(i+1)%convex.Count];
                    budget.Spend(remainder.Count);
                    var outside=Clip(remainder,a,b,false);
                    if(outside.Count>=3 && Mathf.Abs(SignedArea(outside))>MinimumArea)result.Add(outside);
                    if(result.Count>4096)throw new GeometryLimitException();
                    remainder=Clip(remainder,a,b);
                }
            }
            return result;
        }
        public static List<List<Vector2>> Triangulate(IList<Vector2> source)
        {
            if(source.Count>4096)throw new GeometryLimitException();
            var polygon=Clean(source);var result=new List<List<Vector2>>();
            if(polygon.Count<3 || Mathf.Abs(SignedArea(polygon))<=MinimumArea)return result;
            if(SignedArea(polygon)<0)polygon.Reverse();
            // Collinear graph splits are useful for connectivity, not for ears.
            for(int i=polygon.Count-1;i>=0 && polygon.Count>3;i--)
                if(Mathf.Abs(Cross(polygon[i]-polygon[(i+polygon.Count-1)%polygon.Count],polygon[(i+1)%polygon.Count]-polygon[i]))<.00001f)polygon.RemoveAt(i);
            while(polygon.Count>3)
            {
                bool found=false;
                for(int i=0;i<polygon.Count;i++)
                {
                    int prev=(i+polygon.Count-1)%polygon.Count,next=(i+1)%polygon.Count;
                    var a=polygon[prev];var b=polygon[i];var c=polygon[next];
                    if(Cross(b-a,c-b)<=.00001f)continue;
                    bool occupied=false;
                    for(int j=0;j<polygon.Count;j++)
                        if(j!=prev && j!=i && j!=next && Cross(b-a,polygon[j]-a)>=-.00001f && Cross(c-b,polygon[j]-b)>=-.00001f && Cross(a-c,polygon[j]-c)>=-.00001f){occupied=true;break;}
                    if(occupied)continue;
                    result.Add(new List<Vector2>{a,b,c});polygon.RemoveAt(i);found=true;break;
                }
                if(!found)throw new InvalidOperationException("Non-simple room outline; refusing to create an overlapping floor.");
            }
            if(polygon.Count==3)result.Add(polygon);
            return result;
        }
        public static List<List<Vector2>> Missing(Region region,IEnumerable<List<Vector2>> existing)
        {
            var budget=new Budget();
            var parts=Triangulate(region.outline);
            foreach(var hole in region.holes)foreach(var triangle in Triangulate(hole))parts=Subtract(parts,triangle,budget);
            foreach(var polygon in existing)
            {if(parts.Count==0)break;parts=Subtract(parts,polygon,budget);}
            return parts;
        }
    }
}
