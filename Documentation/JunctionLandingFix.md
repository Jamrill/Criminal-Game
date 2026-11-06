# Wall junctions, facing stair landings and two-sided access guides

## Behaviour

- Wall guide arms are combined into one planar shape. Only the external contour is drawn; overlapping translucent faces and end-cap lines at Corner/T/+ joints are removed.
- Wall snapping considers real segment joints on the same plot/storey. It tests the replacement plan (including the resulting Corner/T/+ prefab), rather than rejecting the overlap against the wall that will be replaced. The local search covers rounding on the placement grid. Unaffordable transactions, duplicates, genuine obstructions and plot boundaries retain their checks.
- Facing stair fronts on the same storey require at least one complete 4-unit cell of separation, even when the stair modules themselves do not intersect.
- Aligned facing stairs whose landing areas touch or overlap (4–8 unit separation) share one upper enclosure. Existing internal partitions and any front support that blocks the new stairs are included in the priced red removal preview. Remaining roof/floors are reused; missing connecting floor and roof are included in the green additions. Both exits remain open.
- `BuildEntry.guideAccessSides` selects Front/Back/Left/Right arrows relative to `Guide Front Yaw`. The catalogue sets Front + Back for Bed, Table and Central_display_unit (display isle). Other furniture retains its previous front arrow. Colors still come from the Player's Construction Controller.

Imported Blender pivots, physical stair colliders and user scenes/saves are unchanged. Already constructed invalid arrangements are not silently moved or deleted.

## Verification

`JunctionLandingValidation.Full`, Unity 6000.3.8f1, completed with exit code 0.

- 132 new checks: corner/T/+ magnetic joins in four rotations, real plot-boundary snap/clamp paths, uniform-alpha union shapes without internal contour segments, catalogue arrow counts and rotations, insufficient facing clearance, shared landings at 4 and 8 units in four rotations, continuous supporting floor and unobstructed upper exits.
- Full previous stair occupancy, adaptation, storey, junction, floor, room, wall conversion, mounting, pricing and preview regressions passed.
- Unity render captures: `CornerGuidePreview.png` and `TwoSidedGuidePreview.png`.
- Detailed log/report: `JunctionLandingValidation.log` / `JunctionLandingValidation.txt`.
