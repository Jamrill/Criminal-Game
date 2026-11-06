using System;
using System.Linq;
using JuegoCriminal.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static JuegoCriminal.Localization.GameLanguage;

namespace JuegoCriminal.Construction
{
    public sealed class PropertyListView : MonoBehaviour
    {
        public Transform content;
        public TMP_Text message;
        public JuegoCriminal.UI.TabletView tablet;
        private PropertyService properties;
        private EconomyService economy;
        private BuildPlot[] plots = Array.Empty<BuildPlot>();
        private Button[] rows = Array.Empty<Button>();
        private TMP_Text[] labels = Array.Empty<TMP_Text>();

        private void OnEnable() => Refresh();
        private void Update() => Refresh();
        public void Refresh()
        {
            if (!content || !message) return;
            if (!properties) properties=FindAnyObjectByType<PropertyService>();
            if (!economy) economy=FindAnyObjectByType<EconomyService>();
            var current=BuildPlot.Active.Where(p=>p && p.gameObject.scene==gameObject.scene)
                .OrderBy(p=>p.propertyId).ThenBy(p=>p.name).ToArray();
            if (!current.SequenceEqual(plots))
            {
                foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                plots=current; rows=new Button[plots.Length]; labels=new TMP_Text[plots.Length];
                for (int i=0;i<plots.Length;i++)
                {
                    var target=plots[i]; rows[i]=tablet?JuegoCriminal.UI.TabletLayout.CreateButton(content,"Property_"+target.propertyId,target.DisplayName,target.DisplayName,Vector2.zero,new Vector2(235,70),tablet.buttonTemplate):BuildMenuView.Button(target.DisplayName,content);
                    labels[i]=rows[i].GetComponentInChildren<TMP_Text>(); labels[i].fontSize=18;
                    rows[i].GetComponent<LayoutElement>().preferredHeight=70;
                    rows[i].onClick.AddListener(()=>{if(tablet)tablet.SelectProperty(target);else Buy(target);});
                }
            }
            message.text=plots.Length==0 ? T("No properties available","No hay propiedades") : tablet?T("Select a property","Selecciona un inmueble"):T("Balance: ${0} | Select a plot to buy it","Saldo: ${0} | Selecciona un solar para comprarlo",economy ? economy.Money : 0);
            for (int i=0;i<plots.Length;i++)
            {
                var p=plots[i]; bool owned=properties && properties.IsOwned(p.propertyId);
                bool valid=p.propertyId>=0 && p.price>=0 && plots.Count(other=>other.propertyId==p.propertyId)==1;
                int value=p.TotalValue;
                bool affordable=economy && economy.CanAfford(value);
                rows[i].interactable=tablet?valid:valid && !owned && affordable && properties;
                string state=!valid ? T("Check ID/price in Inspector","Revisar ID/precio en Inspector") : owned ? T("Owned","En propiedad") : affordable ? T("Buy","Comprar") : T("Insufficient funds","Saldo insuficiente");
                labels[i].text=tablet?p.DisplayName+"\n"+T("${0:N0} · {1}","${0:N0} · {1}",value,owned?T("Owned","En propiedad"):T("For sale","En venta")):
                    p.DisplayName+T(" | Value: ${0}\n"," | Valor: ${0}\n",value)+state+(p.buildable ? "" : T(" (not buildable)"," (no construible)"));
            }
        }
        private void Buy(BuildPlot target)
        {
            if (!target || !target.isActiveAndEnabled || !properties || target.gameObject.scene!=gameObject.scene) return;
            if (BuildPlot.Active.Count(p=>p && p.gameObject.scene==gameObject.scene && p.propertyId==target.propertyId)!=1) return;
            properties.TryBuy(target.propertyId,target.TotalValue);
            Refresh();
        }
    }
}
