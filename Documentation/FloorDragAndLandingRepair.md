# Floor rectangles and required staircase landings

## Player interaction

- Select Floor and hold left click. The initial cell and the aimed cell define opposite corners of a rectangle on the initial storey. This supports single rows, strips of any width and larger rectangles.
- Moving the aim back reduces the selection. Release builds the missing surfaces in one payment; construction cancel/right click discards the preview. Existing floors are neither duplicated nor charged again.
- Existing stair openings are cut out of the selection. A cell only partly occupied by an opening or an existing tile can receive the missing floor fragment, rather than rejecting the entire cell.
- Red previews still reject physical obstructions, outside-plot selections and insufficient funds. Each gesture is limited to 1024 cells, with the geometry calculation's existing safety budget also enforced.
- The construction controls help includes floor rectangles in English and Spanish. No new key binding is introduced.

## Staircase correction

The previous automatic completion treated sold-stair passages and deleted-floor records as permanent exclusions, including when those exclusions crossed a newly required landing. Stair planning now checks the post-cut surfaces and explicitly restores the required upper exit, charging missing catalogue tiles. Other historical holes remain untouched.

Floors already embedded in the updated Stairs prefab count as coverage before building, so no additional overlapping tiles are purchased there. When that prefab is sold, its embedded landing is also a historical hole: automatic room completion does not silently buy it back. Explicit floor placement or another staircase can restore it.

Manual floor aiming uses the existing floor's authored pivot rather than its top face, avoiding cumulative centimetre offsets. Aiming at a stair flight resolves to a storey rather than an arbitrary tread height; active drags keep their initial height.

## Verification

`FloorDragLandingValidation.Full` runs isolated real-prefab tests, then the existing corner, wall-stroke, stair, floor and editing regressions. It does not save user scenes or alter saved games. The tests cover:

- Rectangle dimensions, all drag directions, retraction, price, existing/removed floors, budget and obstructions.
- Actual mesh-only controller previews and the cancellation path.
- Upper landing coverage and physical colliders at all four staircase rotations, including build/sell/rebuild.
- Recognition of the user's integrated upper floor, and a separate legacy-prefab fixture without that floor, with both sold-stair and sold-floor history across its exit.
- Continued protection of active stair holes and authored storey height.

Reports: `FloorDragLandingValidation.txt` and `FloorDragLandingValidation.log`.

The user's Stairs prefab is not modified by this change. Existing missing floors are not automatically migrated on load; the Floor tool can repair them explicitly, or a newly placed staircase completes its required landing.
