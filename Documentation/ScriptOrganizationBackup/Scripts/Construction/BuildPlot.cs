using System.Collections.Generic;
using UnityEngine;
using JuegoCriminal.Services;
namespace JuegoCriminal.Construction
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BuildPlot : MonoBehaviour
    {
        public int propertyId = -1;
        public string displayName;
        [Tooltip("Valor base del terreno. El precio final suma los elementos actuales del catalogo.")]
        [Min(0)] public int price = 1;
        public BuildCatalog catalog;
        // Derive value from the saved objects, not a second mutable running total.
        public int TotalValue
        {
            get
            {
                if(catalog && catalog.FreeConstruction)return 0;
                long total=Mathf.Max(0,price);
                if(catalog) foreach(var item in GetComponentsInChildren<PlacedBuildObject>(true))
                    if(!item.deleted && item.propertyId==propertyId && item.GetComponentInParent<BuildPlot>()==this)
                        total+=catalog.Cost(catalog.Find(item.definitionId));
                return (int)System.Math.Min(int.MaxValue,total);
            }
        }
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        public bool buildable;
        [Tooltip("Origen y altura del suelo de la rejilla; usar escala 1 y rotacion solo Y.")]
        public Transform gridOrigin;
        [Tooltip("Height of the protected foundation floor, relative to Grid Origin. Upper storeys remain removable.")]
        public float foundationHeight;
        [Header("Construction boundary")]
        public bool showBoundary = true;
        public Color boundaryColor = new Color(.85f,.88f,.9f,1);
        [Min(.01f)] public float boundaryWidth = .08f;
        [Min(0)] public float boundaryHeight = .06f;
        private LineRenderer boundary;
        private Material boundaryMaterial;
        public const float CellSize = 4;
        [Header("Placement grid")]
        [Tooltip("Centre the largest complete 4x4 furniture grid inside the plot. Keeps Grid Origin height/rotation and does not move existing objects or change wall snapping.")]
        public bool fitFurnitureGridToPlot;
        public bool TryFurnitureGridLayout(out Vector2 origin,out Vector2Int cells)
        {
            origin=Vector2.zero;cells=Vector2Int.zero;
            if(!fitFurnitureGridToPlot)return false;
            var matrix=Grid.worldToLocalMatrix*Volume.transform.localToWorldMatrix;
            var right=matrix.MultiplyVector(Vector3.right);var forward=matrix.MultiplyVector(Vector3.forward);var up=matrix.MultiplyVector(Vector3.up);
            // A non-axis-aligned parcel needs a polygonal layout, not its larger
            // enclosing AABB. Retain the authored grid in that unsupported case.
            if(Mathf.Max(Mathf.Abs(right.normalized.x),Mathf.Abs(right.normalized.z))<.999f || Mathf.Abs(up.normalized.y)<.999f)return false;
            var center=matrix.MultiplyPoint3x4(Volume.center);var half=Volume.size*.5f;
            float width=2*(Mathf.Abs(right.x)*half.x+Mathf.Abs(up.x)*half.y+Mathf.Abs(forward.x)*half.z);
            float depth=2*(Mathf.Abs(right.z)*half.x+Mathf.Abs(up.z)*half.y+Mathf.Abs(forward.z)*half.z);
            cells=new Vector2Int(Mathf.FloorToInt((width+.001f)/CellSize),Mathf.FloorToInt((depth+.001f)/CellSize));
            if(cells.x<1 || cells.y<1)return false;
            origin=new Vector2(center.x-cells.x*CellSize*.5f,center.z-cells.y*CellSize*.5f);return true;
        }
        public Vector2 FurnitureGridOffset=>TryFurnitureGridLayout(out var origin,out _)?origin:Vector2.zero;
        public Vector2Int FurnitureCell(Vector3 world)
        {
            var local=Grid.InverseTransformPoint(world);var origin=FurnitureGridOffset;
            return new Vector2Int(Mathf.FloorToInt((local.x-origin.x)/CellSize),Mathf.FloorToInt((local.z-origin.y)/CellSize));
        }
        [Tooltip("Paso de colocacion en X/Z. No cambia el tamano de los objetos ni la altura de las plantas.")]
        [Min(.1f)] public float placementStep = 1f;
        [Tooltip("Distancia maxima de ajuste a un extremo vecino. Cero desactiva este ajuste.")]
        [Min(0)] public float endpointSnapDistance = .35f;
        public static readonly HashSet<BuildPlot> Active = new HashSet<BuildPlot>();
        public BoxCollider Volume => GetComponent<BoxCollider>();
        public Transform Grid => gridOrigin ? gridOrigin : transform;
        private void Reset() { Volume.isTrigger = true; Volume.size = new Vector3(32,12,32); Volume.center = new Vector3(0,6,0); }
        private void OnEnable() { Active.Add(this);if(Application.isPlaying && !GetComponent<RoomConstruction>())gameObject.AddComponent<RoomConstruction>(); }
        private void OnDisable() { Active.Remove(this); if(boundary) boundary.enabled=false; }
        private void Start(){if(Application.isPlaying)BuildDeletionLedger.Migrate(this);}
        private void LateUpdate()
        {
            bool visible=showBoundary && ConstructionController.Active && ConstructionController.Active.VisiblePlot==this;
            if(!visible) { if(boundary) boundary.enabled=false; return; }
            if(!boundary)
            {
                var shader=Shader.Find("Universal Render Pipeline/Unlit");
                if(!shader)return;
                boundaryMaterial=new Material(shader);
                var go=new GameObject("ConstructionBoundary");go.layer=2;
                go.transform.SetParent(transform,false);
                boundary=go.AddComponent<LineRenderer>();boundary.sharedMaterial=boundaryMaterial;
                boundary.loop=true;boundary.useWorldSpace=true;boundary.positionCount=4;
                boundary.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                boundary.receiveShadows=false;
            }
            boundary.enabled=true;boundary.widthMultiplier=boundaryWidth;
            boundaryMaterial.SetColor("_BaseColor",boundaryColor);
            var c=Volume.center;var half=Volume.size*.5f;
            for(int i=0;i<4;i++)
            {
                var p=transform.TransformPoint(new Vector3(c.x+(i==0 || i==3 ? -half.x : half.x),c.y,c.z+(i<2 ? -half.z : half.z)));
                p+=Grid.up*(boundaryHeight-Vector3.Dot(p-Grid.position,Grid.up));
                boundary.SetPosition(i,p);
            }
        }
        private void OnDestroy()
        {
            if(boundary)Destroy(boundary.gameObject);
            if(boundaryMaterial)Destroy(boundaryMaterial);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Clear() => Active.Clear();
        public bool Contains(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world) - Volume.center;
            Vector3 half = Volume.size * .5f;
            return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z;
        }
        public bool CanBuild(PropertyService properties) => buildable && propertyId >= 0 && properties && properties.IsOwned(propertyId);
        // The player's pivot can sit below floor level while its capsule is inside.
        // Keep the strict volume check for placed geometry; use the body for occupants.
        public bool ContainsPlayer(Transform player, CharacterController capsule)
            => Contains(capsule ? capsule.transform.TransformPoint(capsule.center) : player.position);
        public Vector2Int Cell(Vector3 world)
        {
            var p = Grid.InverseTransformPoint(world);
            return new Vector2Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));
        }
        public Vector3 Center(Vector2Int cell) => Grid.TransformPoint(new Vector3((cell.x+.5f)*CellSize,0,(cell.y+.5f)*CellSize));
        public Vector3 SnapOrigin(Vector3 world)
        {
            var local=Grid.InverseTransformPoint(world);
            float step=Mathf.Max(.1f,placementStep);
            local.x=Mathf.Round(local.x/step)*step;
            local.z=Mathf.Round(local.z/step)*step;
            return Grid.TransformPoint(local);
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix; Gizmos.color = buildable ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(Volume.center, Volume.size);
        }
    }
}
