# Stair relocation, supports and complete transaction preview

## Behaviour

- New staircase plans adapt obstructing solid wall arms on the affected lower and
  upper levels. Unaffected arms are repacked with their original finish; retained
  objects keep their identity. Windows, door holes and glass are not silently
  removed: incompatible intersections remain blocked with an explicit message.
- Upper perimeter walls lacking full floor coverage receive lower-storey wall
  supports. The current Stairs prefab's Blender-local entrance and exit corridors
  remain free. Existing supporting walls/slabs are reused, not charged twice.
- Placement reserves the authored modular stair depth rather than decorative
  tread overhangs. Physical colliders and prefab/FBX pivots are unchanged. Real
  stair/furniture overlaps remain blocked.
- Validation evaluates the planned removals before testing the new staircase.
  Closed-room information is refreshed before planning, including same-frame sales.
- The controller shows green mesh-only previews for every added wall/floor and
  red for removed wall objects and removed floor geometry. Partial floor cuts
  leave the retained portion uncoloured. Unchanged objects are not tinted.
- Whole removed objects are credited; additions are charged in the same quoted
  transaction. Partial tile recuts preserve the tile identity and do not sell it.
  Cancellation and insufficient funds do not perform partial world mutations.
- Preview meshes are cached by plan, have no colliders or persistent identities,
  and dispose their generated meshes when replaced/destroyed.

These changes run when placing/replacing a staircase. No existing saved scene or
user save file was proactively rebuilt or modified.

## Validation

Unity 6000.3.8f1: `StairAdaptationValidation.Full`, exit code 0.
`Documentation/StairAdaptationValidation.log` / `.txt`: 62 new checks.

- Sell then rotate 90 degrees; move one unit; move four units; adjacent live stairs.
- Required lower supports with free entrance/exit corridors.
- Green additions, collider-free previews, retained materials, no preview/cancel
  mutations, insufficient-funds atomicity and exact quoted payment.
- Usable landing and closed roofed upper room after adapting and after save/load.
- Incomplete lower wall regains its top cap after its covering wall is sold, and
  a new wall can be placed on the freed upper seam.

All existing storey, placement-feedback, junction, alignment, stair-clearance,
room, wall-family/reconfiguration, mounting, value, preview and foundation suites
passed. Older upper-wall-only assertions now explicitly filter the upper level;
lower supports have their own assertions. Front clearance tests use the authored
modular depth instead of decorative mesh bounds.
