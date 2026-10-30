# Staircase in a closed plot corner: missing upper walls and slab origin

## Reproduction

`StairStoreyValidation.Run` builds a closed 24x24 lower room and places Stairs
against two perimeter edges. The plot is at (474,109,507), with Grid Origin eight
units below it. Four quarter turns are tested with real catalogue prefabs.

The baseline produced 32 failed assertions (`StairStoreyBaseline.log`).

## Causes and changes

1. The staircase's existing-wall check used `Span.Distance`, intentionally a
   horizontal projection for selecting wall arms. It therefore counted lower
   walls as upper walls. The enclosure now uses a separate `Distance3D` check
   and only active walls belonging to the same plot. UI selection is unchanged.
2. Automatic ceilings added Thickness (0.02) to the placement origin. Both room
   and staircase ceilings now use the nominal storey datum, just like floors.
   Generated top vertices retain the catalogue Floor's authored local top height
   (approximately +0.01), with 0.02 thickness downward. The resulting pivot is
   local Y=-1 in the reproduced upper floor, not -0.98.
3. Patch surface queries now read the actual local top from the saved triangles,
   so both legacy y=0 patches and new authored-height patches work. Cutting a slab
   no longer moves its transform to its top face.

No source FBX, prefab pivots, user scenes or save files were changed. These are
generation fixes, not a migration of already saved incomplete enclosures or slab
positions. Existing saved geometry retains its placement when loaded.

## Verification

`StairStoreyValidation.Full`, Unity 6000.3.8f1, exit code 0.

44 new assertions passed: local slab heights, authored top offset, all 14 upper
wall arms, four Corner prefabs, genuine upper-window reuse, construction cost,
closed upper room, stable origins after cutting/completion and save/load.
All placement-feedback, stair junction/alignment/clearance, room, wall, mounting,
price, preview and foundation regressions also passed.

Results: `StairStoreyValidation.log` and `StairStoreyValidation.txt`.
