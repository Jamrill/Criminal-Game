using System;
using System.Collections.Generic;
using UnityEngine;
namespace JuegoCriminal.Construction
{
    // The controller owns this transient tool; no scene materials are modified.
    public sealed class ConstructionEditing
    {
        readonly Dictionary<Renderer,Material[]> highlighted=new();
        public void Clear()
        {
            foreach(var pair in highlighted)if(pair.Key)pair.Key.sharedMaterials=pair.Value;
            highlighted.Clear();
        }
        public string Tick(Ray ray, BuildPlot plot, BuildCatalog catalog, BuildEntry opening, Material red, Material green, bool click, Transform player=null, JuegoCriminal.Services.EconomyService economy=null)
        {
            Clear();
            bool replacing=opening!=null;
            string hint=replacing ? "Apunta a una pared recta propia para convertirla" : "Vender: apunta a un objeto propio | X: volver";
            var hits=Physics.RaycastAll(ray,100,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            RaycastHit hit=default;bool found=false;
            foreach(var candidate in hits)
            {
                if(player && candidate.collider.transform.IsChildOf(player))continue;
                hit=candidate;found=true;break;
            }
            if(!found)return hint;
            var target=hit.collider.GetComponentInParent<PlacedBuildObject>();
            if(!target || target.deleted || target.propertyId!=plot.propertyId || target.gameObject.scene!=plot.gameObject.scene || !plot.Contains(hit.point))return hint;
            var old=catalog.Find(target.definitionId);
            if(old==null)return hint;
            int difference=replacing ? catalog.Cost(opening)-catalog.Cost(old) : -catalog.Cost(old);
            bool affordable=economy && (difference<=0 || economy.CanAfford(difference));
            if(replacing)
            {
                var wall=target.GetComponent<WallTopology>();var next=opening.prefab.GetComponent<WallTopology>();
                string family=old.prefab.name.ToLowerInvariant();
                bool straight=family=="wall" || family=="wall_complete" || family=="wall_semincomplete" || family=="wall_incomplete" || old.IsOpening;
                if(!straight || !wall || !next || wall.connections.Length!=2 || next.connections.Length!=2)return "Solo se puede convertir una pared recta compatible";
                for(int i=0;i<2;i++)if(Vector3.Distance(wall.connections[i],next.connections[i])>.01f)return "El hueco no tiene las mismas dimensiones";
                // Validate all remaining collisions with the old wall temporarily excluded.
                var colliders=target.GetComponentsInChildren<Collider>();var enabled=new List<Collider>();
                bool valid;
                try
                {
                    foreach(var c in colliders)if(c.enabled){enabled.Add(c);c.enabled=false;}
                    Physics.SyncTransforms();
                    valid=BuildPlacement.IsValid(opening,plot,target.transform.position,target.transform.rotation,target.cellCenter);
                }
                finally { foreach(var c in enabled)if(c)c.enabled=true;Physics.SyncTransforms(); }
                if(!valid)return "No se puede convertir: espacio ocupado o fuera del solar";
            }
            foreach(var renderer in target.GetComponentsInChildren<Renderer>())
            {
                var mats=renderer.sharedMaterials;highlighted[renderer]=mats;
                var tint=new Material[mats.Length];for(int i=0;i<tint.Length;i++)tint[i]=replacing && affordable?green:red;
                renderer.sharedMaterials=tint;
            }
            if(click)
            {
                Clear();
                if(!affordable)return "Saldo insuficiente o servicios no disponibles";
                if(!replacing)ConstructionEconomy.Sell(catalog,target,economy);
                else ConstructionEconomy.Replace(catalog,target,opening,economy);
            }
            return replacing ? "Clic: sustituir por "+opening.title+" | Diferencia: $"+difference+(affordable ? "" : " | Saldo insuficiente") : "Rojo: clic para vender por $"+catalog.Cost(old)+" | X: volver";
        }
    }
}
