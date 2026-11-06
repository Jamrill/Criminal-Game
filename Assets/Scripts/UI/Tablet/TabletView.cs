using System;
using System.Linq;
using JuegoCriminal.Construction;
using JuegoCriminal.Core;
using JuegoCriminal.Environment;
using JuegoCriminal.Localization;
using JuegoCriminal.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static JuegoCriminal.Localization.GameLanguage;

namespace JuegoCriminal.UI
{
    public enum TabletSection { Contacts,LegalAffairs,BlackMarket,Properties,Research,Map }

    // The same three readouts travel with the tablet: no duplicate HUD values.
    // All layout references are serialized so the generated prefab remains editable.
    public sealed class TabletView : MonoBehaviour
    {
        public RectTransform body,dataPanel;
        public CanvasGroup chrome;
        public Button[] navigation=Array.Empty<Button>();
        public GameObject[] pages=Array.Empty<GameObject>();
        public Image[] selectionMarks=Array.Empty<Image>();
        public TMP_Text pageTitle,bankText,timeText,notificationText,closeHint,propertyName,propertyPrice,propertyState;
        public TMP_Text description;
        public RawImage propertyPhoto;
        public GameObject photoPlaceholder;
        public Button purchaseButton;
        public PropertyListView propertyList;
        public Button buttonTemplate;
        public Color accent=new(.82f,.61f,.31f,1);
        [Min(.05f)] public float slideDuration=.35f;
        [Min(0)] public float dockMargin=22;
        [Header("Reserved for future banking and notifications")]
        [SerializeField,Min(0)] int bankBalance;
        [SerializeField,Min(0)] int notificationCount;
        public bool IsOpen {get;private set;}
        public TabletSection SelectedSection {get;private set;}
        public BuildPlot SelectedProperty {get;private set;}
        float progress;
        DayNightCycle clock;
        EconomyService economy;
        PropertyService properties;
        bool wired;

