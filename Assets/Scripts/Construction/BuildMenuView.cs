using System;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;
using JuegoCriminal.Localization;
using static JuegoCriminal.Localization.GameLanguage;
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
        [SerializeField] private Button buttonTemplate;
        BuildCatalog activeCatalog;
        BuildCategory activeCategory;
        Action<BuildEntry> selection;
        void OnEnable(){GameLanguage.Changed+=RefreshLanguage;ConfigurePointerOnly();RefreshLanguage();}
        void OnDisable()=>GameLanguage.Changed-=RefreshLanguage;
        public void ConfigurePointerOnly()
        {
            foreach(var selectable in GetComponentsInChildren<Selectable>(true))selectable.navigation=new Navigation{mode=Navigation.Mode.None};
            ClearKeyboardSelection();
        }
        void LateUpdate()=>ClearKeyboardSelection();
        void ClearKeyboardSelection()
        {
            var events=EventSystem.current;var selected=events?events.currentSelectedGameObject:null;
            if(selected && selected.transform.IsChildOf(transform))events.SetSelectedGameObject(null);
        }
        public void RefreshCatalogPrices()
        {
            if(activeCatalog)Populate(activeCatalog,activeCategory,selection);
            if(propertiesPanel && propertiesPanel.gameObject.activeInHierarchy)propertiesPanel.Refresh();
        }
        void RefreshLanguage()
        {
            void Label(Button button,string en,string es){var text=button?button.GetComponentInChildren<TMP_Text>(true):null;if(text)text.text=T(en,es);}
            Label(wallsButton,"Walls","Paredes");Label(furnitureButton,"Furniture","Muebles");
            Label(propertiesButton,"Properties","Propiedades");Label(hudPropertiesButton,"Properties","Propiedades");
            Label(finishButton,"Finish / paint","Acabado / pintar");
            var title=menu?menu.transform.Find("Label"):null;
            if(title)title.GetComponent<TMP_Text>().text=T("Tablet · Tab to close","Tablet · Tab para cerrar");
            if(activeCatalog)Populate(activeCatalog,activeCategory,selection);
        }
        public void SetPropertiesVisible(bool visible)
        {
            if (!propertiesPanel) return;
            propertiesPanel.gameObject.SetActive(visible);
        }
        // Legacy/fallback creation is optional: an authored prefab may deliberately
        // omit a navigation button or label. Runtime must not recreate it.
        public void EnsurePropertiesUI(bool createMissingControls=true)
        {
            if (createMissingControls && !propertiesButton) propertiesButton=Button(T("Properties","Propiedades"),menu.transform);
            if (createMissingControls && !hudPropertiesButton)
            {
                hudPropertiesButton=Button(T("Properties","Propiedades"),hud.transform);
                hudPropertiesButton.transform.SetSiblingIndex(2);
            }
            if (createMissingControls && !propertiesPanel)
            {
                var panel=new GameObject("PropertiesPanel",typeof(RectTransform),typeof(PropertyListView));
                panel.transform.SetParent(menu.transform,false);
                Place(panel.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(600,370));
                propertiesPanel=panel.GetComponent<PropertyListView>();
                propertiesPanel.message=Text(T("Properties","Propiedades"),panel.transform);
                propertiesPanel.message.fontSize=17;
                Place(propertiesPanel.message.transform,new Vector2(.5f,1),new Vector2(0,-25),new Vector2(580,50));
                var scroll=new GameObject("PropertiesScroll",typeof(RectTransform),typeof(Image),typeof(Mask),typeof(ScrollRect),typeof(LayoutElement));
                scroll.transform.SetParent(panel.transform,false);
                scroll.GetComponent<Image>().color=new Color(.1f,.12f,.15f);
                Place(scroll.transform,new Vector2(.5f,0),new Vector2(0,155),new Vector2(580,310));
                var list=new GameObject("Content",typeof(RectTransform),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));
                list.transform.SetParent(scroll.transform,false);
                var rect=list.GetComponent<RectTransform>(); rect.anchorMin=new Vector2(0,1); rect.anchorMax=Vector2.one; rect.pivot=new Vector2(.5f,1); rect.sizeDelta=Vector2.zero;
                var group=list.GetComponent<VerticalLayoutGroup>(); group.spacing=8; group.childForceExpandHeight=false;
                list.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                var sc=scroll.GetComponent<ScrollRect>(); sc.content=rect; sc.viewport=scroll.GetComponent<RectTransform>(); sc.horizontal=false; sc.scrollSensitivity=30;
                propertiesPanel.content=rect;
            }
            SetPropertiesVisible(false);
            RefreshLanguage();
        }
        public void Populate(BuildCatalog catalog, BuildCategory category, Action<BuildEntry> selected)
        {
            activeCatalog=catalog;activeCategory=category;selection=selected;
            for(int i=content.childCount-1;i>=0;i--)
            {
                var child=content.GetChild(i);child.gameObject.SetActive(false);
                if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);
            }
            foreach(var entry in catalog.entries)
                if(entry.category==category && !entry.hiddenInMenu && !entry.IsOpening) { var e=entry; var b=Button(ConstructionText.Title(entry)+"\n$"+catalog.Cost(entry),content); b.onClick.AddListener(()=>selected(e)); }
            if(category==BuildCategory.Walls)
                foreach(var entry in catalog.entries)
                    if(entry.IsOpening && !entry.hiddenInMenu) { var e=entry;var b=Button(T("Convert wall: {0}","Convertir pared: {0}",entry.prefab.name.StartsWith("Window",StringComparison.OrdinalIgnoreCase)?T("window","ventana"):T("doorway","puerta"))+"\n$"+catalog.Cost(entry),content);b.onClick.AddListener(()=>selected(e)); }
        }
        static void Place(Transform target,Vector2 anchor,Vector2 position,Vector2 size)
        {var rect=(RectTransform)target;rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;}
        public static Button Button(string label,Transform parent)
        {
            var view=parent.GetComponentInParent<BuildMenuView>();
            if(view && view.buttonTemplate)
            {
                var button=JuegoCriminal.UI.ButtonTemplate.Create(view.buttonTemplate,parent,label,out var caption);
                button.navigation=new Navigation{mode=Navigation.Mode.None};
                var element=button.GetComponent<LayoutElement>()??button.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight=44;caption.text=label;caption.fontSize=20;caption.alignment=TextAlignmentOptions.Center;
                caption.rectTransform.offsetMin=new Vector2(6,3);caption.rectTransform.offsetMax=new Vector2(-6,-3);
                return button;
            }
            var go=new GameObject(label,typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement)); go.transform.SetParent(parent,false);
            go.GetComponent<Image>().color=new Color(.17f,.20f,.24f,.98f);
            go.GetComponent<LayoutElement>().preferredHeight=44;
            var text=Text(label,go.transform); text.rectTransform.anchorMin=Vector2.zero; text.rectTransform.anchorMax=Vector2.one;
            text.rectTransform.offsetMin=new Vector2(6,3); text.rectTransform.offsetMax=new Vector2(-6,-3);
            var result=go.GetComponent<Button>();result.navigation=new Navigation{mode=Navigation.Mode.None};return result;
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
            view.menu=Panel("TabMenu",new Vector2(.5f,.5f),new Vector2(900,780));
            Place(Text(T("Tablet · Tab to close","Tablet · Tab para cerrar"),view.menu.transform).transform,new Vector2(.5f,1),new Vector2(0,-35),new Vector2(700,50));
            view.buildButton=Button(T("Build","Construir"),view.menu.transform); view.buildLabel=view.buildButton.GetComponentInChildren<TMP_Text>();
            Place(view.buildButton.transform,new Vector2(.5f,1),new Vector2(-180,-100),new Vector2(300,60));
            view.propertiesButton=Button(T("Properties","Propiedades"),view.menu.transform);
            Place(view.propertiesButton.transform,new Vector2(.5f,1),new Vector2(180,-100),new Vector2(300,60));
            view.hud=Panel("BuildHUD",new Vector2(1,.5f),new Vector2(500,760));
            view.wallsButton=Button(T("Walls","Paredes"),view.hud.transform); view.furnitureButton=Button(T("Furniture","Muebles"),view.hud.transform);
            view.finishButton=Button(T("Finish / paint","Acabado / pintar"),view.hud.transform);
            view.hudPropertiesButton=Button(T("Properties","Propiedades"),view.hud.transform);
            int column=0;
            foreach(var b in new[]{view.wallsButton,view.furnitureButton,view.hudPropertiesButton,view.finishButton})
                Place(b.transform,new Vector2(0,1),new Vector2(65+column++*123,-36),new Vector2(116,48));
            var scroll=new GameObject("Catalog",typeof(RectTransform),typeof(Image),typeof(Mask),typeof(ScrollRect),typeof(LayoutElement)); scroll.transform.SetParent(view.hud.transform,false);
            scroll.GetComponent<Image>().color=new Color(.1f,.12f,.15f); scroll.GetComponent<Mask>().showMaskGraphic=true;
            Place(scroll.transform,new Vector2(.5f,1),new Vector2(0,-350),new Vector2(476,550));
            var content=new GameObject("Content",typeof(RectTransform),typeof(GridLayoutGroup),typeof(ContentSizeFitter),typeof(BuildCatalogGrid)); content.transform.SetParent(scroll.transform,false);
            var rect=content.GetComponent<RectTransform>(); rect.anchorMin=new Vector2(0,1); rect.anchorMax=Vector2.one; rect.pivot=new Vector2(.5f,1); rect.sizeDelta=Vector2.zero;
            content.GetComponent<GridLayoutGroup>().spacing=new Vector2(8,8);
            content.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var sc=scroll.GetComponent<ScrollRect>(); sc.content=rect; sc.viewport=scroll.GetComponent<RectTransform>(); sc.horizontal=false; sc.scrollSensitivity=30;
            view.content=content.transform;
            view.status=Text(T("R: rotate · Wheel: distance\nEsc: exit","R: girar · Rueda: distancia\nEsc: salir"),view.hud.transform);
            Place(view.status.transform,new Vector2(.5f,0),new Vector2(0,64),new Vector2(476,116));
            view.EnsurePropertiesUI();
            view.menu.SetActive(false); view.hud.SetActive(false); return view;
        }
    }
}
