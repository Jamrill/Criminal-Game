using JuegoCriminal.Construction;
using JuegoCriminal.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JuegoCriminal.UI
{
    // Native UI layout: authored into ConstructionUI by the setup tool, with the
    // same builder available for isolated scenes which use the fallback UI.
    public static class TabletLayout
    {
        static readonly Color Ink=new(.035f,.055f,.061f,1),Panel=new(.055f,.075f,.08f,1),Gold=new(.82f,.61f,.31f,1),Text=new(.88f,.87f,.82f,1);
        public static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=size;return r;
        }
        static Image Box(string name,Transform parent,Vector2 p,Vector2 size,Color color)
        {var r=Rect(name,parent,p,size);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
        static TMP_Text Label(string name,Transform parent,Vector2 p,Vector2 size,string en,string es,float font=22)
        {
            var r=Rect(name,parent,p,size);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=TMP_Settings.defaultFontAsset;t.fontSize=font;t.color=Text;t.alignment=TextAlignmentOptions.MidlineLeft;
            t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;
            if(en!=null)LocalizedText.Bind(t,en,es);return t;
        }
        public static Button CreateButton(Transform parent,string name,string en,string es,Vector2 p,Vector2 size,Button template=null)
        {
            if(template)
            {
                Button instance;
#if UNITY_EDITOR
                if(!Application.isPlaying && UnityEditor.EditorUtility.IsPersistent(template))instance=((GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(template.gameObject,parent)).GetComponent<Button>();
                else
#endif
                    instance=Object.Instantiate(template,parent,false);
                instance.name=name;
                UIButtonAudio.Ensure(instance);
                var rect=(RectTransform)instance.transform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=p;rect.sizeDelta=size;
                var caption=instance.GetComponentInChildren<TMP_Text>();caption.rectTransform.sizeDelta=new Vector2(size.x-34,size.y);
                var rule=instance.transform.Find("FineRule") as RectTransform;if(rule){rule.sizeDelta=new Vector2(size.x,1);rule.anchoredPosition=new Vector2(0,-size.y+1);}
                LocalizedText.Bind(caption,en,es);return instance;
            }
            var r=Rect(name,parent,p,size);var image=r.gameObject.AddComponent<Image>();image.color=Color.white;
            var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;button.navigation=new Navigation{mode=Navigation.Mode.None};
            UIButtonAudio.Ensure(button);
            var colors=button.colors;colors.normalColor=Panel;colors.highlightedColor=new Color(.23f,.18f,.105f);colors.pressedColor=new Color(.36f,.26f,.12f);colors.selectedColor=colors.highlightedColor;colors.disabledColor=new Color(.04f,.05f,.05f,.6f);colors.fadeDuration=.12f;button.colors=colors;
            Box("FineRule",r,new Vector2(0,-size.y+1),new Vector2(size.x,1),new Color(Gold.r,Gold.g,Gold.b,.28f));
            Label("Caption",r,new Vector2(18,0),new Vector2(size.x-34,size.y),en,es,20);
            var layout=r.gameObject.AddComponent<LayoutElement>();layout.preferredHeight=size.y;layout.minHeight=size.y;
            return button;
        }
        public static TabletView Create(BuildMenuView view,Sprite frame=null,Button template=null)
        {
            var body=Rect("Tablet",view.transform,Vector2.zero,new Vector2(1100,690));body.anchorMin=body.anchorMax=body.pivot=new Vector2(.5f,.5f);
            var tablet=body.gameObject.AddComponent<TabletView>();tablet.body=body;tablet.buttonTemplate=template;
            var chrome=Rect("Chrome",body,Vector2.zero,body.sizeDelta);tablet.chrome=chrome.gameObject.AddComponent<CanvasGroup>();
            var outer=Box("Frame",chrome,Vector2.zero,body.sizeDelta,new Color(.018f,.023f,.026f,1));if(frame){outer.sprite=frame;outer.color=Color.white;}
            Box("Screen",chrome,new Vector2(20,-20),new Vector2(1060,650),Ink).raycastTarget=true;
            Label("Brand",chrome,new Vector2(38,-35),new Vector2(260,38),"STREET LEGACY","STREET LEGACY",25).color=Gold;
            Label("Subtitle",chrome,new Vector2(38,-74),new Vector2(240,22),"PERSONAL TERMINAL","TERMINAL PERSONAL",12).characterSpacing=4;
            Box("SidebarRule",chrome,new Vector2(285,-110),new Vector2(1,540),new Color(Gold.r,Gold.g,Gold.b,.35f));
            tablet.closeHint=Label("CloseHint",chrome,new Vector2(730,-45),new Vector2(320,30),null,null,16);tablet.closeHint.alignment=TextAlignmentOptions.MidlineRight;
            tablet.navigation=new Button[6];tablet.pages=new GameObject[6];tablet.selectionMarks=new Image[6];
            string[] en={"Contacts","Legal affairs","Black market","Properties","Research","Map"};
            string[] es={"Contactos","Asuntos legales","Mercado negro","Propiedades","Investigación","Mapa"};
            var content=Box("ApplicationArea",chrome,new Vector2(310,-110),new Vector2(750,530),Panel);
            tablet.pageTitle=Label("PageTitle",content.transform,new Vector2(30,-20),new Vector2(690,45),null,null,30);tablet.pageTitle.color=Gold;
            Box("TitleRule",content.transform,new Vector2(30,-78),new Vector2(690,1),new Color(Gold.r,Gold.g,Gold.b,.4f));
            for(int i=0;i<6;i++)
            {
                var b=CreateButton(chrome,"App_"+(TabletSection)i,en[i],es[i],new Vector2(38,-120-i*55),new Vector2(228,46),template);
                tablet.navigation[i]=b;tablet.selectionMarks[i]=Box("Selected",b.transform,Vector2.zero,new Vector2(3,46),Gold);
                var caption=b.GetComponentInChildren<TMP_Text>();caption.rectTransform.anchoredPosition=new Vector2(48,0);caption.rectTransform.sizeDelta=new Vector2(174,46);caption.fontSize=19;
                Label("AppIndex",b.transform,new Vector2(12,0),new Vector2(30,46),(i+1).ToString("00"),(i+1).ToString("00"),13).color=Gold;
                var page=Rect("Page_"+(TabletSection)i,content.transform,new Vector2(24,-100),new Vector2(702,405));tablet.pages[i]=page.gameObject;
                if(i!=3)
                {
                    Label("EmptyTitle",page,new Vector2(24,-110),new Vector2(650,42),en[i],es[i],28).color=Gold;
                    var message=Label("Placeholder",page,new Vector2(24,-165),new Vector2(650,110),"This section will be available in a future update.","Este apartado estará disponible en una próxima actualización.",21);message.textWrappingMode=TextWrappingModes.Normal;
                }
            }
            var data=Box("Data",body,new Vector2(38,-482),new Vector2(228,156),Ink);tablet.dataPanel=(RectTransform)data.transform;
            Box("DataRule",data.transform,Vector2.zero,new Vector2(228,2),Gold);
            tablet.bankText=Label("BankBalance",data.transform,new Vector2(12,-12),new Vector2(210,40),null,null,17);
            tablet.timeText=Label("Time",data.transform,new Vector2(12,-58),new Vector2(210,40),null,null,19);
            tablet.notificationText=Label("Notifications",data.transform,new Vector2(12,-104),new Vector2(210,40),null,null,17);
            var propertyPage=tablet.pages[3].transform;
            var list=Rect("PropertyList",propertyPage,Vector2.zero,new Vector2(235,405)).gameObject.AddComponent<PropertyListView>();list.tablet=tablet;tablet.propertyList=list;
            list.message=Label("ListTitle",list.transform,Vector2.zero,new Vector2(235,44),"Properties","Propiedades",17);
            var scroll=Rect("Scroll",list.transform,new Vector2(0,-50),new Vector2(235,350));scroll.gameObject.AddComponent<RectMask2D>();var scrollView=scroll.gameObject.AddComponent<ScrollRect>();
            var hitArea=scroll.gameObject.AddComponent<Image>();hitArea.color=new Color(0,0,0,.01f);
            var listContent=Rect("Content",scroll,Vector2.zero,new Vector2(235,0));var layout=listContent.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=8;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;list.content=listContent;scrollView.content=listContent;scrollView.viewport=scroll;scrollView.horizontal=false;scrollView.scrollSensitivity=30;
            var detail=Rect("PropertyDetails",propertyPage,new Vector2(262,0),new Vector2(440,405));
            var photo=Box("PhotoPlaceholder",detail,Vector2.zero,new Vector2(440,150),Ink);tablet.photoPlaceholder=photo.gameObject;
            Label("PhotoCaption",photo.transform,new Vector2(22,-45),new Vector2(396,60),"PROPERTY PHOTO\nComing soon","FOTO DEL INMUEBLE\nPróximamente",18).alignment=TextAlignmentOptions.Center;
            tablet.propertyPhoto=Rect("Photo",detail,Vector2.zero,new Vector2(440,150)).gameObject.AddComponent<RawImage>();tablet.propertyPhoto.gameObject.SetActive(false);
            tablet.propertyName=Label("PropertyName",detail,new Vector2(0,-166),new Vector2(440,40),null,null,24);
            tablet.propertyPrice=Label("Price",detail,new Vector2(0,-208),new Vector2(440,34),null,null,22);tablet.propertyPrice.color=Gold;
            tablet.propertyState=Label("State",detail,new Vector2(0,-247),new Vector2(440,30),null,null,17);
            tablet.description=Label("Description",detail,new Vector2(0,-280),new Vector2(440,55),"Property description coming soon.","La descripción del inmueble estará disponible más adelante.",16);tablet.description.textWrappingMode=TextWrappingModes.Normal;
            tablet.purchaseButton=CreateButton(detail,"Purchase","Buy property","Comprar propiedad",new Vector2(0,-350),new Vector2(440,48),template);
            view.menu=body.gameObject;view.tablet=tablet;view.propertiesButton=tablet.navigation[3];view.propertiesPanel=list;
            tablet.Initialize();tablet.Select(TabletSection.Contacts);tablet.SetOpen(false,true);
            return tablet;
        }
    }
}
