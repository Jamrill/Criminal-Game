using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JuegoCriminal.Construction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class ConstructionGuidesValidation
{
    public static void RunAll(){Run();WallFamilyValidation.Full();Debug.Log("CONSTRUCTION_GUIDES_REGRESSIONS_OK");}
    public static void Apply()
    {
        const string path="Assets/ConstructionCatalog.asset";
        string backup="Documentation/ConstructionGuidesBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);File.Copy(path,backup+"/ConstructionCatalog.asset");
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(path);var report=new List<string>{"Backup="+backup};
        foreach(var entry in catalog.entries)
        {
            if(!BuildFootprint.Uses(entry))continue;
            entry.footprintCells=new Vector2Int(Mathf.Max(1,Mathf.CeilToInt((entry.bounds.size.x-.05f)/4)),Mathf.Max(1,Mathf.CeilToInt((entry.bounds.size.z-.05f)/4)));
            report.Add(entry.title+": "+entry.footprintCells+" cells (local X/Z)");
        }
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();File.WriteAllLines("Documentation/ConstructionGuidesSetup.txt",report);
        Run();ConstructionMountValidation.Run();
    }
    public static void Run()
    {
        var report=new List<string>();
        void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines("Documentation/ConstructionGuidesValidation.txt",report);if(!ok)throw new Exception(label);}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>("Assets/ConstructionCatalog.asset");
        BuildEntry Entry(string name)=>catalog.entries.First(e=>e.prefab.name==name);
        var chair=Entry("Chair");var bed=Entry("Bed");var lamp=Entry("Light_point");
        var plot=new GameObject("Guide test plot").AddComponent<BuildPlot>();plot.propertyId=71;plot.catalog=catalog;plot.Volume.isTrigger=true;plot.Volume.size=new Vector3(100,40,100);
        var floor=new GameObject("Guide test ground",typeof(BoxCollider));floor.GetComponent<BoxCollider>().size=new Vector3(40,.2f,40);floor.transform.position=Vector3.down*.1f;
        var roof=new GameObject("Guide test ceiling",typeof(BoxCollider),typeof(BuildFloorCoverage));roof.transform.SetParent(plot.transform,false);roof.transform.position=Vector3.up*7;roof.GetComponent<BoxCollider>().size=new Vector3(40,.02f,40);
        PlacedBuildObject Spawn(BuildEntry e,string id,Vector3 anchor,Quaternion rotation)
        {
            var position=e.mount==BuildMount.Ceiling?anchor-rotation*new Vector3(e.bounds.center.x,e.bounds.max.y,e.bounds.center.z):BuildPlacement.Position(e,anchor,rotation);
            return ConstructionPersistence.Spawn(catalog,new BuildRecord{id=id,definitionId=e.id,propertyId=plot.propertyId,sceneName=scene.name,position=position,rotation=rotation,scale=e.prefab.transform.localScale,finish=-1},true,scene);
        }
        using(var guides=new BuildPlacementGuides())
        {
            Check(BuildFootprint.Cells(chair)==Vector2Int.one && BuildFootprint.Cells(lamp)==Vector2Int.one,"Chair and ceiling light reserve one 4x4 cell");
            Check(BuildFootprint.Cells(bed)==new Vector2Int(2,1) && BuildFootprint.Cells(Entry("Sofa"))==new Vector2Int(2,1) && BuildFootprint.Cells(Entry("Table"))==new Vector2Int(2,1),"Bed, sofa and table reserve two cells on the model's long axis");
            Check(!BuildFootprint.Uses(Entry("Wall_complete")) && !BuildFootprint.Uses(Entry("Floor")) && !BuildFootprint.Uses(Entry("Door")),"Structural walls, floors and mounted doors retain their existing placement rules");
            var anchor=BuildFootprint.SnapAnchor(chair,plot,new Vector3(1,0,1),Quaternion.identity);
            Check(Vector3.Distance(anchor,new Vector3(2,0,2))<.001f,"Small objects snap to centres of 4x4 grid cells");
            var rect=BuildFootprint.RectOnGrid(bed,plot,BuildPlacement.Position(bed,new Vector3(4,0,2),Quaternion.identity),Quaternion.identity);
            Check(Vector2.Distance(rect.size,new Vector2(8,4))<.001f,"Double footprint matches actual 8x4 model direction");
            var rotation=Quaternion.Euler(0,90,0);var rotated=BuildFootprint.RectOnGrid(bed,plot,BuildPlacement.Position(bed,new Vector3(2,0,4),rotation),rotation);
            Check(Vector2.Distance(rotated.size,new Vector2(4,8))<.001f,"Rotating a double object rotates its reserved cells");
            var built=Spawn(chair,"existing-chair",anchor,Quaternion.identity);Physics.SyncTransforms();
            Check(!BuildFootprint.IsAvailable(chair,plot,BuildPlacement.Position(chair,anchor+Vector3.right,Quaternion.identity),Quaternion.identity),"Reserved cells reject overlap even when a small mesh leaves empty space");
            var neighbour=new Vector3(6,0,2);var position=BuildPlacement.Position(chair,neighbour,Quaternion.identity);
            Check(BuildFootprint.IsAvailable(chair,plot,position,Quaternion.identity) && BuildPlacement.IsValid(chair,plot,position,Quaternion.identity,neighbour),"Neighbouring touching cells remain legal with actual geometry checks");
            Check(BuildFootprint.IsAvailable(chair,plot,BuildPlacement.Position(chair,anchor+Vector3.up*7,Quaternion.identity),Quaternion.identity),"Furniture on another storey does not reserve the lower floor");
            Check(BuildPlacementGuides.TryAlignment(new Rect(0,0,4,4),new Rect(12,0,4,4),out var start,out var end) && Mathf.Abs(start.y-end.y)<.001f,"White alignment guide joins matching sides in a row");
            Check(BuildPlacementGuides.TryAlignment(new Rect(0,0,4,4),new Rect(0,12,4,4),out start,out end) && Mathf.Abs(start.x-end.x)<.001f,"White alignment guide joins matching sides in a column");
            Check(!BuildPlacementGuides.TryAlignment(new Rect(0,0,4,4),new Rect(12,.25f,4,4),out _,out _),"Misaligned objects do not show a false alignment line");
            guides.Show(chair,catalog,plot,position,Quaternion.identity,neighbour,true,null,3,24);
            Check(guides.Visible && guides.GridSegments>0 && guides.FootprintSegments==4 && guides.AlignmentSegments>0,"Ground shows grid, preview footprint and alignment to existing furniture");
            floor.SetActive(false);var tiles=new GameObject("Separated floor tiles");
            for(int x=0;x<4;x++)for(int z=0;z<3;z++)
            {
                var tile=new GameObject("Tile",typeof(BoxCollider));tile.transform.SetParent(tiles.transform,false);tile.transform.position=new Vector3(x*4+2,-.1f,z*4+2);tile.GetComponent<BoxCollider>().size=new Vector3(3.96f,.2f,3.96f);
            }
            Physics.SyncTransforms();var seamAnchor=new Vector3(6,0,6);
            guides.Show(chair,catalog,plot,BuildPlacement.Position(chair,seamAnchor,Quaternion.identity),Quaternion.identity,seamAnchor,true,null,3,24);
            Check(guides.GridSegments>0,"Small gaps between modular floor tiles do not erase the grid at its vertices");
            Object.DestroyImmediate(tiles);floor.SetActive(true);Physics.SyncTransforms();
            var bedAnchor=new Vector3(12,0,2);guides.Show(bed,catalog,plot,BuildPlacement.Position(bed,bedAnchor,Quaternion.identity),Quaternion.identity,bedAnchor,true,null,3,24);
            Check(guides.FootprintSegments==5,"Double object footprint includes the dividing cell line");
            Physics.SyncTransforms();
            Check(BuildMounting.TryPose(lamp,catalog,plot,new Ray(new Vector3(2,2,2),Vector3.up),0,null,out var pose),"Ceiling light snaps to a real ceiling cell");
            Check(BuildMounting.IsValid(lamp,catalog,plot,pose),"Furniture underneath does not consume ceiling light cells");
            guides.Show(lamp,catalog,plot,pose.position,pose.rotation,pose.anchor,true,null,3,24);
            Check(guides.Visible && guides.GridSegments>0 && guides.FootprintSegments==4,"Ceiling grid and footprint are rendered on the underside");
            var light=Spawn(lamp,"existing-light",pose.anchor,pose.rotation);Physics.SyncTransforms();
            Check(!BuildMounting.IsValid(lamp,catalog,plot,pose),"A second light cannot reserve an occupied ceiling cell");
            var saved=JsonUtility.FromJson<BuildRecord>(JsonUtility.ToJson(light.Capture()));light.Restore(saved,catalog);
            Check(!BuildFootprint.IsAvailable(lamp,plot,pose.position,pose.rotation),"Ceiling cell reservation survives save/restore without new save fields");
            built.deleted=true;
            Check(BuildFootprint.IsAvailable(chair,plot,BuildPlacement.Position(chair,anchor,Quaternion.identity),Quaternion.identity),"Sold objects release their reserved cells immediately");
            guides.Hide();Check(!guides.Visible,"Guides can be hidden on tablet, pause, mode exit or selection change");
            var gridOrigin=new GameObject("Rotated grid");gridOrigin.transform.SetParent(plot.transform,false);gridOrigin.transform.SetPositionAndRotation(new Vector3(20,0,20),Quaternion.Euler(0,90,0));plot.gridOrigin=gridOrigin.transform;
            var oriented=BuildFootprint.SnapAnchor(chair,plot,gridOrigin.transform.TransformPoint(new Vector3(1,3,1)),gridOrigin.transform.rotation);
            Check(Vector3.Distance(gridOrigin.transform.InverseTransformPoint(oriented),new Vector3(2,3,2))<.001f,"Grid respects authored origin, storey height and rotated plots");
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Debug.Log("CONSTRUCTION_GUIDES_VALIDATION_OK checks="+report.Count);
    }
}
