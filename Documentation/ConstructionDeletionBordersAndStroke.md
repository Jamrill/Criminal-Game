# Sold objects, parcel bounds and wall strokes

## Sold objects

Sold models and their children are destroyed (immediately in editor diagnostics, at frame end in Play). They are disabled only during that one frame to stop collisions and duplicate refunds. `BuildDeletionLedger` stores plain BuildRecord data on Solar, not hidden model GameObjects. The data suppresses authored objects on load and preserves intentional floor/stair openings. Records are stored plot-local so they follow a moved plot; save files retain the existing world-space record format.

Old inactive deleted objects migrate when Play starts or a save is restored. Loading deleted records does not instantiate prefabs. Restoring a live identity removes its deletion record. Save Game and the explicit Play-to-scene capture both include the ledger; the editor capture removes sold scene objects with Undo support.

## Parcel edges

Ground previews clamp their complete rotated bounds into the active plot in X/Z, including a 0.1 mm inward numerical clearance. They preserve height. Objects too large to fit remain invalid. Endpoint snapping is followed by the same boundary clamp. Placement validation and the economy also reject out-of-plot geometry; mounted items retain their host attachment and strict validation rather than moving away from it.

## Holding left click

Pressing left click on a valid single-span wall begins a mesh-only transparent preview stroke. Holding extends consecutive authored spans toward the camera's ground-plane target, filling skipped spans without creating real objects or charging money. Releasing validates the whole pending set and builds it with one combined debit. Invalid space or insufficient total funds rejects the whole set; a failure during spawning rolls back all objects and refunds the debit. A collision or parcel boundary stops preview extension. Existing-wall conversions and demolition remain explicit single-click operations. Straight and diagonal single-span walls are supported; multi-branch corner/T/cross pieces remain single-click.

Releasing the mouse confirms the stroke. Changing item/plot, Alt, tablet/pause/exit or R cancels it without charging. R keeps its normal rotation function; click again to start another direction. Source collider sizes are unchanged.

## Verification

- `ConstructionEditingUpgradeValidation`: 242 checks, including save/restore, legacy model migration, plot-local deleted data, rotated/scaled bounds, closed perimeter detection, prices, sequential brush positions and deferred batch construction/rollback. Also runs the large-roof, source-scene roof, foundation/stair opening and wall/economy regressions. Latest run: ConstructionDeferredWallsValidation.log.
- `ConstructionDeletionPlayValidation`: 7 actual Play checks for frame-end destruction, save suppression and live replacements surviving migration of pending-destroy legacy objects.
- A separate broad legacy UI test currently fails its exact `Slot palette source` assertion. It concerns the existing SlotRow style reference, outside these construction changes; no UI prefab/style was altered for this task.

Reports: ConstructionEditingUpgradeValidation.txt/.log and ConstructionDeletionPlayValidation.txt/.log.
