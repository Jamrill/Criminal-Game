using System;
using System.Linq;
using JuegoCriminal.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JuegoCriminal.Construction
{
    public sealed class PropertyListView : MonoBehaviour
    {
        public Transform content;
        public TMP_Text message;
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
                    var target=plots[i]; rows[i]=BuildMenuView.Button(target.DisplayName,content);
                    labels[i]=rows[i].GetComponentInChildren<TMP_Text>(); labels[i].fontSize=18;
                    rows[i].GetComponent<LayoutElement>().preferredHeight=70;
                    rows[i].onClick.AddListener(()=>Buy(target));
                }
            }
            message.text=plots.Length==0 ? "No hay solares disponibles en esta escena." : "Saldo: "+(economy ? economy.Money : 0)+" | Selecciona un solar para comprarlo";
            for (int i=0;i<plots.Length;i++)
            {
                var p=plots[i]; bool owned=properties && properties.IsOwned(p.propertyId);
                bool valid=p.propertyId>=0 && p.price>=0 && plots.Count(other=>other.propertyId==p.propertyId)==1;
                bool affordable=economy && economy.CanAfford(p.price);
                rows[i].interactable=valid && !owned && affordable && properties;
                string state=!valid ? "Revisar ID/precio en Inspector" : owned ? "En propiedad" : affordable ? "Comprar" : "Saldo insuficiente";
                labels[i].text=p.DisplayName+" | Precio: "+p.price+"\n"+state+(p.buildable ? "" : " (no construible)");
            }
        }
        private void Buy(BuildPlot target)
        {
            if (!target || !target.isActiveAndEnabled || !properties || target.gameObject.scene!=gameObject.scene) return;
            if (BuildPlot.Active.Count(p=>p && p.gameObject.scene==gameObject.scene && p.propertyId==target.propertyId)!=1) return;
            properties.TryBuy(target.propertyId,target.price);
            Refresh();
        }
    }
}
