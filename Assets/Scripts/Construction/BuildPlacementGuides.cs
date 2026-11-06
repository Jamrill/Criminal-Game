using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace JuegoCriminal.Construction
{
    [Serializable]
    public sealed class BuildGuideColors
    {
        public Color grid=new(1,1,1,.22f);
        public Color validOutline=new(.3f,1,.4f,.95f);
        public Color invalidOutline=new(1,.18f,.18f,.95f);
        public Color validFill=new(.15f,.95f,.25f,.22f);
        public Color invalidFill=new(1,.12f,.12f,.22f);
        public Color validArrow=new(.3f,1,.4f,1);
        public Color invalidArrow=new(1,.18f,.18f,1);
        public Color alignment=new(1,1,1,.9f);
    }
    // One transient mesh, not hundreds of colliders or LineRenderer objects.
    public sealed class BuildPlacementGuides : IDisposable
    {
        struct Segment {public Vector3 a,b;public Segment(Vector3 a,Vector3 b){this.a=a;this.b=b;}}
        readonly GameObject root;
        readonly Mesh mesh;
        readonly Material[] materials;
        readonly List<Vector3> vertices=new();
        readonly List<int>[] indices={new(),new(),new(),new(),new()};
        readonly BuildGuideColors defaultColors=new();
        readonly List<Segment> grid=new();
        BuildPlot cachedPlot;
        Vector3 cachedAnchor;
        int cachedRadius;
        bool cachedCeiling;
        float refreshedAt=-1;
        public int GridSegments {get;private set;}
        public int FootprintSegments {get;private set;}
        public int AlignmentSegments {get;private set;}
        public bool HasFrontArrow {get;private set;}
        public int AccessArrowCount {get;private set;}
        public bool Visible=>root && root.activeSelf;
        public BuildPlacementGuides()
        {
            root=new GameObject("ConstructionPlacementGuides",typeof(MeshFilter),typeof(MeshRenderer));root.layer=2;root.hideFlags=HideFlags.DontSave;
            mesh=new Mesh{name="Construction guide lines"};mesh.MarkDynamic();root.GetComponent<MeshFilter>().sharedMesh=mesh;
            materials=new[]{Material(Color.white,3100),Material(Color.white,3110),Material(Color.white,3120),Material(Color.white,3090),Material(Color.white,3130)};
            var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            root.SetActive(false);
        }
        static Material Material(Color color,int queue)
        {
            var result=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Construction guide",renderQueue=queue};
            result.SetColor("_BaseColor",color);result.SetFloat("_Surface",1);result.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);result.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            result.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);result.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
            result.SetFloat("_ZWrite",0);result.SetFloat("_Cull",(float)CullMode.Off);result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            result.SetOverrideTag("RenderType","Transparent");result.SetShaderPassEnabled("ShadowCaster",false);return result;
        }
        public void Hide(){if(root)root.SetActive(false);}
        void Line(Vector3 a,Vector3 b,Vector3 normal,float width,int submesh)
        {
            if((a-b).sqrMagnitude<.0001f)return;
            var side=Vector3.Cross(normal,b-a).normalized*width*.5f;int start=vertices.Count;
            vertices.Add(a-side);vertices.Add(a+side);vertices.Add(b+side);vertices.Add(b-side);
            var triangles=indices[submesh];triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);triangles.Add(start);triangles.Add(start+2);triangles.Add(start+3);
        }
        void Polygon(Vector3[] points,int submesh)
        {
            int start=vertices.Count;vertices.AddRange(points);
            for(int i=1;i<points.Length-1;i++){indices[submesh].Add(start);indices[submesh].Add(start+i);indices[submesh].Add(start+i+1);}
        }
        internal static Rect Footprint(BuildEntry entry,BuildPlot plot,Vector3 position,Quaternion rotation)
        {
            if(BuildFootprint.Uses(entry))return BuildFootprint.RectOnGrid(entry,plot,position,rotation);
            var b=entry.bounds;
            var rect=StairConstruction.IsStairs(entry)?StairConstruction.LocalFootprint(entry):Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z);
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            foreach(float x in new[]{rect.xMin,rect.xMax})foreach(float z in new[]{rect.yMin,rect.yMax})
            {
                var p=plot.Grid.InverseTransformPoint(position+rotation*new Vector3(x,0,z));var v=new Vector2(p.x,p.z);
                min=Vector2.Min(min,v);max=Vector2.Max(max,v);
            }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        internal static List<List<Vector2>> WallShape(IEnumerable<WallGeometry.Span> spans,Transform grid,out List<(Vector2 a,Vector2 b)> contour)
        {
            var polygons=new List<List<Vector2>>();var nodes=new List<Vector2>();
            foreach(var span in spans)
            {
                var a=grid.InverseTransformPoint(span.a);var b=grid.InverseTransformPoint(span.b);
                var start=new Vector2(a.x,a.z);var end=new Vector2(b.x,b.z);var dir=(end-start).normalized;
                var side=new Vector2(-dir.y,dir.x)*.25f;
                polygons.Add(new List<Vector2>{start-side,end-side,end+side,start+side});nodes.Add(start);nodes.Add(end);
            }
            // Fill the outside quadrant of shared elbows, then trace the union.
            // No overlapping alpha fill and no end-cap lines through the joint.
            var joints=new List<Vector2>();
            foreach(var p in nodes)
                if(nodes.Count(n=>(n-p).sqrMagnitude<.0001f)>1&&!joints.Any(n=>(n-p).sqrMagnitude<.0001f))
                {joints.Add(p);polygons.Add(new List<Vector2>{p+new Vector2(-.25f,-.25f),p+new Vector2(.25f,-.25f),p+new Vector2(.25f,.25f),p+new Vector2(-.25f,.25f)});}
            var fill=new List<List<Vector2>>();
            for(int i=0;i<polygons.Count;i++)
            {
                var pieces=new List<List<Vector2>>{polygons[i]};
                for(int j=0;j<i;j++)pieces=RoomGeometry.Subtract(pieces,polygons[j]);
                fill.AddRange(pieces);
            }
            contour=new();
            float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
            bool Inside(Vector2 p)=>polygons.Any(poly=>RoomGeometry.Inside(poly,p));
            foreach(var poly in polygons)for(int i=0;i<poly.Count;i++)
            {
                var a=poly[i];var b=poly[(i+1)%poly.Count];var d=b-a;var cuts=new List<float>{0,1};
                foreach(var other in polygons)for(int j=0;j<other.Count;j++)
                {
                    var c=other[j];var e=other[(j+1)%other.Count]-c;float cross=Cross(d,e);
                    if(Mathf.Abs(cross)>.000001f)
                    {
                        float t=Cross(c-a,e)/cross,u=Cross(c-a,d)/cross;
                        if(t>0&&t<1&&u>=0&&u<=1)cuts.Add(t);
                    }
                    else if(Mathf.Abs(Cross(c-a,d))<.00001f)
                        foreach(var p in new[]{c,c+e}){float t=Vector2.Dot(p-a,d)/d.sqrMagnitude;if(t>0&&t<1)cuts.Add(t);}
                }
                cuts.Sort();var normal=new Vector2(-d.y,d.x).normalized*.002f;
                for(int j=1;j<cuts.Count;j++)
                {
                    var start=a+d*cuts[j-1];var end=a+d*cuts[j];if((end-start).sqrMagnitude<.000001f)continue;
                    var mid=(start+end)*.5f;if(Inside(mid+normal)==Inside(mid-normal))continue;
                    if(contour.Any(s=>((s.a-start).sqrMagnitude<.000001f&&(s.b-end).sqrMagnitude<.000001f)||((s.b-start).sqrMagnitude<.000001f&&(s.a-end).sqrMagnitude<.000001f)))continue;
                    contour.Add((start,end));
                }
            }
            return fill;
        }
        static bool Supported(BuildPlot plot,Vector3 point,bool ceiling,Transform player)
        {
            if(!plot.Contains(point))return false;
            var normal=ceiling?-plot.Grid.up:plot.Grid.up;
            // Vertices lie on tile seams. A few centimetres of lateral tolerance
            // keep the grid visible across tiny authored gaps, not across holes.
            for(int sample=0;sample<5;sample++)
            {
                var shift=sample==0?Vector3.zero:(plot.Grid.right*(((sample-1)&1)==0?-.035f:.035f)+plot.Grid.forward*(((sample-1)&2)==0?-.035f:.035f));
                foreach(var hit in Physics.RaycastAll(point+shift+normal*.15f,-normal,.3f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                {
                    if(player && hit.collider.transform.IsChildOf(player))continue;
                    if(Vector3.Dot(hit.normal,normal)<.7f || Mathf.Abs(Vector3.Dot(hit.point-point,normal))>.04f)continue;
                    if(ceiling)
                    {
                        var floor=hit.collider.GetComponentInParent<BuildFloorCoverage>();
                        if(!floor || floor.GetComponentInParent<BuildPlot>()!=plot)continue;
                    }
                    return true;
                }
            }
            return false;
        }
        void RebuildGrid(BuildPlot plot,Vector3 anchor,bool ceiling,int radius,Transform player)
        {
            var local=plot.Grid.InverseTransformPoint(anchor);
            var cell=plot.FurnitureCell(anchor);var origin=plot.FurnitureGridOffset;
            var key=plot.Grid.TransformPoint(new Vector3(origin.x+cell.x*4,local.y,origin.y+cell.y*4));
            if(cachedPlot==plot && cachedRadius==radius && cachedCeiling==ceiling && (cachedAnchor-key).sqrMagnitude<.0001f && Time.unscaledTime-refreshedAt<.25f)return;
            grid.Clear();cachedPlot=plot;cachedAnchor=key;cachedRadius=radius;cachedCeiling=ceiling;refreshedAt=Time.unscaledTime;
            // Sampling is cached by vertex/edge midpoint during this rebuild.
            var supports=new Dictionary<Vector2,bool>();
            Vector3 Point(float x,float z)=>plot.Grid.TransformPoint(new Vector3(origin.x+x,local.y,origin.y+z));
            bool Has(float x,float z)
            {
                var p=new Vector2(x,z);if(!supports.TryGetValue(p,out bool yes))supports[p]=yes=Supported(plot,Point(x,z),ceiling,player);return yes;
            }
            void Edge(float x,float z,float endX,float endZ)
            {
                if(Has(x,z) && Has(endX,endZ) && Has((x+endX)*.5f,(z+endZ)*.5f))grid.Add(new Segment(Point(x,z),Point(endX,endZ)));
            }
            for(int x=cell.x-radius;x<=cell.x+radius;x++)for(int z=cell.y-radius;z<=cell.y+radius;z++)
            {
                Edge(x*4,z*4,(x+1)*4,z*4);Edge(x*4,z*4,x*4,(z+1)*4);
                if(x==cell.x+radius)Edge((x+1)*4,z*4,(x+1)*4,(z+1)*4);
                if(z==cell.y+radius)Edge(x*4,(z+1)*4,(x+1)*4,(z+1)*4);
            }
        }
        public static bool TryAlignment(Rect a,Rect b,out Vector2 start,out Vector2 end)
        {
            bool Match(float lowA,float highA,float lowB,float highB,out float value)
            {
                if(Mathf.Abs(lowA-lowB)<.06f){value=(lowA+lowB)*.5f;return true;}
                if(Mathf.Abs(highA-highB)<.06f){value=(highA+highB)*.5f;return true;}
                value=(lowA+highA+lowB+highB)*.25f;return Mathf.Abs((lowA+highA)-(lowB+highB))<.12f;
            }
            if((a.xMax<=b.xMin+.01f || b.xMax<=a.xMin+.01f) && Match(a.yMin,a.yMax,b.yMin,b.yMax,out float z))
            {start=new Vector2(a.center.x,z);end=new Vector2(b.center.x,z);return true;}
            if((a.yMax<=b.yMin+.01f || b.yMax<=a.yMin+.01f) && Match(a.xMin,a.xMax,b.xMin,b.xMax,out float x))
            {start=new Vector2(x,a.center.y);end=new Vector2(x,b.center.y);return true;}
            start=end=default;return false;
        }
        public void Show(BuildEntry entry,BuildCatalog catalog,BuildPlot plot,Vector3 position,Quaternion rotation,Vector3 anchor,bool valid,Transform player,int radius,float alignmentRange,BuildGuideColors colors=null)
        {
            colors??=defaultColors;
            bool ceiling=entry.mount==BuildMount.Ceiling;var normal=ceiling?-plot.Grid.up:plot.Grid.up;
            RebuildGrid(plot,anchor,ceiling,Mathf.Clamp(radius,1,8),player);
            vertices.Clear();foreach(var list in indices)list.Clear();GridSegments=grid.Count;FootprintSegments=AlignmentSegments=AccessArrowCount=0;HasFrontArrow=false;
            var offset=normal*.025f;
            foreach(var line in grid)Line(line.a+offset,line.b+offset,normal,.025f,0);
            var local=plot.Grid.InverseTransformPoint(anchor);
            Vector3 Point(Vector2 p)=>plot.Grid.TransformPoint(new Vector3(p.x,local.y,p.y))+offset;
            void Rectangle(Rect rect,int submesh,bool cells)
            {
                var corners=new[]{new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMin),new Vector2(rect.xMax,rect.yMax),new Vector2(rect.xMin,rect.yMax)};
                for(int i=0;i<4;i++)Line(Point(corners[i]),Point(corners[(i+1)%4]),normal,.055f,submesh);
                if(!cells)return;FootprintSegments=4;
                for(float x=rect.xMin+4;x<rect.xMax-.01f;x+=4){Line(Point(new Vector2(x,rect.yMin)),Point(new Vector2(x,rect.yMax)),normal,.04f,submesh);FootprintSegments++;}
                for(float z=rect.yMin+4;z<rect.yMax-.01f;z+=4){Line(Point(new Vector2(rect.xMin,z)),Point(new Vector2(rect.xMax,z)),normal,.04f,submesh);FootprintSegments++;}
            }
            var footprint=Footprint(entry,plot,position,rotation);
            var wall=entry.prefab.GetComponent<WallTopology>();
            if(wall)
            {
                var pieces=WallShape(WallGeometry.At(wall,position,rotation,entry.prefab.transform.localScale),plot.Grid,out var contour);
                foreach(var polygon in pieces)Polygon(polygon.Select(Point).ToArray(),3);
                foreach(var line in contour)Line(Point(line.a),Point(line.b),normal,.055f,1);
                FootprintSegments=contour.Count;
            }
            else
            {
                Rectangle(footprint,1,true);
                Polygon(new[]{Point(footprint.min),Point(new Vector2(footprint.xMax,footprint.yMin)),Point(footprint.max),Point(new Vector2(footprint.xMin,footprint.yMax))},3);
            }
            if(BuildFootprint.Uses(entry))
            {
                var rect=footprint;
                // Per-entry front calibration preserves the imported mesh origin.
                void Arrow(float yaw)
                {
                var front=Vector3.ProjectOnPlane(rotation*Quaternion.Euler(0,entry.guideFrontYaw+yaw,0)*Vector3.forward,normal).normalized;
                var frontLocal=plot.Grid.InverseTransformDirection(front);
                float reach=Mathf.Min(Mathf.Abs(frontLocal.x)>.001f?rect.width*.5f/Mathf.Abs(frontLocal.x):float.PositiveInfinity,
                    Mathf.Abs(frontLocal.z)>.001f?rect.height*.5f/Mathf.Abs(frontLocal.z):float.PositiveInfinity);
                var edge=Point(rect.center)+front*reach;var right=Vector3.Cross(normal,front).normalized;
                Polygon(new[]{edge-front*.6f-right*.18f,edge-front*.6f+right*.18f,edge+right*.18f,edge-right*.18f},4);
                Polygon(new[]{edge-right*.65f,edge+right*.65f,edge+front*.8f},4);HasFrontArrow=true;AccessArrowCount++;
                }
                if((entry.guideAccessSides&BuildAccessSides.Front)!=0)Arrow(0);
                if((entry.guideAccessSides&BuildAccessSides.Back)!=0)Arrow(180);
                if((entry.guideAccessSides&BuildAccessSides.Left)!=0)Arrow(-90);
                if((entry.guideAccessSides&BuildAccessSides.Right)!=0)Arrow(90);
                foreach(var item in plot.GetComponentsInChildren<PlacedBuildObject>())
                {
                    if(item.deleted || item.propertyId!=plot.propertyId || item.GetComponentInParent<BuildPlot>()!=plot)continue;
                    var other=catalog.Find(item.definitionId);if(!BuildFootprint.Uses(other))continue;
                    var otherAnchor=BuildFootprint.Anchor(other,item.transform.position,item.transform.rotation);
                    if(!BuildFootprint.SameSurface(entry,anchor,other,otherAnchor,plot) || Vector3.Distance(anchor,otherAnchor)>alignmentRange)continue;
                    var otherRect=BuildFootprint.RectOnGrid(other,plot,item.transform.position,item.transform.rotation);Rectangle(otherRect,0,false);
                    if(TryAlignment(rect,otherRect,out var start,out var end)){Line(Point(start),Point(end),normal,.075f,2);AlignmentSegments++;}
                }
            }
            materials[0].SetColor("_BaseColor",colors.grid);
            materials[1].SetColor("_BaseColor",valid?colors.validOutline:colors.invalidOutline);
            materials[2].SetColor("_BaseColor",colors.alignment);
            materials[3].SetColor("_BaseColor",valid?colors.validFill:colors.invalidFill);
            materials[4].SetColor("_BaseColor",valid?colors.validArrow:colors.invalidArrow);
            mesh.Clear();mesh.SetVertices(vertices);mesh.subMeshCount=materials.Length;
            for(int i=0;i<materials.Length;i++)mesh.SetTriangles(indices[i],i);mesh.RecalculateBounds();root.SetActive(vertices.Count>0);
        }
        static void Release(Object obj){if(!obj)return;if(Application.isPlaying)Object.Destroy(obj);else Object.DestroyImmediate(obj);}
        public void Dispose(){Release(root);Release(mesh);foreach(var material in materials)Release(material);}
    }
}
