#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JuegoCriminal.Construction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class BuildInputAndHeightValidation
{
    public static void Run()
    {
        var report=new List<string>();
        void Check(bool value,string text){report.Add((value?"PASS ":"FAIL ")+text);File.WriteAllLines("Documentation/BuildInputAndHeightValidation.txt",report);if(!value)throw new Exception(text);}
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var events=new GameObject("Isolated event system").AddComponent<EventSystem>();
        typeof(EventSystem).GetMethod("OnEnable",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(events,null);
        EventSystem.current=events; // Edit-mode fixtures do not invoke runtime OnEnable.
        var ui=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ConstructionUI.prefab"));
        var view=ui.GetComponent<BuildMenuView>();view.ConfigurePointerOnly();
        view.hud.SetActive(true);
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        try
        {
            Check(ui.GetComponentsInChildren<Selectable>(true).All(s=>s.navigation.mode==Navigation.Mode.None),"Authored construction controls have no keyboard navigation");
            var first=BuildMenuView.Button("Test mouse-only button",view.content);var second=BuildMenuView.Button("Second",view.content);
            Check(first.navigation.mode==Navigation.Mode.None && second.navigation.mode==Navigation.Mode.None,"New catalogue buttons also have no keyboard navigation");
            events.SetSelectedGameObject(first.gameObject);view.ConfigurePointerOnly();
            Check(!events.currentSelectedGameObject,"Stale keyboard focus is cleared within construction UI");
            int clicks=0;first.onClick.AddListener(()=>clicks++);
            ExecuteEvents.Execute(first.gameObject,new PointerEventData(events){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
            Check(clicks==1,"Mouse click still activates a construction button");
            foreach(var direction in new[]{MoveDirection.Left,MoveDirection.Right,MoveDirection.Up,MoveDirection.Down})
                ExecuteEvents.Execute(first.gameObject,new AxisEventData(events){moveDir=direction},ExecuteEvents.moveHandler);
            Check(!events.currentSelectedGameObject && clicks==1,"Directional UI events neither select nor activate another construction button");
            var outside=new GameObject("Unrelated menu button",typeof(RectTransform),typeof(Button));events.SetSelectedGameObject(outside);view.ConfigurePointerOnly();
            Check(events.currentSelectedGameObject==outside,"Other menus retain their own keyboard focus");Object.DestroyImmediate(outside);
        }
        finally
        {
            Object.DestroyImmediate(ui);
            typeof(EventSystem).GetMethod("OnDisable",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(events,null);
            Object.DestroyImmediate(events.gameObject);
        }
        var plot=new GameObject("Surface level test plot").AddComponent<BuildPlot>();plot.Volume.size=Vector3.one*200;plot.catalog=catalog;
        var floor=catalog.entries.Single(FloorEditing.IsFloor);
        var wall=new GameObject("Upper storey wall").AddComponent<WallTopology>();wall.height=7;wall.transform.position=new Vector3(0,7,0);
        var box=wall.gameObject.AddComponent<BoxCollider>();box.center=new Vector3(2,3.5f,0);box.size=new Vector3(4,7,.25f);
        var slab=new GameObject("Upper slab").AddComponent<BuildFloorCoverage>();slab.transform.position=new Vector3(10,14,0);slab.GetComponent<BoxCollider>().size=new Vector3(4,.02f,4);
        try
        {
            Physics.SyncTransforms();
            foreach(var sample in new[]{(height:12f,expected:14f),(height:8f,expected:7f)})
            {
                var ray=new Ray(new Vector3(2,sample.height,-4),Vector3.forward);
                Check(Physics.Raycast(ray,out var hit,10),"Wall support hit at height "+sample.height);
                Check(BuildPlacement.TrySurfacePoint(floor,plot,ray,true,hit,Vector3.zero,out var point) && Mathf.Abs(point.y-sample.expected)<.001f,"Horizontal floor preview resolves the intended storey before fallback: "+sample.expected);
                Check(Mathf.Abs(point.x-hit.point.x)<.001f && Mathf.Abs(point.z-hit.point.z)<.001f,"Wall support preserves pointer X/Z instead of teleporting to the wall pivot");
            }
            var upward=new Ray(new Vector3(10,10,0),Vector3.up);
            Check(Physics.Raycast(upward,out var underside,10) && underside.normal.y<-.7f,"Upper slab underside hit");
            Check(BuildPlacement.TrySurfacePoint(floor,plot,upward,true,underside,Vector3.zero,out var high) && Mathf.Abs(high.y-14.01f)<.001f,"Underside targeting selects the upper slab's walkable height");
            var anchor=BuildFootprint.SnapAnchor(floor,plot,high,Quaternion.identity);
            var pose=BuildPlacement.Position(floor,anchor,Quaternion.identity);
            Check(Mathf.Abs((pose+floor.bounds.max).y-high.y)<.001f,"Grid snapping and floor pivot preserve the selected height");
            var down=new Ray(new Vector3(10,18,0),Vector3.down);Physics.Raycast(down,out var top,10);
            Check(BuildPlacement.TrySurfacePoint(floor,plot,down,true,top,Vector3.zero,out var onTop) && Mathf.Abs(onTop.y-14.01f)<.001f,"Top-face targeting still uses the correct upper height");
        }
        finally{Object.DestroyImmediate(wall.gameObject);Object.DestroyImmediate(slab.gameObject);Object.DestroyImmediate(plot.gameObject);}
        FloorFoundationValidation.Run();
        Debug.Log("BUILD_INPUT_HEIGHT_VALIDATION_OK checks="+report.Count);
    }
}
#endif
