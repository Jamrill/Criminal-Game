using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using JuegoCriminal.Construction;
using JuegoCriminal.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using Debug=UnityEngine.Debug;

public static class LargeRoofValidation
{
    static List<Vector2> Square(float x,float z,float size)=>new(){new(x,z),new(x+size,z),new(x+size,z+size),new(x,z+size)};
    static float Area(IEnumerable<List<Vector2>> pieces)=>pieces.Sum(p=>Mathf.Abs(RoomGeometry.SignedArea(p)));
    public static void Run()
    {
        var report=new List<string>();
        void Check(bool value,string label){report.Add((value?"PASS ":"FAIL ")+label);File.WriteAllLines("Documentation/LargeRoofValidation.txt",report);if(!value)throw new Exception(label);}
        var original=new List<List<Vector2>>{Square(0,0,4)};
        var collapsed=new[]{new Vector2(0,0),new Vector2(4,4),new Vector2(4,4)};
        var unchanged=original;
        for(int i=0;i<100;i++)unchanged=RoomGeometry.Subtract(unchanged,collapsed);
        Check(unchanged.Count==1 && Mathf.Abs(Area(unchanged)-16)<.00001f,"Collapsed legacy triangles cannot duplicate fragments or surface area");
        var repeated=new[]{new Vector2(0,0),new Vector2(4,0),new Vector2(4,0),new Vector2(4,4),new Vector2(0,4),new Vector2(0,0)};
        Check(RoomGeometry.Clean(repeated).Count==4,"Duplicate polygon vertices are removed");
        var clockwise=Square(1,1,2);clockwise.Reverse();
        Check(Mathf.Abs(Area(RoomGeometry.Subtract(original,clockwise))-12)<.00001f,"Subtraction normalises cutter winding without changing coverage");
        bool bounded=false;
        try{RoomGeometry.Subtract(Enumerable.Range(0,4097).Select(_=>Square(0,0,4)).ToList(),Square(1,1,2));}
        catch(RoomGeometry.GeometryLimitException){bounded=true;}
        Check(bounded,"Excessive fragment input is rejected before exponential allocation");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;
        var floor=catalog.entries.Single(FloorEditing.IsFloor);
        var services=new GameObject("Large roof test services");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();var properties=services.AddComponent<PropertyService>();
        foreach(Component target in new Component[]{money,properties})target.GetType().GetField("_save",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,save);
        var data=new SaveData{slotId=1,money=10000000,ownedProperties=new[]{996,997}};
        typeof(SaveService).GetProperty("Current").GetSetMethod(true).Invoke(save,new object[]{data});
        var plot=new GameObject("Whole plot closure 44x44").AddComponent<BuildPlot>();plot.propertyId=996;plot.buildable=true;plot.catalog=catalog;plot.fitFurnitureGridToPlot=true;plot.Volume.size=new Vector3(44.5f,28,44.5f);
        var manager=plot.gameObject.AddComponent<RoomConstruction>();
        var walls=new List<WallTopology>();
        WallTopology Wall(BuildPlot owner,Vector2 a,Vector2 b,bool collider)
        {
            var wall=new GameObject("Stress wall").AddComponent<WallTopology>();wall.transform.SetParent(owner.transform,false);wall.transform.localPosition=new Vector3(a.x,0,a.y);
            var end=new Vector3(b.x-a.x,0,b.y-a.y);wall.height=7;wall.segmentPoints=new[]{Vector3.zero,end};
            if(collider)
            {
                var box=new GameObject("Collision").AddComponent<BoxCollider>();box.transform.SetParent(wall.transform,false);box.transform.localPosition=end*.5f+Vector3.up*3.5f;
                box.transform.localRotation=Quaternion.FromToRotation(Vector3.right,end.normalized);box.size=new Vector3(end.magnitude,7,.5f);
            }
            return wall;
        }
        try
        {
            for(int x=0;x<11;x++)for(int z=0;z<11;z++)ConstructionPersistence.Spawn(catalog,new BuildRecord{id="foundation-"+x+"-"+z,definitionId=floor.id,propertyId=996,sceneName=scene.name,position=new Vector3(-22+x*4,-.01f,-22+z*4),rotation=Quaternion.identity,scale=Vector3.one,finish=-1},true,scene);
            var perimeter=Square(-22,-22,44);
            for(int side=0;side<4;side++)for(int step=0;step<11;step++)
                walls.Add(Wall(plot,Vector2.Lerp(perimeter[side],perimeter[(side+1)%4],step/11f),Vector2.Lerp(perimeter[side],perimeter[(side+1)%4],(step+1)/11f),true));
            var inner=Square(-6,-6,8);for(int i=0;i<4;i++)walls.Add(Wall(plot,inner[i],inner[(i+1)%4],true));
            walls[43].gameObject.SetActive(false);manager.Rebuild();
            Check(manager.rooms.Count==1,"Before the last perimeter wall only the inner room is enclosed");
            walls[43].gameObject.SetActive(true);int balance=money.Money;var timer=Stopwatch.StartNew();
            manager.Rebuild(true,money);timer.Stop();
            Check(manager.rooms.Count==2 && manager.rooms.All(r=>r.floorComplete && r.ceilingComplete) && string.IsNullOrEmpty(manager.Warning),"Closing the whole plot with an inner room completes both roofs");
            var roof=plot.GetComponentsInChildren<RoomFloorPatch>().Where(p=>p.transform.position.y>6).ToArray();
            Check(roof.Length==121,"Large roof has exactly 121 paid modular tiles: "+roof.Length);
            Check(balance-money.Money==121*catalog.Cost(floor),"Each roof tile is charged once and foundation is reused");
            Check(roof.All(p=>p.PlanarPieces(plot.Grid).All(t=>Mathf.Abs(RoomGeometry.SignedArea(t))>RoomGeometry.MinimumArea)),"No generated roof triangle has zero area");
            Check(Mathf.Abs(roof.Sum(p=>Area(p.PlanarPieces(plot.Grid)))-1936)<.01f,"Roof surface equals the 44x44 plot without duplicate fragments");
            report.Add("METRIC initial roof completion ms="+timer.ElapsedMilliseconds);
            balance=money.Money;timer.Restart();
            for(int repeat=0;repeat<3;repeat++)manager.Rebuild(true,money);
            foreach(var wall in walls)BuildFloorCoverage.Covers(wall);
            timer.Stop();
            Check(money.Money==balance && plot.GetComponentsInChildren<RoomFloorPatch>().Length==121,"Repeated room and wall-cap scans neither duplicate nor charge tiles");
            report.Add("METRIC three scans plus wall coverage ms="+timer.ElapsedMilliseconds);
            var old=ConstructionPersistence.Spawn(catalog,new BuildRecord{id="legacy-roof",definitionId=floor.id,propertyId=996,sceneName=scene.name,position=new Vector3(0,21,0),rotation=Quaternion.identity,scale=Vector3.one,finish=-1,
                floorTriangles=new[]{Vector3.zero,new Vector3(4,0,0),new Vector3(4,0,4),Vector3.zero,new Vector3(4,0,4),new Vector3(4,0,4)}},true,scene);
            Check(old.GetComponent<RoomFloorPatch>().triangles.Length==3 && old.Capture().floorTriangles.Length==3,"Loading and capturing old saves removes collapsed triangles from mesh and saved geometry");
            var huge=new GameObject("Oversized guarded room").AddComponent<BuildPlot>();huge.propertyId=997;huge.buildable=true;huge.catalog=catalog;huge.Volume.size=Vector3.one*3000;
            var guarded=huge.gameObject.AddComponent<RoomConstruction>();var outline=Square(0,0,1000);
            for(int i=0;i<4;i++)Wall(huge,outline[i],outline[(i+1)%4],false);
            balance=money.Money;guarded.Rebuild(true,money);
            Check(!string.IsNullOrEmpty(guarded.Warning) && money.Money==balance && huge.GetComponentsInChildren<PlacedBuildObject>().Length==0,"Oversized completion shows a warning without charging or spawning a partial roof");
            Object.DestroyImmediate(huge.gameObject);
        }
        finally{Object.DestroyImmediate(plot.gameObject);Object.DestroyImmediate(services);Object.DestroyImmediate(catalog);}
        Debug.Log("LARGE_ROOF_VALIDATION_OK checks="+report.Count);
    }
    public static void ActualScene()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        var plot=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BuildPlot>(true)).Single(p=>p.catalog && p.buildable);
        var manager=plot.GetComponent<RoomConstruction>();
        var services=new GameObject("Read-only source scene roof test");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();var properties=services.AddComponent<PropertyService>();
        foreach(Component target in new Component[]{money,properties})target.GetType().GetField("_save",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,save);
        typeof(SaveService).GetProperty("Current").GetSetMethod(true).Invoke(save,new object[]{new SaveData{slotId=1,money=100000000,ownedProperties=new[]{plot.propertyId}}});
        var offset=plot.FurnitureGridOffset;plot.TryFurnitureGridLayout(out _,out var cells);
        var boundary=new[]{offset,offset+new Vector2(cells.x*4,0),offset+new Vector2(cells.x*4,cells.y*4),offset+new Vector2(0,cells.y*4)};
        for(int side=0;side<4;side++)
        {
            int count=side%2==0?cells.x:cells.y;
            for(int i=0;i<count;i++)
            {
                var a=Vector2.Lerp(boundary[side],boundary[(side+1)%4],(float)i/count);var b=Vector2.Lerp(boundary[side],boundary[(side+1)%4],(float)(i+1)/count);
                var worldA=plot.Grid.TransformPoint(new Vector3(a.x,0,a.y));var worldB=plot.Grid.TransformPoint(new Vector3(b.x,0,b.y));
                if(plot.GetComponentsInChildren<WallTopology>().SelectMany(WallGeometry.World).Any(e=>e.Same(new WallGeometry.Span(worldA,worldB))))continue;
                var wall=new GameObject("Temporary perimeter stress wall").AddComponent<WallTopology>();wall.transform.SetParent(plot.transform,false);wall.transform.SetPositionAndRotation(worldA,plot.Grid.rotation);
                wall.height=7;wall.segmentPoints=new[]{Vector3.zero,new Vector3(b.x-a.x,0,b.y-a.y)};
            }
        }
        var timer=Stopwatch.StartNew();manager.Rebuild(true,money);timer.Stop();
        File.WriteAllText("Documentation/LargeRoofActualScene.txt","Scene was NOT saved.\nRooms="+manager.rooms.Count+"\nComplete="+manager.rooms.Count(r=>r.floorComplete && r.ceilingComplete)+"\nWarning="+manager.Warning+"\nElapsed ms="+timer.ElapsedMilliseconds);
        if(!string.IsNullOrEmpty(manager.Warning) || !manager.rooms.Any() || manager.rooms.Any(r=>!r.floorComplete || !r.ceilingComplete))throw new Exception("Source-scene perimeter roof did not complete: "+manager.Warning);
        Debug.Log("LARGE_ROOF_SOURCE_SCENE_OK ms="+timer.ElapsedMilliseconds);
        // Discard the entire diagnostic scene, including temporary geometry and economy.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    }
    public static void Regressions(){Run();ActualScene();BuildInputAndHeightValidation.Run();Debug.Log("LARGE_ROOF_REGRESSIONS_OK");}
}
