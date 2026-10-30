# Stair occupancy and placement guides

## Corrections

- Stair-to-stair occupancy uses the authored modular X/Z footprint and storey interval, independently of the 0.5-unit wall clearance. Shared boundaries remain allowed; positive overlaps are rejected before planning floor cuts or wall replacements, and checked again before purchase.
- Existing staircase landings are not treated as removable ceiling slabs by the generic collision check.
- Plot clamping uses the modular stair front rather than the decorative tread overhang. Nearby wall seams can align the module without moving the imported model relative to its pivot.
- No FBX, authored prefab origins, user scenes or save files were changed. Already overlapping stairs are not automatically deleted or relocated.

## Inspector controls

On Player > Construction Controller:

- Construction preview: Valid Preview Color, Invalid Preview Color, Preview Opacity.
- Placement guides > Guide Colors: grid, valid/invalid outline, base fill, front arrow, alignment lines. Guide colors include independent alpha.
- Guide Grid Radius and Guide Alignment Range retain their previous meaning.

On each plot > BuildPlot: Boundary Color, Boundary Width and Boundary Height.

On ConstructionCatalog > Entries: Guide Front Yaw calibrates each furniture arrow relative to the imported model (0 = +Z, 90 = +X, 180 = -Z). It never rotates the mesh. Chair, sofa and shelving use their imported +Z front by default.

Guides use one transient mesh and five materials, with no colliders or shadows. Furniture retains its 4x4/8x4 reservation; stairs show their 12x12 module. Corner/T/+ wall guides follow their arms. Ceiling furniture shows its base on the ceiling underside.

## Verification

- `StairOccupancyValidation.Full`: Unity 6000.3.8f1, batch exit code 0 (`StairOccupancyFull.log`).
- Baseline reproduced lateral overlap at 11.8 units; corrected tests cover duplicates, facing partial overlaps in all four rotations, crossed stairs, shared boundaries, independent storeys, blocker highlighting and preview immutability.
- 53 guide checks cover all visible catalogue items, filled bases, arrow rotation, editable colors, ceiling mounting and alignment. `ConstructionGuidesPreview.png` is an actual Unity render of the fixture.
- Existing adaptation (62), storey (44), placement feedback (48), junction (47), alignment (64), room, floor, wall conversion, mounting, pricing and preview suites passed.
- Older hall-reuse tests now sell the existing staircase before attempting to reuse its enclosure; attempting to place a second live staircase in the same space is intentionally invalid.
