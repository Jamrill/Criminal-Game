using System;
using System.Collections.Generic;
using System.Linq;
using JuegoCriminal.Services;
using UnityEngine;
using Object=UnityEngine.Object;
using static JuegoCriminal.Localization.GameLanguage;

namespace JuegoCriminal.Construction
{
    public sealed class WallChangePlan
    {
        public BuildPlot plot;
        public readonly List<PlacedBuildObject> removed=new();
        public readonly List<BuildRecord> added=new();
        public string error;
        public int Difference(BuildCatalog catalog)=>added.Sum(r=>catalog.Cost(catalog.Find(r.definitionId)))-removed.Sum(p=>catalog.Cost(catalog.Find(p.definitionId)));
    }
    public static class WallReconfiguration
    {
        static List<WallGeometry.Span> Distinct(IEnumerable<WallGeometry.Span> spans)
        {
            var result=new List<WallGeometry.Span>();
            foreach(var span in spans)if(!result.Any(s=>s.Same(span)))result.Add(span);
            return result;
        }
        static BuildRecord Record(BuildEntry entry,BuildPlot plot,Vector3 pos,Quaternion rot,int finish)=>new BuildRecord{
            definitionId=entry.id,propertyId=plot.propertyId,sceneName=plot.gameObject.scene.name,
            position=pos,rotation=rot,scale=entry.prefab.transform.localScale,finish=finish,
            cellCenter=pos+rot*Vector3.Scale(entry.prefab.GetComponent<WallTopology>().bottomCenter,entry.prefab.transform.localScale)};
        static List<WallGeometry.Span> Spans(BuildCatalog catalog,BuildRecord record)
            =>WallGeometry.At(catalog.Find(record.definitionId).prefab.GetComponent<WallTopology>(),record.position,record.rotation,record.scale);

        public static Vector3 SnapReplacement(BuildCatalog catalog,BuildEntry selected,PlacedBuildObject target,Vector3 position,Quaternion rotation,Vector3 hit)
        {
            if(!target || target.deleted || !WallGeometry.Solid(selected))return position;
            var topology=target.GetComponent<WallTopology>();
            var source=selected.prefab.GetComponent<WallTopology>();
            if(!topology || WallGeometry.Local(source).Count<2 || Mathf.Abs(position.y-topology.Bottom.y)>.1f)return position;
            var targetSpans=WallGeometry.World(topology);
            if(targetSpans.Count==0)return position;
            var hitSpan=targetSpans.OrderBy(s=>s.Distance(hit)).First();
            float best=float.MaxValue;Vector3 result=position;
            foreach(var span in WallGeometry.At(source,Vector3.zero,rotation,selected.prefab.transform.localScale))
                foreach(var start in new[]{hitSpan.a,hitSpan.b})
                {
                    var candidate=start-span.a;
                    if(!new WallGeometry.Span(candidate+span.a,candidate+span.b).Same(hitSpan))continue;
                    float distance=(candidate-position).sqrMagnitude;
                    if(distance<best){best=distance;result=candidate;}
                }
            float range=WallGeometry.Local(source).Max(s=>s.Length)+.5f;
            return best<=range*range?result:position;
        }

