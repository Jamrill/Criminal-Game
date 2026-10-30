# Placement contacts and cancellation

- Wall placement now permits the same stair side/rear boundary clearance as stair
  placement (0.5 at local X sides, 0.3 at the rear). Each wall box is checked
  independently; inner branches and flight intersections still block placement.
- Physical stair colliders, prefab pivots and original meshes are unchanged.
- Thin generated slabs may touch wall bottoms or tops (0.06 seam tolerance), not
  cross the middle of a wall. Wall placement also normalises the small slab
  thickness offset to existing storey heights.
- Optional collider reporting is shared by placement, footprint reservations,
  mounted objects, replacements and stair enclosure previews. Blocking objects
  receive the existing red preview material temporarily; original material arrays
  are restored before the next update and on exit/disable.
- Right click cancels the current selection, painting/demolition or queued wall
  stroke before left-release can commit it. Pending ghosts are discarded without
  charging; build mode stays open so another catalogue item can be selected.
  It does not undo previously purchased objects.

Validation: `PlacementFeedbackValidation.Full` in Unity 6000.3.8f1, 48 new checks
plus the stair junction/alignment/clearance, room, wall, mounting, price, preview
and foundation regressions. See `PlacementFeedbackValidation.log` and `.txt`.
Cancellation tests exercise the controller's cancellation method with a real
pending stroke; interactive pointer input should also be checked in normal Play.
No user scenes or prefabs were saved by these tests.
