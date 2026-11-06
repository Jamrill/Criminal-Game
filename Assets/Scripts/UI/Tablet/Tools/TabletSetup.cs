#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using JuegoCriminal.Construction;
using JuegoCriminal.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class TabletSetup
{
    public const string UIPath="Assets/Prefabs/UI/ConstructionUI.prefab",PlayerPath="Assets/Prefabs/Player.prefab",ButtonPath="Assets/Prefabs/UI/Button_Tablet.prefab";
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Close Play mode first.");
        string backup="Documentation/TabletBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
        foreach(string path in new[]{UIPath,PlayerPath,"Assets/Scenes/10_World_City.unity"})File.Copy(path,Path.Combine(backup,Path.GetFileName(path)));
        if(!AssetDatabase.LoadAssetAtPath<Button>(ButtonPath))
        {
            var root=new GameObject("Temporary tablet style",typeof(RectTransform));
            var button=TabletLayout.CreateButton(root.transform,"Button_Tablet","Application","Aplicación",Vector2.zero,new Vector2(228,46));
            var states=button.gameObject.AddComponent<ButtonTextStateColors>();var so=new SerializedObject(states);
            so.FindProperty("normal").colorValue=new Color(.88f,.87f,.82f);so.FindProperty("highlighted").colorValue=new Color(1,.8f,.43f);so.FindProperty("pressed").colorValue=Color.white;so.FindProperty("selected").colorValue=new Color(1,.8f,.43f);so.FindProperty("disabled").colorValue=new Color(.45f,.47f,.45f);so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(button.gameObject,ButtonPath);Object.DestroyImmediate(root);
        }
        var prefab=PrefabUtility.LoadPrefabContents(UIPath);
        try
        {
            var view=prefab.GetComponent<BuildMenuView>();
            if(view.menu)Object.DestroyImmediate(view.menu);
            view.buildButton=null;view.buildLabel=null;view.propertiesPanel=null;view.propertiesButton=null;view.tablet=null;
            TabletLayout.Create(view,AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Tablet.png"),AssetDatabase.LoadAssetAtPath<Button>(ButtonPath));
            view.EnsureTablet();view.menu.SetActive(true);view.hud.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(prefab,UIPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
        prefab=PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var view=prefab.GetComponentInChildren<BuildMenuView>(true);
            if(!view || !view.tablet)throw new Exception("Player did not inherit the new tablet");
            // Only the tablet/menu visibility belongs to this migration. Preserve
            // existing construction HUD/catalog overrides inside the player.
            view.menu.SetActive(true);view.gameObject.SetActive(true);view.hud.SetActive(false);
            view.tablet.SetOpen(false,true);PrefabUtility.SaveAsPrefabAsset(prefab,PlayerPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        foreach(var hud in Object.FindObjectsByType<MoneyHUD>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(h=>h.gameObject.scene==scene))
        {
            var label=new SerializedObject(hud).FindProperty("moneyText").objectReferenceValue as TMP_Text;
            if(label)label.gameObject.SetActive(false);
        }
        foreach(var label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.gameObject.scene==scene&&(t.name=="TimeText"||t.name=="MoneyText")&&t.GetComponentInParent<GameplayHUD>(true)))label.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();Debug.Log("TABLET_SETUP_OK backup="+backup);
        TabletValidation.Run();
    }
}
#endif
