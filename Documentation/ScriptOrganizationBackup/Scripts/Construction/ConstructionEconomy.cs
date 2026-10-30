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

        // A wall drag is one transaction. Previews never mutate the world or balance.
        public static bool BuildWalls(BuildCatalog catalog,System.Collections.Generic.IReadOnlyList<BuildRecord> records,BuildPlot plot,EconomyService money)
        {
            if(records.Count==0 || !Owns(plot,money))return false;
            long total=0;
            foreach(var record in records)
            {
                var entry=catalog.Find(record.definitionId);
                if(entry==null || !entry.prefab.GetComponent<WallTopology>() || record.propertyId!=plot.propertyId ||
                    !BuildPlacement.IsValid(entry,plot,record.position,record.rotation,record.cellCenter))return false;
                total+=catalog.Cost(entry);
            }
            if(total>int.MaxValue || !money.TrySpend((int)total))return false;
            var created=new System.Collections.Generic.List<PlacedBuildObject>();
            bool complete=false;
            try
            {
                foreach(var record in records)
                {
                    Physics.SyncTransforms();
                    var entry=catalog.Find(record.definitionId);
                    if(!BuildPlacement.IsValid(entry,plot,record.position,record.rotation,record.cellCenter))return false;
                    var item=ConstructionPersistence.Spawn(catalog,record,true,plot.gameObject.scene);
                    if(!item)return false;
                    created.Add(item);
                }
                complete=true;return true;
            }
            finally
            {
                if(!complete)
                {
                    foreach(var item in created)
                    {
                        item.deleted=true;item.gameObject.SetActive(false);
                        if(Application.isPlaying)Object.Destroy(item.gameObject);else Object.DestroyImmediate(item.gameObject);
                    }
                    money.AddMoney((int)total);WallTopology.MarkDirty();
                }
            }
        }

        public static PlacedBuildObject Build(BuildCatalog catalog, BuildRecord record, BuildPlot plot, EconomyService money)
        {
            var entry=catalog.Find(record.definitionId);
            if(entry==null || !Owns(plot,money) || record.propertyId!=plot.propertyId)return null;
            if(!BuildPlacement.FitsPlot(entry,plot,record.position,record.rotation))return null;
            if(StairConstruction.IsStairs(entry))return StairConstruction.Build(catalog,plot,record,money);
            int cost=catalog.Cost(entry);
            if(!money.TrySpend(cost))return null;
            var placed=ConstructionPersistence.Spawn(catalog,record,true,plot.gameObject.scene);
            if(!placed)money.AddMoney(cost);
            return placed;
        }

        public static bool Sell(BuildCatalog catalog, PlacedBuildObject item, EconomyService money)
        {
            if(!item || item.deleted || (item.ProtectedFloor && !FloorEditing.CanRemove(item)) || !Owns(item.GetComponentInParent<BuildPlot>(),money))return false;
            var entry=catalog.Find(item.definitionId);
            if(entry==null)return false;
            // Mark deleted first: a second click can never pay twice.
            item.Demolish();
            money.AddMoney(catalog.Cost(entry));
            return true;
        }

        public static bool Replace(BuildCatalog catalog, PlacedBuildObject item, BuildEntry next, EconomyService money)
        {
            if(!item || item.deleted || item.ProtectedFloor || next==null || !Owns(item.GetComponentInParent<BuildPlot>(),money))return false;
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
