# Automatic rooms and staircase enclosures

`RoomConstruction` is configured on Solar_1 in 10_World_City. New runtime BuildPlots receive this component automatically. The scene's existing interior ReflectionZone supplies its capture settings; its lights, test volumes and materials were not replaced.

## Closed rooms

- WallGeometry spans form a planar graph per floor/ceiling elevation. Intersections are split, so corners, T/cross joints, diagonal walls, separate rooms, internal dead ends and nested outlines are supported.
- A closed outline is checked for continuous floor and ceiling coverage. Existing floor pieces are reused. Missing pieces are generated on the plot's four-unit furniture lattice and clipped to the actual room, including partial edge tiles.
- One catalogue Floor price is charged per generated tile (including partial tiles). The complete floor/ceiling operation for that room is charged before it starts. If unaffordable, the walls remain, no partial flooring is created, and the construction status shows the pending price. It retries when funds or construction change. Only owned, buildable plots are automatically completed.
- Normal demolition, selling and replacement cannot remove floor/ceiling objects. Floors are hidden from the manual construction catalogue. Future painting is separate from structural removal.
- A fixed ReflectionZone is created per completed room beneath the Solar at runtime. Existing reflection quality settings determine capture behaviour. Opening the room removes its zone, not its permanent tiles. Captures are derived again after loading rather than saved as textures.
- Catalogue prices/value remain real; the existing editor free-construction toggle still applies.

## Stairs

Staircase placement is a deliberate exception to floor protection. Its plan cuts the intervening slab, preserves the remaining geometry, and checks the next storey. A suitable closed upper room is reused. Otherwise an upper rectangular enclosure is built using normal four-unit wall modules, a landing floor and a ceiling. A four-unit landing ring around the reserved stair footprint leaves walking space at the exit. The conservative modular footprint can require extra clearance: obstructed/out-of-plot upper enclosures are rejected, not forced through existing objects.

The preview price includes stairs and all additional walls/floor/ceiling tiles. Payment, slab edits and additional construction are one transaction, with rollback on failure. The room scan reserves active stair passages so it does not immediately fill their openings. Selling stairs leaves purchased upper walls/tiles in place; ordinary room completion may subsequently restore an unused opening at normal tile cost.

## Persistence and editor capture

`BuildRecord.floorTriangles` is an optional additive field. Old records continue to load. New clipped tiles retain their identities, catalogue definition, world transform and planar triangles; mesh rendering and static mesh collision are reconstructed by RoomFloorPatch, including in edit mode. Original rectangular tiles keep their existing colliders. Stair edits persist as modified records or deleted-tile tombstones. Generated geometry has hard surface normals and thickness; no new material assets are required.

Explicit Tools > Criminal Game > Construccion > Guardar construccion en la escena still captures these records. Captures/probes are generated at runtime; normal Play tests do not save structural changes to the scene automatically.

## Verification

39 room/staircase checks passed: closure/gaps, intersections, diagonals/concavity, nested rooms, exact coverage across many tiles, costs/insufficient funds, blocked demolition/refunds, collision/ceiling raycasts, save/load, multiple storeys, staircase opening/upper enclosure/reuse. Existing wall joins, wall conversion, mounts, value/save, preview, UI button inheritance, slot height and animation regressions also passed.

Reports: Documentation/RoomConstructionValidation.txt, RoomConstructionInspection.txt and RoomConstructionRegressions.log. Scene/catalogue snapshots: Documentation/RoomConstructionBackup.

This was automated in-memory/editor validation; the visual result and walking through new stair enclosures still need the normal user Play check.
