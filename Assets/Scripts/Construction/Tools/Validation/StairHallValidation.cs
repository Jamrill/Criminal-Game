#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JuegoCriminal.Construction;
using JuegoCriminal.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class StairHallValidation
{
    public static void Regressions(){Run();WallAutoJoinValidation.Full();ButtonPrefabMigration.Validate();Debug.Log("STAIR_HALL_REGRESSIONS_OK");}
    static List<Vector2> Tile(float x,float z)=>new(){new(x,z),new(x+4,z),new(x+4,z+4),new(x,z+4)};
    public static void Apply()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        string backup="Documentation/StairHallBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
        File.Copy(ConstructionSetup.CatalogPath,backup+"/ConstructionCatalog.asset");
        foreach(var floor in catalog.entries.Where(FloorEditing.IsFloor))floor.hiddenInMenu=false;
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();Run();
        Debug.Log("STAIR_HALL_APPLIED backup="+backup);
    }
    public static void Run()
    {
        var report=new List<string>();
        void Check(bool value,string label){report.Add((value?"PASS ":"FAIL ")+label);File.WriteAllLines("Documentation/StairHallValidation.txt",report);if(!value)throw new Exception(label);}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;
        var stairs=catalog.entries.Single(StairConstruction.IsStairs);var floor=catalog.entries.Single(FloorEditing.IsFloor);
        Check(!floor.hiddenInMenu,"Floor is visible in the construction catalogue");
        var plot=new GameObject("Stair hall test plot").AddComponent<BuildPlot>();plot.propertyId=881;plot.buildable=true;plot.catalog=catalog;plot.Volume.size=Vector3.one*200;
        var manager=plot.gameObject.AddComponent<RoomConstruction>();
        var services=new GameObject("Isolated test economy");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();var properties=services.AddComponent<PropertyService>();
        foreach(Component target in new Component[]{money,properties})target.GetType().GetField("_save",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(target,save);
        var data=new SaveData{slotId=1,money=100000,ownedProperties=new[]{881}};
        typeof(SaveService).GetProperty("Current").GetSetMethod(true).Invoke(save,new object[]{data});
        try
        {
            foreach(float angle in new[]{0f,90f,180f,270f})
            {
                var record=new BuildRecord{definitionId=stairs.id,propertyId=plot.propertyId,sceneName=scene.name,position=Vector3.zero,rotation=Quaternion.Euler(0,angle,0),scale=Vector3.one,finish=-1};
                Check(StairConstruction.Passage(catalog,plot,record,out var hole,out float level) && StairConstruction.Landing(catalog,plot,record,out var landing,out _),"Upper exit found at yaw="+angle);
                StairConstruction.Landing(catalog,plot,record,out landing,out _);
                Check(!BuildFootprint.Overlap(hole,landing),"Stair opening never consumes the landing at yaw="+angle);
                var plan=StairConstruction.Plan(catalog,plot,record);
                Check(string.IsNullOrEmpty(plan.error),"Hall plan is placeable at yaw="+angle+": "+plan.error);
                var enclosure=Rect.MinMaxRect(Mathf.Min(hole.xMin,landing.xMin),Mathf.Min(hole.yMin,landing.yMin),Mathf.Max(hole.xMax,landing.xMax),Mathf.Max(hole.yMax,landing.yMax));
                var ends=plan.additions.Where(r=>catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>())
                    .SelectMany(r=>WallGeometry.At(catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>(),r.position,r.rotation,r.scale)).SelectMany(s=>new[]{s.a,s.b}).ToArray();
                Check(ends.Length>0 && ends.All(p=>p.x>=enclosure.xMin-.001f && p.x<=enclosure.xMax+.001f && p.z>=enclosure.yMin-.001f && p.z<=enclosure.yMax+.001f),"Upper enclosure has no side/back extension at yaw="+angle);
                var coverage=new List<List<Vector2>>();
                foreach(var tile in plan.additions.Where(r=>r.floorTriangles!=null && Mathf.Abs(r.position.y-level)<.06f))
                    for(int i=0;i<tile.floorTriangles.Length;i+=3)
                    {var p=new List<Vector2>();for(int j=0;j<3;j++){var v=tile.position+tile.rotation*tile.floorTriangles[i+j];p.Add(new Vector2(v.x,v.z));}coverage.Add(p);}
                var region=new RoomGeometry.Region{outline=new(){landing.min,new(landing.xMax,landing.yMin),landing.max,new(landing.xMin,landing.yMax)},min=landing.min,max=landing.max};
                Check(RoomGeometry.Missing(region,coverage).Sum(p=>Mathf.Abs(RoomGeometry.SignedArea(p)))<.0001f,"Full landing floor is included before building at yaw="+angle);
                Check(Mathf.Abs(coverage.Sum(p=>Mathf.Abs(RoomGeometry.SignedArea(p)))-(enclosure.width*enclosure.height-hole.width*hole.height))<.001f,"Only the frontal strip receives new upper floor at yaw="+angle);
                // Allow only wall thickness outside the compact footprint, not a
                // spare 4-unit tile on either side or behind the staircase.
                plot.Volume.center=new Vector3(enclosure.center.x,10,enclosure.center.y);
                plot.Volume.size=new Vector3(enclosure.width+.6f,40,enclosure.height+.6f);
                Physics.SyncTransforms();
                Check(BuildPlacement.IsValid(stairs,plot,record.position,record.rotation,record.cellCenter) && string.IsNullOrEmpty(StairConstruction.Plan(catalog,plot,record).error),"Stair fits narrow plot without lateral/rear tile clearance at yaw="+angle);
                // The exit side must still have a full landing; tightening it to
                // the stair's own bounds may not leave the player over a void.
                var front=landing.center-hole.center;
                if(Mathf.Abs(front.x)>Mathf.Abs(front.y)){plot.Volume.size-=new Vector3(3,0,0);plot.Volume.center-=new Vector3(Mathf.Sign(front.x)*1.5f,0,0);}
                else {plot.Volume.size-=new Vector3(0,0,3);plot.Volume.center-=new Vector3(0,0,Mathf.Sign(front.y)*1.5f);}
                Check(!string.IsNullOrEmpty(StairConstruction.Plan(catalog,plot,record).error),"Insufficient frontal landing space is still rejected at yaw="+angle);
                plot.Volume.center=Vector3.zero;plot.Volume.size=Vector3.one*200;
            }
            var stairRecord=new BuildRecord{id="hall-stairs",definitionId=stairs.id,propertyId=plot.propertyId,sceneName=scene.name,rotation=Quaternion.identity,scale=Vector3.one,finish=-1};
            var before=StairConstruction.Plan(catalog,plot,stairRecord);int cost=before.Cost(catalog);data.money=cost-1;
            Check(!ConstructionEconomy.Build(catalog,stairRecord,plot,money) && money.Money==cost-1 && plot.GetComponentsInChildren<PlacedBuildObject>().Length==0,"Insufficient funds creates neither stair nor partial hall");
            data.money=100000;var built=ConstructionEconomy.Build(catalog,stairRecord,plot,money);
            Check(built && money.Money==100000-cost,"Stairs, walls, hall floor and roof use one paid transaction");
            StairConstruction.Landing(catalog,plot,stairRecord,out var hall,out float y);Physics.SyncTransforms();
            Check(Physics.Raycast(new Vector3(hall.center.x,y+2,hall.center.y),Vector3.down,out var hit,3) && Mathf.Abs(hit.point.y-y)<.03f,"Real collider supports the player immediately past the last step");
            manager.Rebuild();var landingTile=hit.collider.GetComponentInParent<PlacedBuildObject>();
            Check(landingTile && FloorEditing.CanRemove(landingTile),"Upper stair landing is editable under the foundation-only rule");
            Check(!built.ProtectedFloor,"Nested intermediate landings do not classify the whole staircase as a floor");
            Check(ConstructionEconomy.Sell(catalog,built,money),"Sell only stairs before testing hall reuse");Physics.SyncTransforms();
            var reuse=StairConstruction.Plan(catalog,plot,stairRecord);Check(string.IsNullOrEmpty(reuse.error) && reuse.additions.Count==0,"Existing complete hall is reused without duplicate charges");
            var savedLanding=landingTile.Capture();var missingLanding=JsonUtility.FromJson<BuildRecord>(JsonUtility.ToJson(savedLanding));missingLanding.deleted=true;
            landingTile.Restore(missingLanding,catalog);manager.Rebuild();
            var repair=StairConstruction.Plan(catalog,plot,stairRecord);
            Check(string.IsNullOrEmpty(repair.error) && !repair.additions.Any(r=>r.floorTriangles!=null && Mathf.Abs(r.position.y-y)<.06f) && !repair.additions.Any(r=>catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>()),"Intentionally removed upper floor stays removed when a hall is reused");
            landingTile=ConstructionPersistence.Spawn(catalog,savedLanding,true,scene);manager.Rebuild();
            built=ConstructionPersistence.Spawn(catalog,stairRecord,true,scene);Physics.SyncTransforms();
            StairConstruction.Passage(catalog,plot,stairRecord,out var aperture,out _);
            var blockedFloor=BuildPlacement.Position(floor,new Vector3(aperture.center.x,y,aperture.center.y),Quaternion.identity);
            Check(!FloorEditing.CanPlace(floor,plot,blockedFloor,Quaternion.identity),"Manual floors cannot seal an active staircase opening");
            var floorRecord=new BuildRecord{id="loose-floor",definitionId=floor.id,propertyId=plot.propertyId,sceneName=scene.name,position=new Vector3(40,6.99f,40),rotation=Quaternion.identity,scale=Vector3.one,finish=-1};
            var loose=ConstructionPersistence.Spawn(catalog,floorRecord,true,scene);
            Check(FloorEditing.CanRemove(loose),"An upper-storey open tile is removable");
            int oldMoney=money.Money;
            Check(ConstructionEconomy.Sell(catalog,loose,money) && loose.deleted && money.Money==oldMoney+catalog.Cost(floor),"Selling allowed open floor records deletion and refunds its catalogue price");
            var anchor=new Vector3(landingTile.transform.position.x+2,y,landingTile.transform.position.z+2);
            var manual=BuildPlacement.Position(floor,anchor,Quaternion.identity);
            Check(Mathf.Abs((manual+floor.bounds.max).y-y)<.001f,"Manual floors align their walkable top to the storey height");
            typeof(RoomConstruction).GetProperty("PendingCost").GetSetMethod(true).Invoke(manager,new object[]{123});
            typeof(RoomConstruction).GetProperty("Warning").GetSetMethod(true).Invoke(manager,new object[]{"pending test"});
            manager.RefreshForEditing();
            Check(manager.PendingCost==123 && manager.Warning=="pending test","Read-only floor checks preserve pending automatic-floor costs and warnings");
        }
        finally{Object.DestroyImmediate(plot.gameObject);Object.DestroyImmediate(services);Object.DestroyImmediate(catalog);}
        RoomConstructionValidation.Run();
        StairPlacementClearanceValidation.Run();
        Debug.Log("STAIR_HALL_VALIDATED checks="+report.Count);
    }
    public static void Inspect()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var stairs=catalog.entries.Single(StairConstruction.IsStairs);
        var root=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(stairs.prefab));
        var lines=new List<string>{"Catalog bounds "+stairs.bounds};
        try
        {
            foreach(var box in root.GetComponentsInChildren<BoxCollider>(true))
            {
                lines.Add(box.name+" parent="+box.transform.parent.name+" center="+root.transform.InverseTransformPoint(box.transform.TransformPoint(box.center))+" size="+box.size);
                foreach(float sign in new[]{-1f,1f})lines.Add("  Z endpoint "+root.transform.InverseTransformPoint(box.transform.TransformPoint(box.center+Vector3.forward*box.size.z*.5f*sign)));
            }
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        File.WriteAllLines("Documentation/StairHallInspection.txt",lines);Debug.Log(string.Join("\n",lines));
    }
}
#endif
