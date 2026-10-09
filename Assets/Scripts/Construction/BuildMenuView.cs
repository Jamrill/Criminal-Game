using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace JuegoCriminal.Construction
{
    public sealed class BuildMenuView : MonoBehaviour
    {
        public GameObject menu, hud;
        public Button buildButton, wallsButton, furnitureButton, finishButton;
        public TMP_Text status, buildLabel;
        public Transform content;
        public Button propertiesButton, hudPropertiesButton;
        public PropertyListView propertiesPanel;
        public void SetPropertiesVisible(bool visible)
        {
            if (!propertiesPanel) return;
            propertiesPanel.gameObject.SetActive(visible);
            var rect=menu.GetComponent<RectTransform>();
            rect.sizeDelta=new Vector2(rect.sizeDelta.x,visible ? 620 : 230);
        }
        // Idempotent prefab upgrade: preserves existing layout, colours and buttons.
        public void EnsurePropertiesUI()
        {
            if (!propertiesButton) propertiesButton=Button("Propiedades",menu.transform);
            if (!hudPropertiesButton)
            {
                hudPropertiesButton=Button("Propiedades",hud.transform);
                hudPropertiesButton.transform.SetSiblingIndex(2);
            }
            if (!propertiesPanel)
            {
                var panel=new GameObject("PropertiesPanel",typeof(RectTransform),typeof(VerticalLayoutGroup),typeof(LayoutElement),typeof(PropertyListView));
                panel.transform.SetParent(menu.transform,false);
                panel.GetComponent<LayoutElement>().preferredHeight=370;
                var layout=panel.GetComponent<VerticalLayoutGroup>(); layout.childForceExpandHeight=false; layout.spacing=8;
                propertiesPanel=panel.GetComponent<PropertyListView>();
                propertiesPanel.message=Text("Propiedades",panel.transform);
                propertiesPanel.message.fontSize=17;
                propertiesPanel.message.gameObject.AddComponent<LayoutElement>().preferredHeight=50;
                var scroll=new GameObject("PropertiesScroll",typeof(RectTransform),typeof(Image),typeof(Mask),typeof(ScrollRect),typeof(LayoutElement));
                scroll.transform.SetParent(panel.transform,false);
                scroll.GetComponent<Image>().color=new Color(.1f,.12f,.15f);
                scroll.GetComponent<LayoutElement>().preferredHeight=310;
                var list=new GameObject("Content",typeof(RectTransform),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));
                list.transform.SetParent(scroll.transform,false);
                var rect=list.GetComponent<RectTransform>(); rect.anchorMin=new Vector2(0,1); rect.anchorMax=Vector2.one; rect.pivot=new Vector2(.5f,1); rect.sizeDelta=Vector2.zero;
                var group=list.GetComponent<VerticalLayoutGroup>(); group.spacing=8; group.childForceExpandHeight=false;
                list.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                var sc=scroll.GetComponent<ScrollRect>(); sc.content=rect; sc.viewport=scroll.GetComponent<RectTransform>(); sc.horizontal=false; sc.scrollSensitivity=30;
                propertiesPanel.content=rect;
            }
            SetPropertiesVisible(false);
        }
        public void Populate(BuildCatalog catalog, BuildCategory category, Action<BuildEntry> selected)
        {
            foreach(Transform child in content) Destroy(child.gameObject);
            foreach(var entry in catalog.entries)
                if(entry.category==category && !entry.hiddenInMenu) { var e=entry; var b=Button(entry.title,content); b.onClick.AddListener(()=>selected(e)); }
        }
        public static Button Button(string label,Transform parent)
        {
            var go=new GameObject(label,typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement)); go.transform.SetParent(parent,false);
            go.GetComponent<Image>().color=new Color(.17f,.20f,.24f,.98f);
            go.GetComponent<LayoutElement>().preferredHeight=44;
            var text=Text(label,go.transform); text.rectTransform.anchorMin=Vector2.zero; text.rectTransform.anchorMax=Vector2.one;
            text.rectTransform.offsetMin=new Vector2(6,3); text.rectTransform.offsetMax=new Vector2(-6,-3);
            return go.GetComponent<Button>();
        }
        public static TMP_Text Text(string value,Transform parent)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
            var t=go.GetComponent<TextMeshProUGUI>(); t.font=TMP_Settings.defaultFontAsset; t.text=value; t.fontSize=20;
            t.alignment=TextAlignmentOptions.Center; t.raycastTarget=false; return t;
        }
        public static BuildMenuView Create()
        {
            var root=new GameObject("ConstructionUI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(BuildMenuView));
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay; root.GetComponent<Canvas>().sortingOrder=60;
            var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080);
            var view=root.GetComponent<BuildMenuView>();
            GameObject Panel(string name,Vector2 anchor,Vector2 size)
            {
                var g=new GameObject(name,typeof(RectTransform),typeof(Image)); g.transform.SetParent(root.transform,false);
                var r=g.GetComponent<RectTransform>(); r.anchorMin=r.anchorMax=anchor; r.pivot=anchor; r.sizeDelta=size;
                g.GetComponent<Image>().color=new Color(.06f,.08f,.11f,.95f); return g;
            }
            view.menu=Panel("TabMenu",new Vector2(.5f,.5f),new Vector2(500,170));
            var layout=view.menu.AddComponent<VerticalLayoutGroup>(); layout.padding=new RectOffset(20,20,20,20); layout.spacing=12;
            Text("Construccion · Tab para cerrar",view.menu.transform);
            view.buildButton=Button("Construir",view.menu.transform); view.buildLabel=view.buildButton.GetComponentInChildren<TMP_Text>();
            view.hud=Panel("BuildHUD",new Vector2(1,.5f),new Vector2(300,760));
            var group=view.hud.AddComponent<VerticalLayoutGroup>(); group.padding=new RectOffset(12,12,12,12); group.spacing=8; group.childControlHeight=true; group.childForceExpandHeight=false;
            view.wallsButton=Button("Paredes",view.hud.transform); view.furnitureButton=Button("Muebles",view.hud.transform);
            view.finishButton=Button("Acabado: colocar / pintar",view.hud.transform);
            var scroll=new GameObject("Catalog",typeof(RectTransform),typeof(Image),typeof(Mask),typeof(ScrollRect),typeof(LayoutElement)); scroll.transform.SetParent(view.hud.transform,false);
            scroll.GetComponent<Image>().color=new Color(.1f,.12f,.15f); scroll.GetComponent<Mask>().showMaskGraphic=true;
            scroll.GetComponent<LayoutElement>().preferredHeight=480;
            var content=new GameObject("Content",typeof(RectTransform),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter)); content.transform.SetParent(scroll.transform,false);
            var rect=content.GetComponent<RectTransform>(); rect.anchorMin=new Vector2(0,1); rect.anchorMax=Vector2.one; rect.pivot=new Vector2(.5f,1); rect.sizeDelta=Vector2.zero;
            var list=content.GetComponent<VerticalLayoutGroup>(); list.spacing=6; list.childControlHeight=true; list.childForceExpandHeight=false;
            content.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var sc=scroll.GetComponent<ScrollRect>(); sc.content=rect; sc.viewport=scroll.GetComponent<RectTransform>(); sc.horizontal=false; sc.scrollSensitivity=30;
            view.content=content.transform;
            view.status=Text("R: girar · rueda: distancia\nEsc: salir",view.hud.transform); view.status.gameObject.AddComponent<LayoutElement>().preferredHeight=105;
            view.EnsurePropertiesUI();
            view.menu.SetActive(false); view.hud.SetActive(false); return view;
        }
    }
}
