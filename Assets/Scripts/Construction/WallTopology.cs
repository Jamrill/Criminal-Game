using System;
using System.Collections.Generic;
using UnityEngine;
namespace JuegoCriminal.Construction
{
    // Swap only the wall skin: preserve colliders, doors, object identity and saved finish.
    public sealed class WallTopology : MonoBehaviour
    {
        public MeshFilter wall;
        public Mesh[] variants = new Mesh[3];
        [Serializable] public class Materials { public Material[] values; }
        public Materials[] variantMaterials = new Materials[3];
        public Vector3[] connections;
        public Vector3 bottomCenter;
        public float height=7;
        public int currentVariant;
        public bool automaticVariants=true;
        private static readonly HashSet<WallTopology> active=new HashSet<WallTopology>();
        private static bool dirty;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetRegistry() { active.Clear(); dirty=true; }
        private void OnEnable() { active.Add(this); dirty=true; }
        private void OnDisable() { active.Remove(this); dirty=true; }
        private void LateUpdate() { if(dirty) RefreshAll(); }
        public Vector3 Bottom => transform.TransformPoint(bottomCenter);
        public float WorldHeight => height*Mathf.Abs(transform.lossyScale.y);
        public static void MarkDirty() => dirty=true;
        public bool JoinsAt(Vector3 position, Quaternion rotation, Vector3 scale, WallTopology other)
        {
            if(!other || connections==null || other.connections==null) return false;
            int matches=0;
            foreach(var local in connections)
                foreach(var end in other.connections)
                    if(Vector3.Distance(position+rotation*Vector3.Scale(local,scale),other.transform.TransformPoint(end))<.06f) { matches++; break; }
            return matches==1;
        }
        public static int ChooseVariant(bool closedSides,bool coveredAbove) => closedSides ? coveredAbove ? 2 : 1 : 0;
        public static void RefreshAll()
        {
            dirty=false;
            foreach(var item in active) if(item) item.RefreshVariant();
        }
        public static void RefreshScene(UnityEngine.SceneManagement.Scene scene)
        {
            foreach(var root in scene.GetRootGameObjects())
                foreach(var wall in root.GetComponentsInChildren<WallTopology>()) if(wall.isActiveAndEnabled) active.Add(wall);
            RefreshAll();
        }
        private void RefreshVariant()
        {
            if(!automaticVariants) return;
            if(!wall || connections==null || connections.Length<2) return;
            bool closed=true, above=false;
            foreach(var local in connections)
            {
                var point=transform.TransformPoint(local); bool joined=false;
                foreach(var other in active)
                {
                    if(!other || other==this || other.gameObject.scene!=gameObject.scene || other.connections==null) continue;
                    foreach(var end in other.connections)
                        if(Vector3.Distance(point,other.transform.TransformPoint(end))<.06f) { joined=true; break; }
                    if(joined) break;
                }
                if(!joined) { closed=false; break; }
            }
            foreach(var other in active)
                if(other && other!=this && other.gameObject.scene==gameObject.scene && other.connections!=null &&
                    Vector3.Distance(Bottom+transform.up*WorldHeight,other.Bottom)<.06f)
                {
                    bool covered=true;
                    foreach(var end in connections)
                    {
                        bool match=false;
                        foreach(var otherEnd in other.connections)
                            if(Vector3.Distance(transform.TransformPoint(end)+transform.up*WorldHeight,other.transform.TransformPoint(otherEnd))<.06f) { match=true; break; }
                        if(!match) { covered=false; break; }
                    }
                    if(covered) { above=true; break; }
                }
            SetVariant(ChooseVariant(closed,above));
        }
        public void SetVariant(int index)
        {
            if(!wall || variants==null || index>=variants.Length || !variants[index]) return;
            if(wall.sharedMesh==variants[index]) { currentVariant=index; return; }
            var renderer=wall.GetComponent<MeshRenderer>();
            // Preserve a painted finish; glass/frame slots keep their own materials.
            Material finish=null;
            var surface=GetComponent<BuildSurface>();
            if(surface) foreach(var slot in surface.slots)
                if(slot.renderer==renderer && slot.index<renderer.sharedMaterials.Length) { finish=renderer.sharedMaterials[slot.index]; break; }
            wall.sharedMesh=variants[index]; currentVariant=index;
            if(renderer && variantMaterials.Length>index)
            {
                var materials=(Material[])variantMaterials[index].values.Clone();
                if(finish && surface) foreach(var slot in surface.slots)
                    if(slot.renderer==renderer && slot.index<materials.Length) materials[slot.index]=finish;
                renderer.sharedMaterials=materials;
            }
        }
    }
}
