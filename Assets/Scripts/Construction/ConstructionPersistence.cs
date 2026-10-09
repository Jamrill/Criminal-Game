using System.Collections;
using System.Linq;
using JuegoCriminal.Core;
using UnityEngine;
using System.Collections.Generic;
using JuegoCriminal.Services;
using UnityEngine.SceneManagement;
namespace JuegoCriminal.Construction
{
    public sealed class ConstructionPersistence : MonoBehaviour
    {
        public BuildCatalog catalog;
        private static readonly Dictionary<int, SaveData> Restored = new Dictionary<int, SaveData>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            Restored.Clear();
            SceneManager.sceneUnloaded -= ForgetScene;
            SceneManager.sceneUnloaded += ForgetScene;
        }
        private static void ForgetScene(Scene scene) => Restored.Remove(scene.handle);
        private IEnumerator Start()
        {
            while(!Bootstrapper.Instance || !Bootstrapper.Instance.SaveService || !Bootstrapper.Instance.SaveService.HasCurrentGame) yield return null;
            RestoreScene(Bootstrapper.Instance.SaveService.Current,gameObject.scene);
        }
        public void RestoreScene(SaveData data, Scene scene)
        {
            if (!catalog || data==null || !scene.IsValid() || !scene.isLoaded) return;
            if (Restored.TryGetValue(scene.handle,out var previous) && ReferenceEquals(previous,data)) return;
            foreach(var record in (data.buildings ?? System.Array.Empty<BuildRecord>()).Where(r=>r!=null && r.sceneName==scene.name))
            {
                var existing=FindObjectsByType<PlacedBuildObject>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(p=>p.gameObject.scene==scene && p.persistentId==record.id);
                if(existing) existing.Restore(record,catalog);
                else
                {
                    var spawned=Spawn(catalog,record,true);
                    if(spawned) SceneManager.MoveGameObjectToScene(spawned.gameObject,scene);
                }
            }
            Restored[scene.handle]=data;
        }
        public static PlacedBuildObject Spawn(BuildCatalog catalog,BuildRecord record,bool runtime)
        {
            var entry=catalog.Find(record.definitionId);
            if(entry==null || !entry.prefab) { Debug.LogWarning("Unknown building: "+record.definitionId); return null; }
            var go=Instantiate(entry.prefab,record.position,record.rotation);
            var marker=go.GetComponent<PlacedBuildObject>(); if(!marker) marker=go.AddComponent<PlacedBuildObject>();
            marker.runtimePlaced=runtime; marker.Restore(record,catalog); return marker;
        }
    }
}
