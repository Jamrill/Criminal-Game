# Automatic roof freeze correction

## Failure

Clipping a room triangle against the modular floor grid could produce repeated vertices. Fan triangulation then emitted collapsed triangles. The polygon subtraction routine accepted these as cutters; a zero-length edge copied a remainder into both halves instead of removing any area. This can duplicate fragments and inflate CPU/memory use. A bounded isolated reproduction showed exponential growth on repeated collapsed cutters; a 44x44 room generated 40 zero-area triangles with the old clipping/fan path.

The original frozen Unity session was not stack-sampled, so this is a reproduced implementation defect consistent with the reported freeze, not a captured live call stack.

## Changes

- Clean consecutive/closing duplicate vertices in clipped polygons; ignore collapsed cutters and normalise winding.
- Reject zero-area output triangles during tile planning.
- Clean legacy saved RoomFloorPatch triangles before generating meshes/colliders and exclude collapsed planar pieces from coverage checks.
- Bound polygon subtraction by work, fragment count and elapsed time; cap automatic tile plans. Oversized/invalid calculations produce a localised warning instead of unbounded allocation. Stair planning reports a normal placement error; uncertain wall-top coverage retains its cap.
- Floor/roof plans are computed before payment. Safety rejection does not charge or create a partially generated room surface.

## Verification

`LargeRoofValidation.Regressions` passed:

- Collapsed and duplicate-vertex cutters, cutter winding and fragment-limit rejection.
- Last-wall closure of an 11x11-tile plot containing another closed room: 121 roof tiles, 1936 square units, no collapsed triangles, catalogue pricing charged once.
- Repeated completion and wall-top coverage checks without duplication.
- Legacy saved triangle cleanup and oversized-room warning without spending.
- Temporary perimeter closure in the actual 10_World_City scene: five complete rooms, no warning. The source scene was NOT saved.
- Existing placement/UI, sold-stair/save-load, foundation, wall-joining, construction economy, button and animation regressions.

See `LargeRoofValidation.txt`, `LargeRoofActualScene.txt` and `LargeRoofValidation.log` for timings and detailed results.