        // Null means ordinary empty-space placement. A non-null invalid plan keeps
        // overlapping duplicates red instead of falling through to a paid build.
        public static WallChangePlan Placement(BuildCatalog catalog,BuildPlot plot,BuildEntry selected,Vector3 pos,Quaternion rot)
        {
            if(selected==null || !selected.prefab || !selected.prefab.GetComponent<WallTopology>())return null;
            var candidate=Record(selected,plot,pos,rot,-1);
            var requested=Spans(catalog,candidate);
            var touched=plot.GetComponentsInChildren<PlacedBuildObject>().Where(p=>!p.deleted && p.propertyId==plot.propertyId && p.GetComponent<WallTopology>() &&
                WallGeometry.World(p.GetComponent<WallTopology>()).Any(s=>requested.Any(t=>t.Same(s)))).ToArray();
            if(touched.Length==0)return JoinAtEndpoints(catalog,plot,selected,requested,rot);
            var plan=new WallChangePlan{plot=plot};
            if(requested.Count==1)
                return Opening(catalog,plot,touched[0],selected,(requested[0].a+requested[0].b)*.5f+Vector3.up*2);
            var solid=new List<WallGeometry.Span>(requested);
            foreach(var item in touched)
            {
                var previous=WallGeometry.World(item.GetComponent<WallTopology>());
                if(!WallGeometry.Solid(catalog.Find(item.definitionId)) && previous.Any(s=>!requested.Any(t=>t.Same(s))))
                {plan.error=T("No compatible pieces to preserve the rest of this special wall","No hay piezas compatibles para conservar el resto de esta pared especial");return plan;}
                plan.removed.Add(item);solid.AddRange(previous);
            }
            solid=Distinct(solid);
            // -1 is the prefab's default finish; index 0 is the same base wall
            // material used by older runtime-built walls.
            var finishes=plan.removed.Select(p=>p.finish<0?0:p.finish).Distinct().ToArray();
            if(finishes.Length>1){plan.error=T("Sections have different finishes","Los tramos tienen acabados diferentes");return plan;}
            int finish=finishes.Length==1?finishes[0]:touched[0].finish;
            candidate.finish=finish;
            // The selected shape intentionally replaces the types underneath it.
            if(requested.All(s=>solid.Any(t=>t.Same(s))))
            {plan.added.Add(candidate);solid.RemoveAll(s=>requested.Any(t=>t.Same(s)));}
            if(!Pack(catalog,plot,solid,rot,finish,plan.added)){plan.error=T("No compatible prefabs to preserve all sections","No hay prefabs compatibles para conservar todos los tramos");return plan;}
            if(plan.removed.Count==1 && plan.added.Count==1 &&
                WallGeometry.Family(catalog.Find(plan.removed[0].definitionId))==WallGeometry.Family(catalog.Find(plan.added[0].definitionId)) &&
                SameCoverage(WallGeometry.World(plan.removed[0].GetComponent<WallTopology>()),Spans(catalog,plan.added[0])))
                plan.error=T("This wall already exists; no changes","Ya existe esa pared; no hay cambios");
            if(plan.removed.Count==0 && plan.added.Count==0)plan.error=T("These sections are already built","Ya estan construidos esos tramos");
            return plan;
        }
        static bool SameCoverage(List<WallGeometry.Span> a,List<WallGeometry.Span> b)=>a.Count==b.Count && a.All(s=>b.Any(t=>s.Same(t)));
        static WallChangePlan JoinAtEndpoints(BuildCatalog catalog,BuildPlot plot,BuildEntry selected,List<WallGeometry.Span> requested,Quaternion basis)
        {
            if(!WallGeometry.Solid(selected))return null;
            // Only directly touching solid pieces on this storey. Never consume
            // the whole connected building or remove existing windows/glass/doors.
            var neighbours=plot.GetComponentsInChildren<PlacedBuildObject>().Where(item=>!item.deleted && item.propertyId==plot.propertyId &&
                item.GetComponentInParent<BuildPlot>()==plot && WallGeometry.Solid(catalog.Find(item.definitionId)) &&
                WallGeometry.World(item.GetComponent<WallTopology>()).Any(a=>requested.Any(b=>
                    WallGeometry.Near(a.a,b.a)||WallGeometry.Near(a.a,b.b)||WallGeometry.Near(a.b,b.a)||WallGeometry.Near(a.b,b.b)))).ToArray();
            if(neighbours.Length==0)return null;
            var finishes=neighbours.Select(item=>item.finish<0?0:item.finish).Distinct().ToArray();
            if(finishes.Length!=1)return null; // A single prefab cannot preserve different per-arm paint.
            var spans=Distinct(requested.Concat(neighbours.SelectMany(item=>WallGeometry.World(item.GetComponent<WallTopology>()))));
            var plan=new WallChangePlan{plot=plot};
            if(!Pack(catalog,plot,spans,basis,finishes[0],plan.added) || plan.added.Count>=neighbours.Length+1)return null;
            plan.removed.AddRange(neighbours);
            // Preserve identities of any neighbouring piece that exact-cover left
            // unchanged. Only pieces actually consolidated become tombstones.
            foreach(var item in neighbours)
            {
                var original=WallGeometry.World(item.GetComponent<WallTopology>());
                int same=plan.added.FindIndex(record=>WallGeometry.Family(catalog.Find(record.definitionId))==WallGeometry.Family(catalog.Find(item.definitionId)) && SameCoverage(original,Spans(catalog,record)));
                if(same<0)continue;
                plan.added.RemoveAt(same);plan.removed.Remove(item);
            }
            return plan.removed.Count>0?plan:null;
        }