        void Awake()
        {
            Initialize();
        }
        public void Initialize()
        {
            if(wired || navigation.Length!=6)return;wired=true;
            for(int i=0;i<navigation.Length;i++){var section=(TabletSection)i;navigation[i].onClick.AddListener(()=>Select(section));}
            if(purchaseButton)purchaseButton.onClick.AddListener(BuySelected);
            Select(TabletSection.Contacts);SetOpen(false,true);
        }
        void OnEnable(){GameLanguage.Changed+=RefreshLanguage;RefreshLanguage();}
        void OnDisable()=>GameLanguage.Changed-=RefreshLanguage;
        public void SetBankBalance(int value){bankBalance=Math.Max(0,value);RefreshReadouts();}
        public void SetNotificationCount(int value){notificationCount=Math.Max(0,value);RefreshReadouts();}
        public void SetOpen(bool open,bool immediate=false)
        {
            // Button visibility feedback also observes CanvasGroup slides;
            // do not play a second cue here when those controls appear/hide.
            IsOpen=open;if(immediate)progress=open?1:0;UpdatePose();
        }
        public void OpenProperty(BuildPlot target)
        {SelectProperty(target);SetOpen(true);}
        public void SelectProperty(BuildPlot target)
        {SelectedProperty=target;Select(TabletSection.Properties);RefreshProperty();}
        public void Select(TabletSection section)
        {
            SelectedSection=section;
            for(int i=0;i<pages.Length;i++)if(pages[i])pages[i].SetActive(i==(int)section);
            for(int i=0;i<selectionMarks.Length;i++)if(selectionMarks[i]){selectionMarks[i].color=accent;selectionMarks[i].enabled=i==(int)section;}
            if(section==TabletSection.Properties && !SelectedProperty)
                SelectedProperty=BuildPlot.Active.FirstOrDefault(p=>p && p.gameObject.scene==gameObject.scene);
            RefreshLanguage();
        }
        void LateUpdate()
        {
            progress=Mathf.MoveTowards(progress,IsOpen?1:0,Time.unscaledDeltaTime/Mathf.Max(.05f,slideDuration));
            UpdatePose();RefreshReadouts();
            if(IsOpen && SelectedSection==TabletSection.Properties)RefreshProperty();
        }
        public void UpdatePose()
        {
            if(!body || !dataPanel || !chrome || !(body.parent is RectTransform parent))return;
            float scale=Mathf.Min(1,Mathf.Min((parent.rect.width-40)/body.sizeDelta.x,(parent.rect.height-40)/body.sizeDelta.y));scale=Mathf.Max(.1f,scale);
            body.localScale=Vector3.one*scale;
            // Place precisely the data block inside the top-right safe area.
            var dataCenter=(Vector2)dataPanel.localPosition+new Vector2((.5f-dataPanel.pivot.x)*dataPanel.rect.width,(.5f-dataPanel.pivot.y)*dataPanel.rect.height);
            var target=new Vector2(parent.rect.width*.5f-dockMargin-dataPanel.rect.width*scale*.5f,parent.rect.height*.5f-dockMargin-dataPanel.rect.height*scale*.5f);
            float eased=Mathf.SmoothStep(0,1,progress);
            body.anchoredPosition=Vector2.Lerp(target-dataCenter*scale,Vector2.zero,eased);
            chrome.alpha=eased;chrome.interactable=IsOpen;chrome.blocksRaycasts=IsOpen;
        }
        static string Title(TabletSection section)=>section switch {
            TabletSection.Contacts=>T("Contacts","Contactos"),TabletSection.LegalAffairs=>T("Legal affairs","Asuntos legales"),
            TabletSection.BlackMarket=>T("Black market","Mercado negro"),TabletSection.Properties=>T("Properties","Propiedades"),
            TabletSection.Research=>T("Research","Investigación"),_=>T("Map","Mapa")};
        public void RefreshLanguage()
        {
            if(pageTitle)pageTitle.text=Title(SelectedSection);
            if(closeHint)closeHint.text=T("{0} · Close tablet","{0} · Cerrar tablet",GameInput.GetBindingDisplayString(GameInputAction.Construction,0));
            RefreshReadouts();RefreshProperty();
        }
        void RefreshReadouts()
        {
            if(bankText)bankText.text=T("Bank balance  ${0:N0}","Dinero bancario  ${0:N0}",bankBalance);
            if(notificationText)notificationText.text=T("Notifications  {0}","Notificaciones  {0}",notificationCount);
            if(!clock)clock=FindObjectsByType<DayNightCycle>(FindObjectsSortMode.None).FirstOrDefault(c=>c.gameObject.scene==gameObject.scene);
            float hour=clock?clock.CurrentHour:0;
            if(timeText)timeText.text=T("Time  {0:00}:{1:00}","Hora  {0:00}:{1:00}",Mathf.FloorToInt(hour),Mathf.FloorToInt(hour*60)%60);
        }
        public void RefreshProperty()
        {
            if(!propertyName || !purchaseButton)return;
            if(!economy)economy=FindAnyObjectByType<EconomyService>();
            if(!properties)properties=FindAnyObjectByType<PropertyService>();
            var p=SelectedProperty;
            propertyName.text=p?p.DisplayName:T("Select a property","Selecciona una propiedad");
            propertyPrice.text=p?T("Price: ${0:N0}","Precio: ${0:N0}",p.TotalValue):"";
            bool owned=p&&properties&&properties.IsOwned(p.propertyId);
            bool unique=p&&p.propertyId>=0&&BuildPlot.Active.Count(x=>x&&x.propertyId==p.propertyId)==1;
            bool affordable=p&&economy&&economy.CanAfford(p.TotalValue);
            propertyState.text=!p?"":owned?T("Owned","En propiedad"):!unique?T("Property unavailable","Propiedad no disponible"):!affordable?T("Insufficient funds","Saldo insuficiente"):T("Available for purchase","Disponible para comprar");
            purchaseButton.interactable=p&&unique&&!owned&&affordable&&properties;
            var label=purchaseButton.GetComponentInChildren<TMP_Text>();if(label)label.text=owned?T("Owned","En propiedad"):T("Buy property","Comprar propiedad");
        }
        public void BuySelected()
        {
            RefreshProperty();
            if(!purchaseButton.interactable || !SelectedProperty || SelectedProperty.gameObject.scene!=gameObject.scene)return;
            properties.TryBuy(SelectedProperty.propertyId,SelectedProperty.TotalValue);
            RefreshProperty();if(propertyList)propertyList.Refresh();
        }
    }
}
