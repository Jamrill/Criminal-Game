using System;
using System.Linq;
using JuegoCriminal.Core;
using JuegoCriminal.Player;
using JuegoCriminal.Inventory;
using JuegoCriminal.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace JuegoCriminal.Construction
{
    [DefaultExecutionOrder(-250)]
    public sealed class ConstructionController : MonoBehaviour
    {
        public BuildCatalog catalog;
        public BuildMenuView uiPrefab;
        public BuildMenuView embeddedUI;
        public static ConstructionController Active { get; private set; }
        public bool CapturesInput => captured;
        private BuildMenuView view;
        private CanvasGroup hudInput;
        private BuildPlot plot;
        private PropertyService properties;
        private CharacterController capsule;
        private bool freeCursor;
        private string blockedReason;
        private BuildEntry selected;
        private GameObject ghost;
        private Material validMaterial, invalidMaterial;
        private bool building, menuOpen, captured, oldMove, oldInteract, oldInventory, oldCursor;
        private CursorLockMode oldLock;
        private ThirdPersonController movement;
        private InteractorRaycast interactor;
        private InventoryMenuUI inventory;
        private int rotationSteps, finish=-1;
        private float distanceOffset;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Clear() => Active=null;
        private void Start()
        {
            movement=GetComponent<ThirdPersonController>(); interactor=GetComponent<InteractorRaycast>(); inventory=GetComponent<InventoryMenuUI>();
            capsule=GetComponent<CharacterController>();
            view=embeddedUI ? embeddedUI : uiPrefab ? Instantiate(uiPrefab,transform,false) : BuildMenuView.Create();
            if (!embeddedUI && !uiPrefab) view.transform.SetParent(transform,false);
            view.EnsurePropertiesUI();
            hudInput=view.hud.GetComponent<CanvasGroup>() ?? view.hud.AddComponent<CanvasGroup>();
            view.propertiesButton.onClick.AddListener(()=>view.SetPropertiesVisible(!view.propertiesPanel.gameObject.activeSelf));
            view.hudPropertiesButton.onClick.AddListener(()=>{ menuOpen=true; RefreshUI(); view.SetPropertiesVisible(true); });
            view.buildButton.onClick.AddListener(()=>{ if(building || plot) building=!building; RefreshUI(); });
            view.wallsButton.onClick.AddListener(()=>ShowCategory(BuildCategory.Walls));
            view.furnitureButton.onClick.AddListener(()=>ShowCategory(BuildCategory.Furniture));
            view.finishButton.onClick.AddListener(()=>{ finish++; if(finish>=catalog.finishes.Length) finish=-1; });
            var shader=Shader.Find("Universal Render Pipeline/Unlit");
            validMaterial=new Material(shader); validMaterial.SetColor("_BaseColor",new Color(.15f,.9f,.3f));
            invalidMaterial=new Material(shader); invalidMaterial.SetColor("_BaseColor",new Color(.95f,.12f,.12f));
            if(catalog && catalog.entries.Length>0) Select(catalog.entries.First(e=>!e.hiddenInMenu));
            RefreshUI();
        }
        private void Update()
        {
            if(!view || !catalog) return;
            if(!properties) properties=FindAnyObjectByType<PropertyService>();
            var occupied=BuildPlot.Active.Where(p=>p && p.gameObject.scene==gameObject.scene && p.ContainsPlayer(transform,capsule)).ToArray();
            plot=occupied.FirstOrDefault(p=>p.CanBuild(properties));
            blockedReason=!properties ? "Servicios no disponibles" : occupied.Length==0 ? "Entra en tu solar para construir" :
                occupied.Any(p=>properties.IsOwned(p.propertyId)) ? "Este solar no es construible" : "Compra este solar en Propiedades";
            if(captured && (Time.timeScale<=0 || (building && !plot))) { Exit(); return; }
            if(GameInput.ConstructionPressed && (captured || (Time.timeScale>0 && Cursor.lockState==CursorLockMode.Locked)))
            { menuOpen=!menuOpen; SetCaptured(menuOpen||building); RefreshUI(); }
            if(captured && GameInput.PausePressed) { GameInput.ConsumePausePress(); Exit(); return; }
            UpdateControlMode();
            if(menuOpen)
            {
                view.buildButton.interactable=building || plot;
                view.buildLabel.text=building ? "Salir de construccion" : plot ? "Construir" : blockedReason;
            }
            if(ghost) ghost.SetActive(building && !menuOpen && finish<0);
            if(!building || menuOpen || !plot || Mouse.current==null) return;
            var camera=Camera.main; if(!camera) return;
            bool overUI=freeCursor && EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            Ray ray=freeCursor ? camera.ScreenPointToRay(Mouse.current.position.ReadValue()) : camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
            if(finish>=0)
            {
                view.status.text="Pintar: "+catalog.finishes[finish].name+"\nClic en una pared propia · Esc: salir";
                if(!overUI && Mouse.current.leftButton.wasPressedThisFrame && Physics.Raycast(ray,out var hit,100,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                {
                    var surface=hit.collider.GetComponentInParent<BuildSurface>();
                    if(!surface) surface=hit.collider.transform.root.GetComponentInChildren<BuildSurface>();
                    if(surface && plot.Contains(hit.point))
                    {
                        var marker=surface.GetComponentInParent<PlacedBuildObject>();
                        if(marker && marker.propertyId==plot.propertyId) { surface.Apply(catalog.finishes[finish]); marker.finish=finish; }
                    }
                }
                return;
            }
            if(!selected?.prefab || !ghost) return;
            if(!overUI) distanceOffset=Mathf.Clamp(distanceOffset+Mouse.current.scroll.ReadValue().y*.01f,-20,20);
            if(GameInput.BuildRotatePressed) rotationSteps=(rotationSteps+1)%4;
            var plane=new Plane(plot.Grid.up,plot.Grid.position);
            if(!plane.Raycast(ray,out float travel) || travel>100) { ghost.SetActive(false); return; }
            var point=ray.GetPoint(travel);
            point+=Vector3.ProjectOnPlane(point-transform.position,plot.Grid.up).normalized*distanceOffset;
            var cellCenter=plot.Center(plot.Cell(point));
            // Aim at the upper part of an existing wall to stack the next storey.
            if(selected.prefab.GetComponent<WallTopology>() && Physics.Raycast(ray,out var supportHit,100,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                var support=supportHit.collider.GetComponentInParent<WallTopology>();
                if(support && supportHit.point.y>support.Bottom.y+support.WorldHeight*.65f)
                {
                    cellCenter=plot.Center(plot.Cell(support.Bottom));
                    cellCenter.y=support.Bottom.y+support.WorldHeight;
                }
            }
            var rotation=plot.Grid.rotation*Quaternion.Euler(0,rotationSteps*90,0);
            var position=BuildPlacement.Position(selected,cellCenter,rotation);
            ghost.transform.SetPositionAndRotation(position,rotation);
            Physics.SyncTransforms();
            bool valid=BuildPlacement.IsValid(selected,plot,position,rotation,cellCenter);
            foreach(var renderer in ghost.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterials=Enumerable.Repeat(valid ? validMaterial : invalidMaterial,renderer.sharedMaterials.Length).ToArray();
            view.status.text=selected.title+"\n"+(valid ? "Verde: clic para colocar" : "Rojo: ocupado o fuera del solar")+"\nAlt: cursor · R: girar\nRueda: distancia · Esc: salir";
            if(!overUI && valid && Mouse.current.leftButton.wasPressedThisFrame && plot.CanBuild(properties))
            {
                var record=new BuildRecord { id=Guid.NewGuid().ToString("N"),definitionId=selected.id,propertyId=plot.propertyId,
                    position=position,rotation=rotation,scale=selected.prefab.transform.localScale,cellCenter=cellCenter,sceneName=gameObject.scene.name };
                ConstructionPersistence.Spawn(catalog,record,true);
            }
        }
        private void ShowCategory(BuildCategory category) => view.Populate(catalog,category,Select);
        private void Select(BuildEntry entry)
        {
            selected=entry; finish=-1; rotationSteps=0; distanceOffset=0;
            if(ghost) Destroy(ghost);
            ghost=new GameObject("ConstructionPreview"); ghost.layer=2; ghost.transform.localScale=entry.prefab.transform.localScale;
            void Clone(Transform source,Transform target)
            {
                var mesh=source.GetComponent<MeshFilter>();
                if(mesh && mesh.sharedMesh)
                {
                    target.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
                    var r=target.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterials=Enumerable.Repeat(invalidMaterial,Mathf.Max(1,mesh.sharedMesh.subMeshCount)).ToArray();
                    r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                foreach(Transform child in source)
                {
                    var g=new GameObject(child.name); g.layer=2; g.transform.SetParent(target,false);
                    g.transform.localPosition=child.localPosition; g.transform.localRotation=child.localRotation; g.transform.localScale=child.localScale;
                    Clone(child,g.transform);
                }
            }
            Clone(entry.prefab.transform,ghost.transform); ghost.SetActive(false);
        }
        private void RefreshUI()
        {
            view.menu.SetActive(menuOpen); view.hud.SetActive(building&&!menuOpen);
            if(!menuOpen && !building) SetCaptured(false);
            if(building && !menuOpen) ShowCategory(BuildCategory.Walls);
            UpdateControlMode();
        }
        private void UpdateControlMode()
        {
            if(!captured) return;
            bool nextFree=menuOpen || GameInput.BuildCursorHeld;
            if(nextFree!=freeCursor && movement) movement.StopHorizontalMotion();
            freeCursor=nextFree;
            if(hudInput) { hudInput.interactable=freeCursor; hudInput.blocksRaycasts=freeCursor; }
            // Movement/head look already honor cursor lock. Keep the component enabled
            // so it clears animation velocity while the cursor is released.
            if(movement) movement.enabled=oldMove;
            Cursor.lockState=freeCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible=freeCursor;
        }
        private void SetCaptured(bool value)
        {
            if(captured==value) return;
            captured=value;
            if(value)
            {
                Active=this; oldLock=Cursor.lockState; oldCursor=Cursor.visible;
                oldMove=movement&&movement.enabled; oldInteract=interactor&&interactor.enabled; oldInventory=inventory&&inventory.enabled;
                if(interactor) interactor.enabled=false; if(inventory) inventory.enabled=false;
                UpdateControlMode();
            }
            else
            {
                if(Active==this) Active=null;
                if(movement) movement.enabled=oldMove; if(interactor) interactor.enabled=oldInteract; if(inventory) inventory.enabled=oldInventory;
                Cursor.lockState=oldLock; Cursor.visible=oldCursor;
            }
        }
        private void Exit() { building=false; menuOpen=false; if(view) RefreshUI(); SetCaptured(false); if(ghost) ghost.SetActive(false); }
        private void OnDisable() { Exit(); }
        private void OnDestroy() { if(view) Destroy(view.gameObject); if(ghost) Destroy(ghost); if(validMaterial) Destroy(validMaterial); if(invalidMaterial) Destroy(invalidMaterial); }
    }
}