        public static WallChangePlan Opening(BuildCatalog catalog,BuildPlot plot,PlacedBuildObject target,BuildEntry opening,Vector3 hit)
        {
            var plan=new WallChangePlan{plot=plot};
            if(!target || target.deleted || target.propertyId!=plot.propertyId || target.GetComponentInParent<BuildPlot>()!=plot)
            {plan.error=T("Aim at a wall you own","Apunta a una pared propia");return plan;}
            var old=catalog.Find(target.definitionId);var wall=target.GetComponent<WallTopology>();
            if(!wall || old==null || opening==null || !opening.prefab || !opening.prefab.GetComponent<WallTopology>())
            {plan.error=T("This wall type does not support this replacement","Este tipo de pared no admite esta sustitucion");return plan;}
            var spans=WallGeometry.World(wall).OrderBy(s=>s.Distance(hit)).ToList();
            if(spans.Count==0){plan.error=T("Wall has no configured sections","Pared sin tramos configurados");return plan;}
            if(spans.Count>1 && Mathf.Abs(spans[0].Distance(hit)-spans[1].Distance(hit))<.01f)
            {plan.error=T("Aim at the arm to change, away from the joint","Apunta al brazo que quieres cambiar, lejos de la union");return plan;}
            var chosen=spans[0];
            var template=WallGeometry.Local(opening.prefab.GetComponent<WallTopology>());
            if(template.Count!=1 || Mathf.Abs(template[0].Length-chosen.Length)>.06f)
            {plan.error=T("A prefab matching this section's length is required","Se necesita un prefab con la longitud de ese tramo");return plan;}
            // Keep the old orientation when replacing an existing straight opening.
            var delta=chosen.b-chosen.a;
            var local=template[0].b-template[0].a;
            float angle=Vector3.SignedAngle(local,delta,Vector3.up);
            var rotation=Quaternion.AngleAxis(angle,Vector3.up);
            var record=Record(opening,plot,chosen.a-rotation*Vector3.Scale(template[0].a,opening.prefab.transform.localScale),rotation,target.finish);
            if(WallGeometry.Family(old)==WallGeometry.Family(opening))
            {plan.error=T("This section is already the same type","Ese tramo ya es del mismo tipo");return plan;}
            plan.removed.Add(target);plan.added.Add(record);
            spans.RemoveAt(0);
            if(spans.Count>0 && !WallGeometry.Solid(old))
            {plan.error=T("No compatible pieces to preserve the rest of this special wall","No hay piezas compatibles para conservar el resto de esta pared especial");return plan;}
            if(!Pack(catalog,plot,spans,target.transform.rotation,target.finish,plan.added))plan.error=T("No prefabs to preserve the rest of the wall","No hay prefabs para conservar el resto de la pared");
            return plan;
        }

        static bool Pack(BuildCatalog catalog,BuildPlot plot,List<WallGeometry.Span> spans,Quaternion basis,int finish,List<BuildRecord> result)
        {
            if(spans.Count==0)return true;
            // Exact cover: a template may only consume whole existing segments.
            // This prevents merging across gaps, crossing storeys or losing arms.
            var templates=catalog.entries.Where(e=>!e.hiddenInMenu && WallGeometry.Solid(e))
                .OrderByDescending(e=>WallGeometry.Local(e.prefab.GetComponent<WallTopology>()).Count).ToArray();
            foreach(var entry in templates)
                for(int turn=0;turn<4;turn++)
                {
                    var rotation=basis*Quaternion.Euler(0,turn*90,0);
                    var local=WallGeometry.At(entry.prefab.GetComponent<WallTopology>(),Vector3.zero,rotation,entry.prefab.transform.localScale);
                    if(local.Count>spans.Count)continue;
                    foreach(var anchor in local)
                        foreach(var end in new[]{spans[0].a,spans[0].b})
                        {
                            var pos=end-anchor.a;
                            var covered=local.Select(s=>new WallGeometry.Span(pos+s.a,pos+s.b)).ToList();
                            if(!covered.All(s=>spans.Any(t=>s.Same(t))))continue;
                            int count=result.Count;result.Add(Record(entry,plot,pos,rotation,finish));
                            if(Pack(catalog,plot,spans.Where(s=>!covered.Any(t=>s.Same(t))).ToList(),basis,finish,result))return true;
                            result.RemoveRange(count,result.Count-count);
                        }
                }
            return false;
        }

