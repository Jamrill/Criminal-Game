using System;
using System.Linq;
using JuegoCriminal.Core;
using JuegoCriminal.Player;
using JuegoCriminal.Inventory;
using JuegoCriminal.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using static JuegoCriminal.Localization.GameLanguage;
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
        public Color validPreviewColor=new(.15f,.9f,.3f,1);
        public Color invalidPreviewColor=new(.95f,.12f,.12f,1);
        [Header("Placement guides")]
        public bool showPlacementGuides=true;
        [Range(1,8)] public int guideGridRadius=4;
        [Min(4)] public float guideAlignmentRange=24;
        public BuildGuideColors guideColors=new();
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
        private MeshPreview ghostVisual;
        private Material validMaterial, invalidMaterial;
        private bool building, menuOpen, captured, oldMove, oldInteract, oldInventory, oldCursor;
        private CursorLockMode oldLock;
        private ThirdPersonController movement;
        private InteractorRaycast interactor;
        private InventoryMenuUI inventory;
        private int rotationSteps, finish=-1;
        private float distanceOffset;
        private bool demolishing;
        private readonly WallBuildStroke wallStroke=new();
        private readonly System.Collections.Generic.List<BuildRecord> pendingWalls=new();
        private readonly System.Collections.Generic.List<MeshPreview> pendingPreviews=new();
        private readonly System.Collections.Generic.List<Collider> placementBlockers=new();
        private void CancelPlacement()
        {
            CancelWallStroke();guides?.Hide();editing.Clear();placementBlockers.Clear();
            demolishing=false;finish=-1;selected=null;stairPlan=null;
            ghostVisual?.Dispose();ghostVisual=null;ghost=null;
            if(view)view.status.text=T("Placement cancelled. Choose an item to build.","Colocación cancelada. Elige un objeto para construir.");
        }
        private void CancelWallStroke()
        {
            wallStroke.Reset();pendingWalls.Clear();
            foreach(var preview in pendingPreviews)preview.Dispose();
            pendingPreviews.Clear();
        }
        private void QueueWall(Vector3 position,Quaternion rotation,Vector3 anchor)
        {
            pendingWalls.Add(new BuildRecord{id=Guid.NewGuid().ToString("N"),definitionId=selected.id,propertyId=plot.propertyId,
                position=position,rotation=rotation,scale=selected.prefab.transform.localScale,cellCenter=anchor,sceneName=gameObject.scene.name,finish=-1});
            // Clone the mesh-only preview, never the interactive prefab or its colliders.
            pendingPreviews.Add(MeshPreview.Create(selected.prefab,position,rotation,selected.prefab.transform.localScale,validMaterial,"PendingWallPreview"));
        }
        private StairBuildPlan stairPlan;
        private int stairRevision=-1;
        private Vector3 stairPosition;
        private Quaternion stairRotation;
        private string roomWarning;
        private void LateUpdate()
        {
            if(!view || !view.status)return;
            if(building && !menuOpen && (selected!=null || finish>=0 || demolishing))
            {
                var hint=T("Right click: cancel","Clic derecho: cancelar");
                if(!view.status.text.Contains(hint))view.status.text+="\n"+hint;
            }
            if(!string.IsNullOrEmpty(roomWarning))view.status.text=view.status.text.Replace("\n"+roomWarning,"");
            roomWarning=building && plot?plot.GetComponent<RoomConstruction>()?.Warning:null;
            if(!string.IsNullOrEmpty(roomWarning))view.status.text+="\n"+roomWarning;
        }
        private readonly ConstructionEditing editing=new ConstructionEditing();
        private BuildPlacementGuides guides;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Clear() => Active=null;
        private void Start()
        {
            movement=GetComponent<ThirdPersonController>(); interactor=GetComponent<InteractorRaycast>(); inventory=GetComponent<InventoryMenuUI>();
            capsule=GetComponent<CharacterController>();
            view=embeddedUI ? embeddedUI : uiPrefab ? Instantiate(uiPrefab,transform,false) : BuildMenuView.Create();
            if (!embeddedUI && !uiPrefab) view.transform.SetParent(transform,false);
            view.EnsurePropertiesUI(false);
            hudInput=view.hud.GetComponent<CanvasGroup>() ?? view.hud.AddComponent<CanvasGroup>();
            if(view.propertiesButton && view.propertiesPanel)view.propertiesButton.onClick.AddListener(()=>view.SetPropertiesVisible(!view.propertiesPanel.gameObject.activeSelf));
            if(view.hudPropertiesButton && view.propertiesPanel)view.hudPropertiesButton.onClick.AddListener(()=>{ menuOpen=true; RefreshUI(); view.SetPropertiesVisible(true); });
            view.buildButton.onClick.AddListener(()=>{ if(building || plot) building=!building; RefreshUI(); });
            view.wallsButton.onClick.AddListener(()=>ShowCategory(BuildCategory.Walls));
            view.furnitureButton.onClick.AddListener(()=>ShowCategory(BuildCategory.Furniture));
            view.finishButton.onClick.AddListener(()=>{ demolishing=false;editing.Clear();finish++; if(finish>=catalog.finishes.Length) finish=-1; });
            validMaterial=CreatePreviewMaterial(validPreviewColor);
            invalidMaterial=CreatePreviewMaterial(invalidPreviewColor);
            if(catalog && catalog.entries.Length>0) Select(catalog.entries.First(e=>!e.hiddenInMenu));
            RefreshUI();
        }
        private void Update()
        {
            UpdatePreviewColor(validMaterial,validPreviewColor);
            UpdatePreviewColor(invalidMaterial,invalidPreviewColor);
            editing.Clear();
            placementBlockers.Clear();
            if(!view || !catalog) return;
            if(!properties) properties=FindAnyObjectByType<PropertyService>();
            if(!economy) economy=FindAnyObjectByType<EconomyService>();
            var occupied=BuildPlot.Active.Where(p=>p && p.gameObject.scene==gameObject.scene && p.ContainsPlayer(transform,capsule)).ToArray();
            plot=occupied.FirstOrDefault(p=>p.CanBuild(properties));
            if(wallStroke.Active && wallStroke.Plot!=plot)CancelWallStroke();
            blockedReason=!properties ? T("Services unavailable","Servicios no disponibles") : occupied.Length==0 ? T("Enter your plot to build","Entra en tu solar para construir") :
                occupied.Any(p=>properties.IsOwned(p.propertyId)) ? T("Building is not allowed on this plot","Este solar no es construible") : T("Buy this plot in Properties","Compra este solar en Propiedades");
            if(captured && (Time.timeScale<=0 || (building && !plot))) { Exit(); return; }
            if(GameInput.TabletPressed && (captured || (Time.timeScale>0 && Cursor.lockState==CursorLockMode.Locked)))
            { menuOpen=!menuOpen; SetCaptured(menuOpen||building); RefreshUI(); }
            if(captured && GameInput.PausePressed) { GameInput.ConsumePausePress(); Exit(); return; }
            UpdateControlMode();
            // Cancellation wins even if left release arrives in the same frame:
            // discard the pending stroke before its payment/build transaction.
            if(building && !menuOpen && Mouse.current!=null && Mouse.current.rightButton.wasPressedThisFrame)
            {CancelPlacement();return;}
            if(menuOpen || freeCursor || !building)CancelWallStroke();
            if(wallStroke.Active && Mouse.current!=null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                bool placed=ConstructionEconomy.BuildWalls(catalog,pendingWalls,plot,economy);
                CancelWallStroke();
                if(!placed)view.status.text=T("Walls not built: occupied space or insufficient funds","Paredes no construidas: espacio ocupado o saldo insuficiente");
                return;
            }
            if(wallStroke.Active && (Mouse.current==null || !Mouse.current.leftButton.isPressed))CancelWallStroke();
            if(menuOpen)
            {
                view.buildButton.interactable=building || plot;
                view.buildLabel.text=building ? T("Exit building mode","Salir de construcción") : plot ? T("Build","Construir") : blockedReason;
            }
            if(building && !menuOpen && !freeCursor && GameInput.BuildDemolishPressed) { CancelWallStroke();demolishing=!demolishing;finish=-1; }
            if(ghost) ghost.SetActive(building && !menuOpen && finish<0 && !demolishing && !(selected?.IsOpening??false));
            if(!building || menuOpen || !plot || Mouse.current==null){guides?.Hide();return;}
            // Alt is exclusively for the HUD: freeze the preview and forbid world actions,
            // even when the pointer is outside a button or over an empty UI area.
            if(freeCursor) return;
            guides?.Hide();
            var camera=Camera.main; if(!camera) return;
            bool overUI=freeCursor && EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            Ray ray=freeCursor ? camera.ScreenPointToRay(Mouse.current.position.ReadValue()) : camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
            bool aimedReplacement=!wallStroke.Active && finish<0 && selected?.prefab && selected.mount==BuildMount.Ground && selected.prefab.GetComponent<WallTopology>() &&
                WallGeometry.Local(selected.prefab.GetComponent<WallTopology>()).Count==1 && ConstructionEditing.FindWall(ray,plot,catalog,transform,out _,out _);
            if(demolishing || (finish<0 && ((selected?.IsOpening??false) || aimedReplacement)))
            {
                if(ghost)ghost.SetActive(false);
                view.status.text=editing.Tick(ray,plot,catalog,demolishing?null:selected,invalidMaterial,validMaterial,Mouse.current.leftButton.wasPressedThisFrame && plot.CanBuild(properties),transform,economy);
                return;
            }
            if(finish>=0)
            {
                view.status.text=T("Paint: {0}\nClick a wall you own · Esc: exit","Pintar: {0}\nClic en una pared propia · Esc: salir",catalog.finishes[finish].name);
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
            if(GameInput.BuildRotatePressed){CancelWallStroke();rotationSteps=(rotationSteps+1)%4;}
            if(selected.mount!=BuildMount.Ground)
            {
                UpdateMountedPreview(ray);
                return;
            }
            // Prefer the actual floor under the pointer, including upper/basement floors.
            bool hasHit=Physics.Raycast(ray,out var supportHit,100,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            var selectedWall=selected.prefab.GetComponent<WallTopology>();
            var level=plot.Grid.position;
            if(Physics.Raycast(transform.position+plot.Grid.up*.2f,-plot.Grid.up,out var below,20,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                level=below.point;
            if(selectedWall || StairConstruction.IsStairs(selected))level=BuildPlacement.WallSupportLevel(plot,transform.position);
            bool hasPoint=BuildPlacement.TrySurfacePoint(selected,plot,ray,hasHit,supportHit,level,out var point);
            if(wallStroke.Active)point=wallStroke.Aim(ray,hasPoint?point:ray.GetPoint(20));
            else if(!hasPoint){ghost.SetActive(false);return;}
            point+=Vector3.ProjectOnPlane(point-transform.position,plot.Grid.up).normalized*distanceOffset;
            var cellCenter=plot.SnapOrigin(point);
            // Aim at the upper part of an existing wall to stack the next storey.
            if(selectedWall && !wallStroke.Active && hasHit)
            {
                var support=supportHit.collider.GetComponentInParent<WallTopology>();
                if(support && supportHit.point.y>support.Bottom.y+support.WorldHeight*.65f)
                {
                    cellCenter=support.transform.position+support.transform.up*support.WorldHeight;
                }
            }
            var rotation=plot.Grid.rotation*Quaternion.Euler(0,rotationSteps*90,0);
            if(BuildFootprint.Uses(selected) || FloorEditing.IsFloor(selected))cellCenter=BuildFootprint.SnapAnchor(selected,plot,FloorEditing.IsFloor(selected)?cellCenter:point,rotation);
            var position=BuildPlacement.Position(selected,cellCenter,rotation);
            position=BuildPlacement.ClampToPlot(selected,plot,position,rotation);
            if(StairConstruction.IsStairs(selected))
            {
                position=StairConstruction.SnapToStorey(plot,position);
                position=StairConstruction.SnapBoundary(selected,plot,position,rotation);
                position=StairConstruction.SnapVacatedPosition(selected,plot,position,rotation);
                cellCenter=BuildFootprint.Anchor(selected,position,rotation);
            }
            if(selectedWall && !wallStroke.Active)
            {
                position=StairConstruction.SnapToStorey(plot,position);
                if(hasHit)position=WallReconfiguration.SnapReplacement(catalog,selected,supportHit.collider.GetComponentInParent<PlacedBuildObject>(),position,rotation,supportHit.point);
                position=BuildPlacement.SnapWall(selected,plot,position,rotation);
                position=BuildPlacement.ClampToPlot(selected,plot,position,rotation);
                cellCenter=position+rotation*Vector3.Scale(selectedWall.bottomCenter,selected.prefab.transform.localScale);
            }
            var strokeTarget=position;
            int strokeIndex=0;bool strokeCandidate=!wallStroke.Active || wallStroke.Next(position,out position,out strokeIndex);
            if(wallStroke.Active)
            {
                // Fill skipped spans with previews only; payment and real objects wait for release.
                for(int i=0;i<1024 && wallStroke.Next(strokeTarget,out var next,out int index);i++)
                {
                    var anchor=next+rotation*Vector3.Scale(selectedWall.bottomCenter,selected.prefab.transform.localScale);
                    if(!BuildPlacement.IsValid(selected,plot,next,rotation,anchor,placementBlockers))break;
                    QueueWall(next,rotation,anchor);wallStroke.Commit(index);
                }
                bool canPay=economy && (long)catalog.Cost(selected)*pendingWalls.Count<=economy.Money;
                foreach(var preview in pendingPreviews)preview.SetMaterial(canPay?validMaterial:invalidMaterial);
                ghost.SetActive(false);
                editing.HighlightBlockers(placementBlockers,invalidMaterial);
                view.status.text=T("{0} walls | ${1}\nRelease left click to build · Right click: cancel","{0} paredes | ${1}\nSuelta el clic izquierdo para construir · Clic derecho: cancelar",pendingWalls.Count,(long)catalog.Cost(selected)*pendingWalls.Count);
                return;
            }
            if(wallStroke.Active && selectedWall)cellCenter=position+rotation*Vector3.Scale(selectedWall.bottomCenter,selected.prefab.transform.localScale);
            ghost.transform.SetPositionAndRotation(position,rotation);
            Physics.SyncTransforms();
            var change=selectedWall && !wallStroke.Active ? WallReconfiguration.Placement(catalog,plot,selected,position,rotation) : null;
            if(change!=null)
            {
                bool possible=WallReconfiguration.Validate(change,catalog,out var reason,placementBlockers);
                int delta=possible?change.Difference(catalog):0;
                bool canPay=economy && (delta<=0 || economy.CanAfford(delta));
                if(possible){ghost.SetActive(false);editing.ShowPlan(change,catalog,canPay?validMaterial:invalidMaterial);}
                else {ghostVisual.SetMaterial(invalidMaterial);editing.HighlightBlockers(placementBlockers,invalidMaterial);}
                ShowGuides(position,rotation,cellCenter,possible&&canPay);
                view.status.text=possible ? T("Replace walls | Difference: ${0}\n{1}","Sustituir paredes | Diferencia: ${0}\n{1}",delta,canPay?T("Click to confirm","Clic para confirmar"):T("Insufficient funds","Saldo insuficiente")) : reason;
                if(possible && canPay && Mouse.current.leftButton.wasPressedThisFrame && plot.CanBuild(properties))
                {editing.Clear();if(!WallReconfiguration.Apply(change,catalog,economy,out reason))view.status.text=reason;}
                return;
            }
            bool valid=strokeCandidate && (StairConstruction.IsStairs(selected) || BuildPlacement.IsValid(selected,plot,position,rotation,cellCenter,placementBlockers));
            int placementCost=catalog.Cost(selected);
            if(StairConstruction.IsStairs(selected))
            {
                if(stairPlan==null || stairPlan.plot!=plot || stairRevision!=RoomConstruction.Revision || stairPosition!=position || stairRotation!=rotation)
                {
                    stairPlan=StairConstruction.Plan(catalog,plot,new BuildRecord{definitionId=selected.id,propertyId=plot.propertyId,position=position,rotation=rotation,scale=selected.prefab.transform.localScale,cellCenter=cellCenter,sceneName=gameObject.scene.name,finish=-1});
                    stairPosition=position;stairRotation=rotation;stairRevision=RoomConstruction.Revision;
                }
                placementBlockers.Clear();valid=StairConstruction.Validate(stairPlan,catalog);placementCost=stairPlan.Cost(catalog);
                placementBlockers.AddRange(stairPlan.blockers);
                editing.ShowStairPlan(stairPlan,catalog,validMaterial,invalidMaterial);
            }
            if(!valid)editing.HighlightBlockers(placementBlockers,invalidMaterial);
            bool affordable=economy && economy.CanAfford(placementCost);
            ShowGuides(position,rotation,cellCenter,valid && affordable);
            ghostVisual.SetMaterial(valid && affordable ? validMaterial : invalidMaterial);
            view.status.text=ConstructionText.Title(selected)+" | $"+placementCost+"\n"+(!affordable ? T("Insufficient funds","Saldo insuficiente") : valid ? T("Green: click to place","Verde: clic para colocar") : StairConstruction.IsStairs(selected) && !string.IsNullOrEmpty(stairPlan.error)?stairPlan.error:T("Red: occupied or outside the plot","Rojo: ocupado o fuera del solar"))+T("\nAlt: cursor · R: rotate\nWheel: distance · Esc: exit","\nAlt: cursor · R: girar\nRueda: distancia · Esc: salir");
            if(!overUI && valid && affordable && (Mouse.current.leftButton.wasPressedThisFrame || (wallStroke.Active && Mouse.current.leftButton.isPressed)) && plot.CanBuild(properties))
            {
                if(selectedWall && wallStroke.Begin(selected,plot,position,rotation))
                {
                    QueueWall(position,rotation,cellCenter);ghost.SetActive(false);return;
                }
                var record=new BuildRecord { id=Guid.NewGuid().ToString("N"),definitionId=selected.id,propertyId=plot.propertyId,
                    position=position,rotation=rotation,scale=selected.prefab.transform.localScale,cellCenter=cellCenter,sceneName=gameObject.scene.name };
                editing.Clear();
                if(ConstructionEconomy.Build(catalog,record,plot,economy))
                {
                    // Multi-branch pieces retain explicit single-click placement.
                }
            }
        }
        private void ShowCategory(BuildCategory category) => view.Populate(catalog,category,Select);
        private void ShowGuides(Vector3 position,Quaternion rotation,Vector3 anchor,bool valid)
        {
            if(!showPlacementGuides){guides?.Hide();return;}
            guides??=new BuildPlacementGuides();
            guides.Show(selected,catalog,plot,position,rotation,anchor,valid,transform,guideGridRadius,guideAlignmentRange,guideColors);
        }
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
        private void UpdatePreviewColor(Material material,Color color)
        {
            if(!material)return;
            color.a=previewOpacity;material.SetColor("_BaseColor",color);
        }
        private void UpdateMountedPreview(Ray ray)
        {
            Physics.SyncTransforms();
            if(!BuildMounting.TryPose(selected,catalog,plot,ray,rotationSteps,transform,out var pose))
            {
                ghost.SetActive(false);
                view.status.text=selected.mount==BuildMount.Doorway ? T("Aim at an empty doorway","Apunta al hueco de una pared con puerta libre") : T("Aim at the underside of your plot's ceiling","Apunta a la cara inferior del techo de tu solar");
                return;
            }
            ghost.SetActive(true);ghost.transform.SetPositionAndRotation(pose.position,pose.rotation);
            bool affordable=ConstructionEconomy.CanAfford(catalog,selected,economy);
            bool valid=BuildMounting.IsValid(selected,catalog,plot,pose,placementBlockers);
            if(!valid)editing.HighlightBlockers(placementBlockers,invalidMaterial);
            ShowGuides(pose.position,pose.rotation,pose.anchor,valid && affordable);
            ghostVisual.SetMaterial(valid && affordable ? validMaterial : invalidMaterial);
            view.status.text=ConstructionText.Title(selected)+" | $"+catalog.Cost(selected)+"\n"+
                (!affordable ? T("Insufficient funds","Saldo insuficiente") : valid ? T("Green: click to place","Verde: clic para colocar") : T("Red: occupied or outside the plot","Rojo: espacio ocupado o fuera del solar"))+
                (selected.mount==BuildMount.Doorway ? T("\nR: flip door","\nR: invertir puerta") : T("\nR: rotate light","\nR: girar plafón"))+" | Alt: cursor";
            if(valid && affordable && Mouse.current.leftButton.wasPressedThisFrame && plot.CanBuild(properties))
                ConstructionEconomy.Build(catalog,new BuildRecord{id=Guid.NewGuid().ToString("N"),definitionId=selected.id,
                    propertyId=plot.propertyId,position=pose.position,rotation=pose.rotation,scale=selected.prefab.transform.localScale,
                    cellCenter=pose.anchor,finish=-1,sceneName=gameObject.scene.name},plot,economy);
        }
        private void Select(BuildEntry entry)
        {
            CancelWallStroke();
            guides?.Hide();
            editing.Clear();demolishing=false;
            var previewPosition=ghost ? ghost.transform.position : transform.position;
            var previewRotation=ghost ? ghost.transform.rotation : Quaternion.identity;
            selected=entry; finish=-1; rotationSteps=0; distanceOffset=0;
            ghostVisual?.Dispose();
            ghostVisual=MeshPreview.Create(entry.prefab,previewPosition,previewRotation,entry.prefab.transform.localScale,invalidMaterial,"ConstructionPreview");
            ghost=ghostVisual.Root;ghost.SetActive(false);
        }
        private void RefreshUI()
        {
            if(!building || menuOpen)guides?.Hide();
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
        private void Exit() { CancelWallStroke();guides?.Hide();editing.Clear();demolishing=false;building=false; menuOpen=false; if(view) RefreshUI(); SetCaptured(false); if(ghost) ghost.SetActive(false); }
        private void OnDisable() { Exit(); }
        private void OnDestroy() { guides?.Dispose();editing.Dispose();ghostVisual?.Dispose();if(view) Destroy(view.gameObject); if(validMaterial) Destroy(validMaterial); if(invalidMaterial) Destroy(invalidMaterial); }
    }
}
