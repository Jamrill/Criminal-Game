#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JuegoCriminal.Construction;
using JuegoCriminal.Core;
using JuegoCriminal.Localization;
using JuegoCriminal.Services;
using JuegoCriminal.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class TabletValidation
{
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Run()
    {
        var report=new List<string>();void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines("Documentation/TabletValidation.txt",report);if(!ok)throw new Exception(label);}
        var previous=GameLanguage.Current;bool hadLocale=PlayerPrefs.HasKey(GameLanguage.PreferenceKey);int locale=PlayerPrefs.GetInt(GameLanguage.PreferenceKey);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TabletSetup.UIPath));var view=root.GetComponent<BuildMenuView>();var tablet=view.tablet;tablet.Initialize();
        var canvas=root.GetComponent<Canvas>();var camera=new GameObject("UI capture camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.13f,.16f);camera.orthographic=true;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var target=new RenderTexture(1600,1000,24);camera.targetTexture=target;target.Create();
        var services=new GameObject("Isolated services");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();var properties=services.AddComponent<PropertyService>();
        foreach(Component service in new Component[]{money,properties})service.GetType().GetField("_save",Fields).SetValue(service,save);
        save.SetCurrent(new SaveData{slotId=1,money=5000,ownedProperties=Array.Empty<int>()});
        var plot=new GameObject("Warehouse").AddComponent<BuildPlot>();plot.propertyId=972;plot.displayName="Warehouse 12";plot.price=1250;plot.buildable=true;plot.Volume.size=Vector3.one*30;
        BuildPlot.Active.Add(plot); // Edit-mode fixtures do not receive OnEnable.
        try
        {
            Check(tablet && tablet.navigation.Length==6 && tablet.pages.Length==6,"Six authored applications and six content panels");
            Check(!view.buildButton && view.contextPrompt && (!view.hudPropertiesButton || !view.hudPropertiesButton.gameObject.activeSelf),"Build moved out of tablet; contextual prompt authored");
            Check(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions").FindAction("Player/BuildOrBuy").bindings[0].path=="<Keyboard>/b","Build/buy has separate B action; Tab action preserved");
            foreach(var language in new[]{GameLocale.English,GameLocale.Spanish})
            {
                GameLanguage.Set(language);LocalizedText.BindMenu(root);tablet.RefreshLanguage();
                for(int i=0;i<6;i++)
                {
                    tablet.navigation[i].onClick.Invoke();
                    Check(tablet.SelectedSection==(TabletSection)i && tablet.pages.Count(p=>p.activeSelf)==1,"Exclusive panel selection "+i+" locale="+language);
                }
                Check(tablet.navigation[0].GetComponentInChildren<TMP_Text>().text==(language==GameLocale.Spanish?"Contactos":"Contacts"),"Navigation follows locale "+language);
                Check(tablet.bankText.text.Contains("0") && !tablet.bankText.text.Contains("5000") && tablet.notificationText.text.Contains("0"),"Bank and notifications start independently at zero");
            }
            tablet.OpenProperty(plot);tablet.SetOpen(true,true);tablet.propertyList.Refresh();tablet.RefreshProperty();Canvas.ForceUpdateCanvases();tablet.UpdatePose();
            Check(tablet.SelectedProperty==plot && tablet.SelectedSection==TabletSection.Properties && tablet.propertyPrice.text.Contains("1"),"Deep link selects property and its price");
            Check(money.Money==5000 && !properties.IsOwned(972),"Opening property never spends money");
            Check(tablet.purchaseButton.interactable,"Affordable property has confirmation button");
            Capture("Documentation/TabletOpen.png");
            tablet.purchaseButton.onClick.Invoke();
            Check(properties.IsOwned(972) && money.Money==3750 && !tablet.purchaseButton.interactable,"Confirm purchase once at catalogue value");
            tablet.purchaseButton.onClick.Invoke();Check(money.Money==3750,"Repeated purchase cannot charge twice");
            foreach(var size in new[]{new Vector2Int(1600,1000),new Vector2Int(1920,1080),new Vector2Int(1280,720)})
            {
                camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);target=new RenderTexture(size.x,size.y,24);target.Create();camera.targetTexture=target;
                Canvas.ForceUpdateCanvases();tablet.SetOpen(false,true);Canvas.ForceUpdateCanvases();tablet.UpdatePose();
                var corners=new Vector3[4];tablet.dataPanel.GetWorldCorners(corners);var points=corners.Select(p=>camera.WorldToScreenPoint(p)).ToArray();
                Check(points.All(p=>p.x>=0&&p.x<=size.x&&p.y>=0&&p.y<=size.y),"Three readouts stay on screen when docked "+size);
                Check(tablet.chrome.alpha==0 && !tablet.chrome.blocksRaycasts && root.activeSelf && tablet.dataPanel.gameObject.activeInHierarchy,"Closed tablet persists without hidden interactive buttons "+size);
                if(size.x==1600)Capture("Documentation/TabletDocked.png");
            }
            var controller=new GameObject("Context action fixture").AddComponent<ConstructionController>();
            void Set(string name,object value)=>typeof(ConstructionController).GetField(name,Fields).SetValue(controller,value);
            Set("view",view);Set("contextPlot",plot);Set("properties",properties);Set("economy",money);controller.catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
            // Avoid instantiating the full player or loading a save: exercise the
            // same handler called by the input action with an isolated balance.
            save.SetCurrent(new SaveData{slotId=1,money=5000,ownedProperties=Array.Empty<int>()});controller.ContextAction();
            Check(tablet.IsOpen && tablet.SelectedProperty==plot && !properties.IsOwned(972)&&money.Money==5000,"B in unowned building opens details without purchase");
            tablet.BuySelected();controller.ContextAction();
            Check(controller.CapturesInput && !tablet.IsOpen && view.hud.activeSelf,"B in owned building activates construction without tablet");
            controller.ContextAction();Check(!controller.CapturesInput&&!view.hud.activeSelf,"B can leave construction");
            Set("view",null);Object.DestroyImmediate(controller.gameObject);
            Check(AssetDatabase.LoadAssetAtPath<GameObject>(TabletSetup.PlayerPath).GetComponentInChildren<BuildMenuView>(true).tablet,"Player inherits tablet from shared UI prefab");
            Debug.Log("TABLET_VALIDATION_OK checks="+report.Count);
        }
        finally
        {
            GameLanguage.Set(previous);if(hadLocale)PlayerPrefs.SetInt(GameLanguage.PreferenceKey,locale);else PlayerPrefs.DeleteKey(GameLanguage.PreferenceKey);PlayerPrefs.Save();
            Object.DestroyImmediate(root);Object.DestroyImmediate(services);BuildPlot.Active.Remove(plot);Object.DestroyImmediate(plot.gameObject);camera.targetTexture=null;Object.DestroyImmediate(camera.gameObject);target.Release();Object.DestroyImmediate(target);
        }
        void Capture(string path)
        {
            Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);RenderTexture.active=old;
        }
    }
}
#endif