        public static bool Validate(WallChangePlan plan,BuildCatalog catalog,out string reason)
        {
            reason=plan?.error;
            if(plan==null || !plan.plot || !string.IsNullOrEmpty(reason))return false;
            if(plan.removed.Any(p=>!p || p.deleted || p.GetComponentInParent<BuildPlot>()!=plan.plot))
            {reason=T("The wall has changed; aim again","La pared ha cambiado; vuelve a apuntar");return false;}
            foreach(var host in plan.removed)
            {
                var old=catalog.Find(host.definitionId);
                if(old==null || old.doorway.size.x<=0)continue;
                var center=host.transform.TransformPoint(old.doorway.center);
                foreach(var item in plan.plot.GetComponentsInChildren<PlacedBuildObject>())
                {
                    var entry=catalog.Find(item.definitionId);
                    if(item.deleted || entry==null || entry.mount!=BuildMount.Doorway)continue;
                    var closedCenter=item.transform.position+item.transform.rotation*entry.bounds.center;
                    if(Vector3.Distance(center,closedCenter)<.15f)
                    {reason=T("Remove or sell the installed door first","Retira o vende primero la puerta instalada en ese hueco");return false;}
                }
            }
            var colliders=plan.removed.SelectMany(p=>p.GetComponentsInChildren<Collider>()).Where(c=>c.enabled).ToArray();
            try
            {
                foreach(var c in colliders)c.enabled=false;
                Physics.SyncTransforms();
                foreach(var record in plan.added)
                    if(!BuildPlacement.IsValid(catalog.Find(record.definitionId),plan.plot,record.position,record.rotation,record.cellCenter))
                    {reason=T("Cannot replace: occupied or outside the plot","No se puede sustituir: espacio ocupado o fuera del solar");return false;}
                return true;
            }
            finally {foreach(var c in colliders)if(c)c.enabled=true;Physics.SyncTransforms();}
        }
        public static bool Apply(WallChangePlan plan,BuildCatalog catalog,EconomyService money,out string reason)
        {
            if(!Validate(plan,catalog,out reason))return false;
            var properties=money?money.GetComponent<PropertyService>():null;
            if(!properties || !plan.plot.CanBuild(properties)){reason=T("You need a buildable plot you own","Necesitas un solar propio y construible");return false;}
            int delta=plan.Difference(catalog);
            if(delta>0 && !money.TrySpend(delta)){reason=T("Insufficient funds","Saldo insuficiente");return false;}
            var spawned=new List<PlacedBuildObject>();
            try
            {
                foreach(var record in plan.added)
                {
                    record.id=Guid.NewGuid().ToString("N");
                    var item=ConstructionPersistence.Spawn(catalog,record,true,plan.plot.gameObject.scene);
                    if(!item)throw new InvalidOperationException("No se pudo crear una pared");
                    spawned.Add(item);
                }
            }
            catch(Exception exception)
            {
                foreach(var item in spawned){item.persistentId=string.Empty;item.gameObject.SetActive(false);Destroy(item.gameObject);}
                if(delta>0)money.AddMoney(delta);
                reason=T("Could not complete the replacement","No se pudo completar la sustitucion");Debug.LogException(exception);return false;
            }
            // Keep tombstones even for authored scene objects: loading the original
            // scene must not resurrect the T/+ after it has been split into pieces.
            foreach(var item in plan.removed)item.Demolish();
            if(delta<0)money.AddMoney(-delta);
            WallTopology.RefreshScene(plan.plot.gameObject.scene);
            return true;
        }
        static void Destroy(GameObject go){if(Application.isPlaying)Object.Destroy(go);else Object.DestroyImmediate(go);}
    }
}
