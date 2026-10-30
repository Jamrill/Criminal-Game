# Stair enclosure junctions and thin floating edges

## Reproduced cause

The real stair prefab's transformed ramp bounds differed from modular edges by
roughly 0.000002 units. Subtracting the passage left long, microscopic floor
polygons. Their area passed the old minimum-area test; extrusion still gave them
the full 0.02-unit slab thickness, producing visible floating edges.

Baseline: `StairJunctionInspection.log`, `NARROW_PATCH` entries. The corrected
inspection has none, for all four quarter turns at world offset (474,101,507).

## Changes

- Stair region polygons use the existing millimetre geometry precision. Authored
  prefab transforms and Blender pivots are unchanged.
- Floor generation and legacy patch reconstruction reject numerical sliver
  triangles by minimum altitude as well as area. Empty patches remain empty on
  JSON save/load; they cannot revert to full prefab tiles.
- Upper enclosures pack actual wall arms into Corner, T and + prefabs. Only
  directly touching solid neighbours with compatible base finishes participate.
  Different painted finishes, windows, door holes and glass remain intact.
- Replaced solid arms are credited in the single construction transaction and
  persist as deletion records. Junction packing is local/bounded, not a recursive
  search through the entire connected building.
- Existing completed enclosures are not migrated/rebuilt automatically. New
  staircase construction uses the new packing. Legacy floor patch meshes are
  sanitised when reconstructed (including loading a saved game).

## Verification

Unity 6000.3.8f1 batch run: `StairJunctionValidation.Full`, exit code 0.
See `StairJunctionValidation.log` and `StairJunctionValidation.txt`.

- 47 new checks: four rotations, real world offset, four Corners, T/+ neighbours,
  prices, transaction, saved replacement identities, window/door preservation,
  no microscopic ribbons, legacy empty-patch round trip, centimetre-sized edges.
- 60 stair alignment checks passed.
- Existing room, stair hall, wall joining/families/reconfiguration, mounted
  furniture, prices, previews and foundation regressions passed.

No user scene, prefab pivot, source FBX or save file was modified by the tests.
