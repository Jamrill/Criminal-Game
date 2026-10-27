using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static JuegoCriminal.Localization.GameLanguage;
namespace JuegoCriminal.Construction
{
    // The controller owns this transient tool; no scene materials are modified.
    public sealed class ConstructionEditing
    {
        readonly Dictionary<Renderer,Material[]> highlighted=new();
        readonly Dictionary<Renderer,bool> visibility=new();
        readonly WallChangePreview preview=new();
        public void Clear()
        {
            foreach(var pair in highlighted)if(pair.Key)pair.Key.sharedMaterials=pair.Value;
            highlighted.Clear();
            foreach(var pair in visibility)if(pair.Key)pair.Key.enabled=pair.Value;
            visibility.Clear();preview.Hide();
        }
        public void Dispose(){Clear();preview.Dispose();}
        public void ShowPlan(WallChangePlan plan,BuildCatalog catalog,Material material)
        {
            foreach(var item in plan.removed)foreach(var renderer in item.GetComponentsInChildren<Renderer>())
            {if(!visibility.ContainsKey(renderer))visibility[renderer]=renderer.enabled;renderer.enabled=false;}
            preview.Show(plan,catalog,material);
        }
        public static bool FindWall(Ray ray,BuildPlot plot,BuildCatalog catalog,Transform player,out PlacedBuildObject target,out Vector3 point)
        {
            target=null;point=default;
            var hits=Physics.RaycastAll(ray,100,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            var first=Array.Find(hits,h=>!player || !h.collider.transform.IsChildOf(player));
            float nearest=100;
            // Keep real face/end-cap hits. Virtual planes are only needed to make
            // empty apertures selectable, not to move the cursor through a wall.
            var physical=first.collider?first.collider.GetComponentInParent<PlacedBuildObject>():null;
            if(physical && !physical.deleted && physical.propertyId==plot.propertyId && physical.GetComponentInParent<BuildPlot>()==plot && physical.GetComponent<WallTopology>())
            {target=physical;point=first.point;nearest=first.distance;}
            foreach(var item in plot.GetComponentsInChildren<PlacedBuildObject>())
            {
                var wall=item.GetComponent<WallTopology>();
                if(item.deleted || item.propertyId!=plot.propertyId || !wall)continue;
                foreach(var span in WallGeometry.World(wall))
                {
                    var normal=Vector3.Cross(span.b-span.a,plot.Grid.up).normalized;
                    if(!new Plane(normal,span.a).Raycast(ray,out float distance) || distance>nearest)continue;
                    var hit=ray.GetPoint(distance);
                    float height=Vector3.Dot(hit-span.a,plot.Grid.up);
                    if(height<0 || height>wall.WorldHeight || span.Distance(hit)>.3f)continue;
                    if(first.collider && first.distance<distance-.01f && first.collider.GetComponentInParent<PlacedBuildObject>()!=item)
                    {
                        var blocker=first.collider.GetComponentInParent<PlacedBuildObject>();
                        var blockerEntry=blocker?catalog.Find(blocker.definitionId):null;
                        var hostEntry=catalog.Find(item.definitionId);
                        bool installed=blockerEntry!=null && blockerEntry.mount==BuildMount.Doorway && hostEntry!=null && hostEntry.doorway.size.x>0 &&
                            Vector3.Distance(item.transform.TransformPoint(hostEntry.doorway.center),blocker.transform.position+blocker.transform.rotation*blockerEntry.bounds.center)<.15f;
                        if(!installed)continue;
                    }
                    nearest=distance;target=item;point=hit;
                }
            }
            return target;
        }
        public string Tick(Ray ray, BuildPlot plot, BuildCatalog catalog, BuildEntry opening, Material red, Material green, bool click, Transform player=null, JuegoCriminal.Services.EconomyService economy=null)
        {
            Clear();
            bool replacing=opening!=null;
            string hint=replacing ? T("Aim at the wall section to convert","Apunta al tramo de pared que quieres convertir") : T("Sell: aim at an object you own | X: back","Vender: apunta a un objeto propio | X: volver");
            PlacedBuildObject target=null;Vector3 aim=default;
            if(replacing && !FindWall(ray,plot,catalog,player,out target,out aim))return hint;
            var hits=Physics.RaycastAll(ray,100,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            RaycastHit hit=default;bool found=false;
            foreach(var candidate in hits)
            {
                if(player && candidate.collider.transform.IsChildOf(player))continue;
                hit=candidate;found=true;break;
            }
            if(!replacing)
            {
                if(!found)return hint;
                target=hit.collider.GetComponentInParent<PlacedBuildObject>();aim=hit.point;
            }
            if(!target || target.deleted || target.propertyId!=plot.propertyId || target.gameObject.scene!=plot.gameObject.scene || !plot.Contains(aim))return hint;
            var old=catalog.Find(target.definitionId);
            if(old==null)return hint;
            if(!replacing && target.ProtectedFloor && !FloorEditing.CanRemove(target))return FloorEditing.RemovalWarning;
            int difference=replacing ? catalog.Cost(opening)-catalog.Cost(old) : -catalog.Cost(old);
            bool affordable=economy && (difference<=0 || economy.CanAfford(difference));
            if(replacing)
            {
                var plan=WallReconfiguration.Opening(catalog,plot,target,opening,aim);
                bool valid=WallReconfiguration.Validate(plan,catalog,out var reason);
                if(!valid)
                {
                    foreach(var renderer in target.GetComponentsInChildren<Renderer>())
                    {highlighted[renderer]=renderer.sharedMaterials;renderer.sharedMaterials=System.Linq.Enumerable.Repeat(red,renderer.sharedMaterials.Length).ToArray();}
                    return reason;
                }
                difference=plan.Difference(catalog);affordable=economy && (difference<=0 || economy.CanAfford(difference));
                ShowPlan(plan,catalog,affordable?green:red);
                if(click)
                {
                    Clear();
                    if(!WallReconfiguration.Apply(plan,catalog,economy,out reason))return reason;
                }
                return T("Click: replace section | Difference: ${0}","Clic: sustituir tramo | Diferencia: ${0}",difference)+(affordable ? "" : T(" | Insufficient funds"," | Saldo insuficiente"));
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
                if(!affordable)return T("Insufficient funds or services unavailable","Saldo insuficiente o servicios no disponibles");
                if(!replacing)ConstructionEconomy.Sell(catalog,target,economy);
            }
            return replacing ? T("Click: replace with {0} | Difference: ${1}","Clic: sustituir por {0} | Diferencia: ${1}",ConstructionText.Title(opening),difference)+(affordable ? "" : T(" | Insufficient funds"," | Saldo insuficiente")) : T("Red: click to sell for ${0} | X: back","Rojo: clic para vender por ${0} | X: volver",catalog.Cost(old));
        }
    }
}
