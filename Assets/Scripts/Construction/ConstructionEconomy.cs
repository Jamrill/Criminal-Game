using JuegoCriminal.Services;
using UnityEngine;

namespace JuegoCriminal.Construction
{
    // All world mutations go through the same pricing rules. Persistence captures
    // the resulting world and balance together when the player saves the game.
    public static class ConstructionEconomy
    {
        static bool Owns(BuildPlot plot,EconomyService money)
        {
            var properties=money ? money.GetComponent<PropertyService>() : null;
            return plot && properties && plot.CanBuild(properties);
        }
        public static bool CanAfford(BuildCatalog catalog, BuildEntry entry, EconomyService money)
            => money && money.CanAfford(catalog.Cost(entry));

        public static PlacedBuildObject Build(BuildCatalog catalog, BuildRecord record, BuildPlot plot, EconomyService money)
        {
            var entry=catalog.Find(record.definitionId);
            if(entry==null || !Owns(plot,money) || record.propertyId!=plot.propertyId)return null;
            int cost=catalog.Cost(entry);
            if(!money.TrySpend(cost))return null;
            var placed=ConstructionPersistence.Spawn(catalog,record,true,plot.gameObject.scene);
            if(!placed)money.AddMoney(cost);
            return placed;
        }

        public static bool Sell(BuildCatalog catalog, PlacedBuildObject item, EconomyService money)
        {
            if(!item || item.deleted || !Owns(item.GetComponentInParent<BuildPlot>(),money))return false;
            var entry=catalog.Find(item.definitionId);
            if(entry==null)return false;
            // Mark deleted first: a second click can never pay twice.
            item.Demolish();
            money.AddMoney(catalog.Cost(entry));
            return true;
        }

        public static bool Replace(BuildCatalog catalog, PlacedBuildObject item, BuildEntry next, EconomyService money)
        {
            if(!item || item.deleted || next==null || !Owns(item.GetComponentInParent<BuildPlot>(),money))return false;
            var old=catalog.Find(item.definitionId);
            if(old==null)return false;
            int difference=catalog.Cost(next)-catalog.Cost(old);
            if(difference>0 && !money.TrySpend(difference))return false;
            var record=item.Capture();record.definitionId=next.id;
            var replacement=ConstructionPersistence.Spawn(catalog,record,true,item.gameObject.scene);
            if(!replacement)
            {
                if(difference>0)money.AddMoney(difference);
                return false;
            }
            item.deleted=true; // Exclude the old instance immediately, before deferred Destroy.
            item.persistentId=string.Empty;
            item.gameObject.SetActive(false);
            if(Application.isPlaying)Object.Destroy(item.gameObject);else Object.DestroyImmediate(item.gameObject);
            if(difference<0)money.AddMoney(-difference);
            WallTopology.MarkDirty();
            return true;
        }
    }
}
