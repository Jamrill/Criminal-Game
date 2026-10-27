# Stair halls and editable floors

## Current rule (supersedes the earlier removal restrictions below)

Only the foundation floor is protected. `BuildPlot.Foundation Height` specifies its height relative to Grid Origin (default 0). Floors in higher storeys, including closed-room floors, ceilings and stair landings, can be sold/removed. No perimeter, furniture or walkway restrictions apply to them.

Intentional upper-floor deletions and cleared stair openings are retained as plain construction records on Solar, including sold stairs; sold model GameObjects are destroyed. Automatic completion does not fill or charge for those openings, even after save/load. The player may rebuild them manually; an active stair still prevents placing a slab through its passage. Removing a stair does not demolish its hall, walls or roof.

Verified by `FloorFoundationValidation.Run`: foundation protection, editable upper slabs, preserved deleted-floor geometry and sold-stair openings, JSON save/load, and existing construction/UI/animation regressions. See `FloorFoundationValidation.txt` and `.log`.

## Earlier implementation notes

The upper passage now ends at the last tread of the highest ramp, rather than at the rounded visual bounding box. The previous cut could extend one tile past the exit and erase the floor in front of the player.

New stair construction includes the upper enclosure, a tile-wide landing, missing floor and roof in one paid transaction. Reusing an upper room no longer skips missing floors. A complete existing hall is reused without duplicate charges; another staircase opening is never filled. Planned landing coverage is checked in all four orientations, and the constructed hall is tested with a raycast against its actual collider. Existing enclosed halls missing floor can be completed by the normal owned-room system at catalogue prices.

Floor / Suelo is now visible in the Walls construction category. Its 4x4 grid fits the plot and its walkable top aligns to the chosen storey. It can extend a floor and be placed on wall tops; it cannot seal an active stair opening. Normal build prices, sale refunds and saved deletion records apply.

In demolition mode (X), a floor tile is removable only if it does not remove coverage from a closed room's floor/ceiling, a stair landing, or furniture. Open-floor removal also compares polygon-union connectivity and enclosed holes before/after: edge trimming is allowed, new interior holes or disconnected walkways are not. This also handles clipped/diagonal floor pieces. Checks are cached until the construction revision changes and do not clear pending automatic-floor payments.

Nested floor parts of Stairs remain part of the staircase, not independently sellable floor tiles.

Catalogue backup: `Documentation/StairHallBackup/20261023_163346`. Verification reports: `StairHallValidation.txt`, `StairHallRegressions.log`, `RoomConstructionValidation.txt`, and `StairPlacementClearanceValidation.txt`.
