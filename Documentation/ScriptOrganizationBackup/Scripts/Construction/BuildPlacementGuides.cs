using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace JuegoCriminal.Construction
{
    // One transient mesh, not hundreds of colliders or LineRenderer objects.
    public sealed class BuildPlacementGuides : IDisposable
    {
        struct Segment {public Vector3 a,b;public Segment(Vector3 a,Vector3 b){this.a=a;this.b=b;}}
        readonly GameObject root;
        readonly Mesh mesh;
        readonly Material[] materials;
        readonly List<Vector3> vertices=new();
        readonly List<int>[] indices={new(),new(),new()};
        readonly List<Segment> grid=new();
        BuildPlot cachedPlot;
        Vector3 cachedAnchor;
        int cachedRadius;
        bool cachedCeiling;
        float refreshedAt=-1;
        public int GridSegments {get;private set;}
        public int FootprintSegments {get;private set;}
        public int AlignmentSegments {get;private set;}
        public bool Visible=>root && root.activeSelf;
        public BuildPlacementGuides()
        {
            root=new GameObject("ConstructionPlacementGuides",typeof(MeshFilter),typeof(MeshRenderer));root.layer=2;root.hideFlags=HideFlags.DontSave;
            mesh=new Mesh{name="Construction guide lines"};mesh.MarkDynamic();root.GetComponent<MeshFilter>().sharedMesh=mesh;
            materials=new[]{Material(new Color(1,1,1,.22f),3100),Material(new Color(.3f,1,.4f,.9f),3110),Material(new Color(1,1,1,.9f),3120)};
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
        public void Show(BuildEntry entry,BuildCatalog catalog,BuildPlot plot,Vector3 position,Quaternion rotation,Vector3 anchor,bool valid,Transform player,int radius,float alignmentRange)
        {
            if(entry.mount==BuildMount.Doorway){Hide();return;}
            bool ceiling=entry.mount==BuildMount.Ceiling;var normal=ceiling?-plot.Grid.up:plot.Grid.up;
            RebuildGrid(plot,anchor,ceiling,Mathf.Clamp(radius,1,8),player);
            vertices.Clear();foreach(var list in indices)list.Clear();GridSegments=grid.Count;FootprintSegments=AlignmentSegments=0;
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
            if(BuildFootprint.Uses(entry))
            {
                var rect=BuildFootprint.RectOnGrid(entry,plot,position,rotation);Rectangle(rect,1,true);
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
            materials[1].SetColor("_BaseColor",valid?new Color(.3f,1,.4f,.9f):new Color(1,.18f,.18f,.9f));
            mesh.Clear();mesh.SetVertices(vertices);mesh.subMeshCount=3;
            for(int i=0;i<3;i++)mesh.SetTriangles(indices[i],i);mesh.RecalculateBounds();root.SetActive(vertices.Count>0);
        }
        static void Release(Object obj){if(!obj)return;if(Application.isPlaying)Object.Destroy(obj);else Object.DestroyImmediate(obj);}
        public void Dispose(){Release(root);Release(mesh);foreach(var material in materials)Release(material);}
    }
}
