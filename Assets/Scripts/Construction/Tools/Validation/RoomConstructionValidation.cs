#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JuegoCriminal.Construction;
using JuegoCriminal.Core;
using JuegoCriminal.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class RoomConstructionValidation
{
    static readonly List<string> report=new();
    static void Check(bool condition,string message)
    {report.Add((condition?"PASS ":"FAIL ")+message);File.WriteAllLines("Documentation/RoomConstructionValidation.txt",report);if(!condition)throw new Exception(message);}
    static List<RoomGeometry.Edge> Loop(params Vector2[] points)
    {return points.Select((p,i)=>new RoomGeometry.Edge(p,points[(i+1)%points.Length])).ToList();}
    static List<RoomGeometry.Edge> Square(float x=0,float z=0,float size=4)
        =>Loop(new Vector2(x,z),new Vector2(x+size,z),new Vector2(x+size,z+size),new Vector2(x,z+size));
    public static void Geometry()
    {
        report.Clear();
        var square=Square();var rooms=RoomGeometry.Find(square);
        Check(rooms.Count==1 && Mathf.Abs(rooms[0].Area-16)<.001f,"Four walls enclose one room");
        var open=square.Take(3);Check(RoomGeometry.Find(open).Count==0,"Open outline is not a room");
        var gap=square.Take(3).Concat(new[]{new RoomGeometry.Edge(new Vector2(0,4),new Vector2(0,.02f))});
        Check(RoomGeometry.Find(gap).Count==0,"Small genuine wall gap prevents closure");
        var divided=Square(0,0,8);divided.Add(new RoomGeometry.Edge(new Vector2(4,0),new Vector2(4,8)));
        var halves=RoomGeometry.Find(divided);Check(halves.Count==2 && halves.All(r=>Mathf.Abs(r.Area-32)<.001f),"T junction divides rooms even without endpoint on long wall");
        divided.Add(new RoomGeometry.Edge(new Vector2(0,4),new Vector2(8,4)));
        Check(RoomGeometry.Find(divided).Count==4,"Cross intersection yields four rooms");
        var diagonal=Loop(Vector2.zero,new Vector2(8,0),new Vector2(0,8));
        Check(RoomGeometry.Find(diagonal).Count==1 && Mathf.Abs(RoomGeometry.Find(diagonal)[0].Area-32)<.001f,"Diagonal walls enclose triangular room");
        var concave=Loop(Vector2.zero,new Vector2(8,0),new Vector2(8,4),new Vector2(4,4),new Vector2(4,8),new Vector2(0,8));
        var shape=RoomGeometry.Find(concave).Single();Check(Mathf.Abs(shape.Area-48)<.001f && shape.Contains(shape.InteriorPoint),"Concave room has interior capture position");
        Check(Mathf.Abs(RoomGeometry.Triangulate(shape.outline).Sum(p=>RoomGeometry.SignedArea(p))-48)<.001f,"Concave triangulation preserves area");
        var nested=Square(0,0,12);nested.AddRange(Square(4,4,4));
        var inside=RoomGeometry.Find(nested);Check(inside.Count==2 && Mathf.Abs(inside.Sum(r=>r.Area)-144)<.001f,"Nested closed room does not double-count floor");
        square.Add(new RoomGeometry.Edge(new Vector2(0,2),new Vector2(2,2)));
        Check(RoomGeometry.Find(square).Count==1 && Mathf.Abs(RoomGeometry.Find(square)[0].Area-16)<.001f,"Dangling internal wall does not invent a room");
        var region=rooms[0];
        var cover=new List<List<Vector2>>{new List<Vector2>{Vector2.zero,new Vector2(4,0),new Vector2(4,4),new Vector2(0,4)}};
        Check(RoomGeometry.Missing(region,cover).Count==0,"Existing complete floor creates no duplicates");
        cover[0][1]=new Vector2(3.98f,0);cover[0][2]=new Vector2(3.98f,4);
        Check(Mathf.Abs(RoomGeometry.Missing(region,cover).Sum(p=>RoomGeometry.SignedArea(p))-.08f)<.001f,"Narrow uncovered strip is detected exactly");
        var big=RoomGeometry.Find(Square(-22,-22,44)).Single();
        big.holes.Add(new List<Vector2>{new Vector2(-2,2),new Vector2(10,2),new Vector2(10,10),new Vector2(-2,10)});
        var tiles=new List<List<Vector2>>();
        for(int x=0;x<11;x++)for(int z=0;z<11;z++)tiles.Add(new List<Vector2>{new Vector2(-22+x*4,-22+z*4),new Vector2(-18+x*4,-22+z*4),new Vector2(-18+x*4,-18+z*4),new Vector2(-22+x*4,-18+z*4)});
        Check(RoomGeometry.Missing(big,tiles).Count==0,"Large tiled floor with internal room has no numerical gaps");
    }
    static void Bind(Component target,SaveService save)=>target.GetType().GetField("_save",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,save);
    public static void Run()
    {
        Geometry();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;
        var floor=catalog.entries.First(e=>e.prefab && e.prefab.GetComponent<BuildFloorCoverage>());floor.price=30;
        var plot=new GameObject("Test plot").AddComponent<BuildPlot>();plot.propertyId=71;plot.catalog=catalog;plot.buildable=true;plot.Volume.size=new Vector3(100,100,100);
        var manager=plot.gameObject.AddComponent<RoomConstruction>();
        var services=new GameObject("Test services");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();var properties=services.AddComponent<PropertyService>();Bind(money,save);Bind(properties,save);
        var data=new SaveData{slotId=1,money=59,ownedProperties=new[]{71}};
        typeof(SaveService).GetProperty("Current").GetSetMethod(true).Invoke(save,new object[]{data});
        var walls=new List<WallTopology>();
        foreach(var edge in Square())
        {
            var go=new GameObject("Wall");go.transform.SetParent(plot.transform,false);var wall=go.AddComponent<WallTopology>();wall.height=4;
            wall.segmentPoints=new[]{new Vector3(edge.a.x,0,edge.a.y),new Vector3(edge.b.x,0,edge.b.y)};wall.bottomCenter=Vector3.zero;walls.Add(wall);
        }
        manager.Rebuild(true,money);
        Check(manager.rooms.Count==1 && manager.PendingCost==60 && money.Money==59 && plot.GetComponentsInChildren<RoomFloorPatch>().Length==0,"Insufficient funds leaves no half-built floor or charge");
        data.money=100;manager.Rebuild(true,money);
        var tiles=plot.GetComponentsInChildren<PlacedBuildObject>();
        Check(tiles.Length==2 && money.Money==40,"Room automatically charges and builds one floor and one ceiling tile");
        Check(manager.rooms[0].floorComplete && manager.rooms[0].ceilingComplete && plot.GetComponentsInChildren<ReflectionZone>().Length==1,"One reflection volume only after complete enclosure");
        Check(tiles.All(t=>t.transform.parent==plot.transform && t.ProtectedFloor),"Floor tiles belong to plot and are protected");
        foreach(var tile in tiles)
        {
            if(FloorEditing.CanRemove(tile)){Check(tile.transform.position.y>plot.foundationHeight+.06f,"Automatic upper floor is editable");continue;}
            Check(!ConstructionEconomy.Sell(catalog,tile,money),"Cannot sell foundation tile");tile.Demolish();Check(!tile.deleted && tile.gameObject.activeSelf,"Cannot demolish foundation tile");
        }
        Check(money.Money==40,"Blocked floor sale never refunds money");
        manager.Rebuild(true,money);Check(plot.GetComponentsInChildren<RoomFloorPatch>().Length==2 && money.Money==40,"Repeated room scan neither duplicates nor charges again");
        Physics.SyncTransforms();
        Check(Physics.Raycast(new Vector3(1,2,1),Vector3.down,out var hit,3) && hit.collider.GetComponentInParent<RoomFloorPatch>(),"Generated floor supports character collision");
        Check(Physics.Raycast(new Vector3(1,2,1),Vector3.up,out hit,3) && hit.normal.y<-.9f,"Generated ceiling supports underside raycasts");
        var records=tiles.Select(t=>t.Capture()).ToArray();
        string json=JsonUtility.ToJson(new SaveData{buildings=records});var loaded=JsonUtility.FromJson<SaveData>(json);
        foreach(var tile in tiles)Object.DestroyImmediate(tile.gameObject);
        foreach(var record in loaded.buildings)ConstructionPersistence.Spawn(catalog,record,true,scene);
        manager.Rebuild();Check(plot.GetComponentsInChildren<RoomFloorPatch>().Length==2 && manager.rooms[0].floorComplete && manager.rooms[0].ceilingComplete,"Save/load rebuilds geometry and enclosure without another payment");
        walls[0].gameObject.SetActive(false);manager.Rebuild();
        Check(manager.rooms.Count==0 && plot.GetComponentsInChildren<ReflectionZone>().Length==0 && plot.GetComponentsInChildren<RoomFloorPatch>().Length==2,"Opening room removes capture but keeps permanent floors");
        walls[0].gameObject.SetActive(true);manager.Rebuild(true,money);
        Check(manager.rooms.Count==1 && money.Money==40,"Closing again reuses permanent tiles");
        foreach(var wall in walls.ToArray())
        {
            var go=new GameObject("Upper wall");go.transform.SetParent(plot.transform,false);go.transform.localPosition=Vector3.up*4;
            var upper=go.AddComponent<WallTopology>();upper.height=4;upper.segmentPoints=wall.segmentPoints;upper.bottomCenter=Vector3.zero;
        }
        data.money=100;manager.Rebuild(true,money);
        Check(manager.rooms.Count==2 && manager.rooms.All(r=>r.floorComplete && r.ceilingComplete) && plot.GetComponentsInChildren<RoomFloorPatch>().Length==3 && money.Money==70,"Upper storey reuses lower ceiling as floor and charges only new roof");
        Check(plot.GetComponentsInChildren<ReflectionZone>().Length==2,"Independent reflection for each storey");
        foreach(var edge in Loop(new Vector2(8,0),new Vector2(12,0),new Vector2(8,4)))
        {
            var go=new GameObject("Triangle wall");go.transform.SetParent(plot.transform,false);var wall=go.AddComponent<WallTopology>();wall.height=4;
            wall.segmentPoints=new[]{new Vector3(edge.a.x,0,edge.a.y),new Vector3(edge.b.x,0,edge.b.y)};
        }
        data.money=200;manager.Rebuild(true,money);
        var diagonalTiles=plot.GetComponentsInChildren<RoomFloorPatch>().Where(p=>p.transform.position.x>=8).ToArray();
        Check(diagonalTiles.Length==2 && money.Money==140 && diagonalTiles.All(p=>Mathf.Abs(p.PlanarPieces(plot.Grid).Sum(q=>RoomGeometry.SignedArea(q))-8)<.001f),"Diagonal room gets clipped tiles, not square overhangs");
        Check(manager.rooms.Count==3 && plot.GetComponentsInChildren<ReflectionZone>().Length==3,"Separate rooms have independent captures");
        var stairs=catalog.entries.First(StairConstruction.IsStairs);
        var stairRecord=new BuildRecord{id="stair-test",definitionId=stairs.id,propertyId=plot.propertyId,sceneName=scene.name,position=new Vector3(24,0,16),rotation=Quaternion.identity,scale=Vector3.one,finish=-1};
        var oldCeiling=ConstructionPersistence.Spawn(catalog,new BuildRecord{id="ceiling-cut",definitionId=floor.id,propertyId=plot.propertyId,sceneName=scene.name,position=new Vector3(16,7,12),rotation=Quaternion.identity,scale=Vector3.one,finish=-1},true,scene);
        Physics.SyncTransforms();
        var stairPlan=StairConstruction.Plan(catalog,plot,stairRecord);
        Check(string.IsNullOrEmpty(stairPlan.error),"Staircase enclosure plan is clear: "+stairPlan.error);
        Check(stairPlan.additions.Any(r=>catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>()) && stairPlan.additions.Any(r=>r.floorTriangles!=null),"Stairs plan includes upper walls, landing and roof");
        Check(stairPlan.edits.Any(e=>e.item==oldCeiling),"Staircase removes the intervening protected ceiling through an explicit exception");
        int stairCost=stairPlan.Cost(catalog);data.money=stairCost-1;
        Check(!ConstructionEconomy.Build(catalog,stairRecord,plot,money) && !oldCeiling.deleted && money.Money==stairCost-1,"Staircase cannot cut ceiling or partly build with insufficient funds");
        data.money=100000;
        var builtStair=ConstructionEconomy.Build(catalog,stairRecord,plot,money);
        Check(builtStair && oldCeiling.deleted && money.Money==100000-stairCost,"Staircase charges all parts and cuts old ceiling atomically");
        int afterStair=money.Money;manager.Rebuild(true,money);
        Check(manager.rooms.Count==4 && manager.rooms.All(r=>r.floorComplete && r.ceilingComplete) && money.Money==afterStair,"Room scan preserves staircase opening instead of rebuilding its ceiling");
        ConstructionEconomy.Sell(catalog,builtStair,money);Physics.SyncTransforms();
        var reused=StairConstruction.Plan(catalog,plot,stairRecord);
        Check(string.IsNullOrEmpty(reused.error) && reused.additions.Count==0 && reused.Cost(catalog)==catalog.Cost(stairs),"Existing upper room is reused without charging another enclosure: error="+reused.error+" additions="+reused.additions.Count+" cost="+reused.Cost(catalog)+" rooms="+string.Join(";",manager.rooms.Select(r=>r.floor+" "+r.shape.min+".."+r.shape.max)));
        Object.DestroyImmediate(plot.gameObject);Object.DestroyImmediate(services);Object.DestroyImmediate(catalog);
        Inspect();Debug.Log("ROOM_CONSTRUCTION_VALIDATION_OK checks="+report.Count);
    }
    public static void Inspect()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");var lines=new List<string>();
        foreach(var plot in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BuildPlot>(true)))
        {
            var manager=plot.GetComponent<RoomConstruction>()??plot.gameObject.AddComponent<RoomConstruction>();manager.Rebuild();
            lines.Add(plot.name+" grid="+plot.Grid.position+" floor count="+plot.GetComponentsInChildren<BuildFloorCoverage>().Length+" rooms="+manager.rooms.Count);
            foreach(var level in plot.GetComponentsInChildren<BuildFloorCoverage>().GroupBy(f=>Mathf.Round(plot.Grid.InverseTransformPoint(f.transform.position).y*100)/100))lines.Add("Authored floor level "+level.Key+": "+level.Count());
            foreach(var room in manager.rooms)lines.Add("floor="+room.floor+" ceiling="+room.ceiling+" area="+room.area+" bounds="+room.shape.min+".."+room.shape.max+" floor complete="+room.floorComplete+" roof complete="+room.ceilingComplete+" missing floor area="+room.missingFloorArea+" missing roof area="+room.missingCeilingArea);
        }
        File.WriteAllLines("Documentation/RoomConstructionInspection.txt",lines);
        // Inspection is disposable: never save generated captures or completion.
    }
    public static void Apply()
    {
        Run();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        string backup="Documentation/RoomConstructionBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
        File.Copy(scene.path,backup+"/10_World_City.unity");File.Copy(ConstructionSetup.CatalogPath,backup+"/ConstructionCatalog.asset");
        var template=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ReflectionZone>(true)).FirstOrDefault(z=>z.name=="Reflections_Interior_Small_Property");
        foreach(var plot in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BuildPlot>(true)))
        {
            var manager=plot.GetComponent<RoomConstruction>()??plot.gameObject.AddComponent<RoomConstruction>();manager.reflectionTemplate=template;
        }
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        foreach(var entry in catalog.entries)if(FloorEditing.IsFloor(entry))entry.hiddenInMenu=false;
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        Debug.Log("ROOM_CONSTRUCTION_SETUP_OK backup="+backup);
    }
    public static void Regressions(){Run();WallAutoJoinValidation.Full();ButtonPrefabMigration.Validate();Debug.Log("ROOM_CONSTRUCTION_REGRESSIONS_OK");}
}
#endif
