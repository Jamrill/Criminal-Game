using System;
using System.Collections.Generic;
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
            vertices.Clear();foreach(var list in indices)list.Clear();GridSegments=grid.Count;FootprintSegments=AlignmentSegments=0;HasFrontArrow=false;
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
                // Corners and junctions follow their arms, not an enclosing square
                // which would falsely suggest that the space between arms is used.
                foreach(var span in WallGeometry.At(wall,position,rotation,entry.prefab.transform.localScale))
                {
                    var a=plot.Grid.InverseTransformPoint(span.a);var b=plot.Grid.InverseTransformPoint(span.b);
                    var start=Point(new Vector2(a.x,a.z));var end=Point(new Vector2(b.x,b.z));
                    var side=Vector3.Cross(normal,end-start).normalized*.25f;
                    var corners=new[]{start-side,start+side,end+side,end-side};Polygon(corners,3);
                    for(int i=0;i<4;i++)Line(corners[i],corners[(i+1)%4],normal,.055f,1);
                    FootprintSegments+=4;
                }
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
                var front=Vector3.ProjectOnPlane(rotation*Quaternion.Euler(0,entry.guideFrontYaw,0)*Vector3.forward,normal).normalized;
                var frontLocal=plot.Grid.InverseTransformDirection(front);
                float reach=Mathf.Min(Mathf.Abs(frontLocal.x)>.001f?rect.width*.5f/Mathf.Abs(frontLocal.x):float.PositiveInfinity,
                    Mathf.Abs(frontLocal.z)>.001f?rect.height*.5f/Mathf.Abs(frontLocal.z):float.PositiveInfinity);
                var edge=Point(rect.center)+front*reach;var right=Vector3.Cross(normal,front).normalized;
                Polygon(new[]{edge-front*.6f-right*.18f,edge-front*.6f+right*.18f,edge+right*.18f,edge-right*.18f},4);
                Polygon(new[]{edge-right*.65f,edge+right*.65f,edge+front*.8f},4);HasFrontArrow=true;
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
