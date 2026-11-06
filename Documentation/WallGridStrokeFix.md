# Wall lattice and mouse-release junctions

## Causes

1. Empty-space wall placement used `BuildPlot.placementStep` (1 unit), while the visible guides and floor/furniture lattice used 4-unit cells with `FurnitureGridOffset`. Magnetic endpoint snapping could align an existing seam, but could not fix every empty-space position. Continuous boundary clamping could also introduce fractional offsets.
2. `ConstructionEconomy.BuildWalls` spawned queued straight pieces directly, bypassing `WallReconfiguration.Placement`. Valid physical endpoint contact did not guarantee a Corner/T/+ prefab was created at mouse release.

## Changes

- `BuildPlacement.ResolveWallPosition` preserves nearby valid existing seams, then otherwise snaps authored wall segment contacts to the exact lattice displayed by the grid. `SnapWallGrid` selects fitting whole-cell positions at plot edges rather than moving the mesh to an arbitrary fractional coordinate. Imported prefab pivots are not recentered.
- `WallReconfiguration.Stroke` creates the consolidated solid-wall plan. The held-button preview, quoted net price and release transaction use that plan. Requested duplicates/collinear overlaps remain invalid. Glass/opening strokes retain their specialized placement rules.
- Direct single-wall purchases also use junction reconfiguration and collision validation, closing another route that could leave separate intersecting pieces.
- Queuing and cancelling remain non-mutating. Replacement identities, deletion ledger, finish checks, ownership, funds, plot boundaries and existing windows/doors remain subject to the existing validators.

Existing authored/off-grid walls are not automatically relocated. Joining them preserves their exact seams; unconnected new walls use the visible grid. No user scene or save was changed by the validation.

## Verification

`WallGridStrokeValidation.Full` completed in Unity 6000.3.8f1 with exit code 0.

- 142 new checks: wall/corner/T/+/large-glass positions on the displayed lattice, four rotations, actual 44.5-unit parcel dimensions with translated/rotated grid, limits, queued-junction preview, one net-price transaction on release, replacement tombstones, no leftover standalone planes, legacy seam preservation, direct purchase consolidation.
- Previous junction/landing, guides, stairs, rooms, wall conversion, mount, price and floor suites passed.
- `ConstructionEditingUpgradeValidation`: 242 checks, including delayed construction, duplicate rejection, insufficient funds and large roof regression tests.

Reports: `WallGridStrokeValidation.txt` and `WallGridStrokeValidation.log`.
