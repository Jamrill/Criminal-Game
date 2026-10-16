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
        [Header("Construction preview")]
        [Tooltip("Opacidad del resaltado verde/rojo. Un valor menor permite ver mejor las formas interiores.")]
        [Range(.1f,1f)] public float previewOpacity=.55f;
        public static ConstructionController Active { get; private set; }
        public bool CapturesInput => captured;
        public BuildPlot VisiblePlot => building && captured ? plot : null;
        private BuildMenuView view;
        private CanvasGroup hudInput;
        private BuildPlot plot;
        private PropertyService properties;
        private EconomyService economy;
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
        private bool demolishing;
        private readonly ConstructionEditing editing=new ConstructionEditing();
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
            view.finishButton.onClick.AddListener(()=>{ demolishing=false;editing.Clear();finish++; if(finish>=catalog.finishes.Length) finish=-1; });
            validMaterial=CreatePreviewMaterial(new Color(.15f,.9f,.3f));
            invalidMaterial=CreatePreviewMaterial(new Color(.95f,.12f,.12f));
            if(catalog && catalog.entries.Length>0) Select(catalog.entries.First(e=>!e.hiddenInMenu));
            RefreshUI();
        }
        private void Update()
        {
            UpdatePreviewOpacity(validMaterial);
            UpdatePreviewOpacity(invalidMaterial);
            editing.Clear();
            if(!view || !catalog) return;
            if(!properties) properties=FindAnyObjectByType<PropertyService>();
            if(!economy) economy=FindAnyObjectByType<EconomyService>();
            var occupied=BuildPlot.Active.Where(p=>p && p.gameObject.scene==gameObject.scene && p.ContainsPlayer(transform,capsule)).ToArray();
            plot=occupied.FirstOrDefault(p=>p.CanBuild(properties));
            blockedReason=!properties ? "Servicios no disponibles" : occupied.Length==0 ? "Entra en tu solar para construir" :
                occupied.Any(p=>properties.IsOwned(p.propertyId)) ? "Este solar no es construible" : "Compra este solar en Propiedades";
            if(captured && (Time.timeScale<=0 || (building && !plot))) { Exit(); return; }
            if(GameInput.TabletPressed && (captured || (Time.timeScale>0 && Cursor.lockState==CursorLockMode.Locked)))
            { menuOpen=!menuOpen; SetCaptured(menuOpen||building); RefreshUI(); }
            if(captured && GameInput.PausePressed) { GameInput.ConsumePausePress(); Exit(); return; }
            UpdateControlMode();
            if(menuOpen)
            {
                view.buildButton.interactable=building || plot;
                view.buildLabel.text=building ? "Salir de construccion" : plot ? "Construir" : blockedReason;
            }
            if(building && !menuOpen && !freeCursor && GameInput.BuildDemolishPressed) { demolishing=!demolishing;finish=-1; }
            if(ghost) ghost.SetActive(building && !menuOpen && finish<0 && !demolishing && !(selected?.IsOpening??false));
            if(!building || menuOpen || !plot || Mouse.current==null) return;
            // Alt is exclusively for the HUD: freeze the preview and forbid world actions,
            // even when the pointer is outside a button or over an empty UI area.
            if(freeCursor) return;
            var camera=Camera.main; if(!camera) return;
            bool overUI=freeCursor && EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            Ray ray=freeCursor ? camera.ScreenPointToRay(Mouse.current.position.ReadValue()) : camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
            if(demolishing || (finish<0 && (selected?.IsOpening??false)))
            {
                view.status.text=editing.Tick(ray,plot,catalog,demolishing?null:selected,invalidMaterial,validMaterial,Mouse.current.leftButton.wasPressedThisFrame && plot.CanBuild(properties),transform,economy);
                return;
            }
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
            if(!overUI) distanceOffset=Mathf.Clamp(distanceOffset+GameInput.BuildDistance*.01f,-20,20);
            if(GameInput.BuildRotatePressed) rotationSteps=(rotationSteps+1)%4;
            if(selected.mount!=BuildMount.Ground)
            {
                UpdateMountedPreview(ray);
                return;
            }
            // Prefer the actual floor under the pointer, including upper/basement floors.
            bool hasHit=Physics.Raycast(ray,out var supportHit,100,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            var level=plot.Grid.position;
            if(Physics.Raycast(transform.position+plot.Grid.up*.2f,-plot.Grid.up,out var below,20,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                level=below.point;
            var plane=new Plane(plot.Grid.up,level);
            Vector3 point;
            if(hasHit && Vector3.Dot(supportHit.normal,plot.Grid.up)>.7f)point=supportHit.point;
            else if(plane.Raycast(ray,out float travel) && travel<=100)point=ray.GetPoint(travel);
            else { ghost.SetActive(false); return; }
            point+=Vector3.ProjectOnPlane(point-transform.position,plot.Grid.up).normalized*distanceOffset;
            var selectedWall=selected.prefab.GetComponent<WallTopology>();
            var cellCenter=plot.SnapOrigin(point);
            // Aim at the upper part of an existing wall to stack the next storey.
            if(selectedWall && hasHit)
            {
                var support=supportHit.collider.GetComponentInParent<WallTopology>();
                if(support && supportHit.point.y>support.Bottom.y+support.WorldHeight*.65f)
                {
                    cellCenter=support.transform.position+support.transform.up*support.WorldHeight;
                }
            }
            var rotation=plot.Grid.rotation*Quaternion.Euler(0,rotationSteps*90,0);
            var position=BuildPlacement.Position(selected,cellCenter,rotation);
            if(selectedWall)
            {
                position=BuildPlacement.SnapWall(selected,plot,position,rotation);
                cellCenter=position+rotation*Vector3.Scale(selectedWall.bottomCenter,selected.prefab.transform.localScale);
            }
            ghost.transform.SetPositionAndRotation(position,rotation);
            Physics.SyncTransforms();
            bool valid=BuildPlacement.IsValid(selected,plot,position,rotation,cellCenter);
            bool affordable=ConstructionEconomy.CanAfford(catalog,selected,economy);
            foreach(var renderer in ghost.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterials=Enumerable.Repeat(valid && affordable ? validMaterial : invalidMaterial,renderer.sharedMaterials.Length).ToArray();
            view.status.text=selected.title+" | $"+catalog.Cost(selected)+"\n"+(!affordable ? "Saldo insuficiente" : valid ? "Verde: clic para colocar" : "Rojo: ocupado o fuera del solar")+"\nAlt: cursor · R: girar\nRueda: distancia · Esc: salir";
            if(!overUI && valid && affordable && Mouse.current.leftButton.wasPressedThisFrame && plot.CanBuild(properties))
            {
                var record=new BuildRecord { id=Guid.NewGuid().ToString("N"),definitionId=selected.id,propertyId=plot.propertyId,
                    position=position,rotation=rotation,scale=selected.prefab.transform.localScale,cellCenter=cellCenter,sceneName=gameObject.scene.name };
                ConstructionEconomy.Build(catalog,record,plot,economy);
            }
        }
        private void ShowCategory(BuildCategory category) => view.Populate(catalog,category,Select);
        private Material CreatePreviewMaterial(Color color)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            color.a=previewOpacity;material.SetColor("_BaseColor",color);
            material.SetFloat("_Surface",1);
            material.SetFloat("_Blend",0);
            material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha",(float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType","Transparent");
            material.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster",false);
            return material;
        }
        private void UpdatePreviewOpacity(Material material)
        {
            if(!material)return;
            var color=material.GetColor("_BaseColor");
            if(Mathf.Approximately(color.a,previewOpacity))return;
            color.a=previewOpacity;material.SetColor("_BaseColor",color);
        }
        private void UpdateMountedPreview(Ray ray)
        {
            Physics.SyncTransforms();
            if(!BuildMounting.TryPose(selected,catalog,plot,ray,rotationSteps,transform,out var pose))
            {
                ghost.SetActive(false);
                view.status.text=selected.mount==BuildMount.Doorway ? "Apunta al hueco de una pared con puerta libre" : "Apunta a la cara inferior del techo de tu solar";
                return;
            }
            ghost.SetActive(true);ghost.transform.SetPositionAndRotation(pose.position,pose.rotation);
            bool affordable=ConstructionEconomy.CanAfford(catalog,selected,economy);
            bool valid=BuildMounting.IsValid(selected,catalog,plot,pose);
            foreach(var renderer in ghost.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterials=Enumerable.Repeat(valid && affordable ? validMaterial : invalidMaterial,renderer.sharedMaterials.Length).ToArray();
            view.status.text=selected.title+" | $"+catalog.Cost(selected)+"\n"+
                (!affordable ? "Saldo insuficiente" : valid ? "Verde: clic para colocar" : "Rojo: espacio ocupado o fuera del solar")+
                (selected.mount==BuildMount.Doorway ? "\nR: invertir puerta" : "\nR: girar plafon")+" | Alt: cursor";
            if(valid && affordable && Mouse.current.leftButton.wasPressedThisFrame && plot.CanBuild(properties))
                ConstructionEconomy.Build(catalog,new BuildRecord{id=Guid.NewGuid().ToString("N"),definitionId=selected.id,
                    propertyId=plot.propertyId,position=pose.position,rotation=pose.rotation,scale=selected.prefab.transform.localScale,
                    cellCenter=pose.anchor,finish=-1,sceneName=gameObject.scene.name},plot,economy);
        }
        private void Select(BuildEntry entry)
        {
            editing.Clear();demolishing=false;
            var previewPosition=ghost ? ghost.transform.position : transform.position;
            var previewRotation=ghost ? ghost.transform.rotation : Quaternion.identity;
            selected=entry; finish=-1; rotationSteps=0; distanceOffset=0;
            if(ghost) Destroy(ghost);
            ghost=new GameObject("ConstructionPreview"); ghost.layer=2; ghost.transform.localScale=entry.prefab.transform.localScale;
            ghost.transform.SetPositionAndRotation(previewPosition,previewRotation);
            void Clone(Transform source,Transform target)
            {
                var sourceRenderer=source.GetComponent<Renderer>();
                if(sourceRenderer && sourceRenderer.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)return;
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
        private void Exit() { editing.Clear();demolishing=false;building=false; menuOpen=false; if(view) RefreshUI(); SetCaptured(false); if(ghost) ghost.SetActive(false); }
        private void OnDisable() { Exit(); }
        private void OnDestroy() { if(view) Destroy(view.gameObject); if(ghost) Destroy(ghost); if(validMaterial) Destroy(validMaterial); if(invalidMaterial) Destroy(invalidMaterial); }
    }
}
