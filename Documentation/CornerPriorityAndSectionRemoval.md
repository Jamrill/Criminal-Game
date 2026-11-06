# Corner priority and individual wall sections

## Behaviour

- Solid-wall packing reserves outside right-angle corners before adjacent T or cross junctions can consume their shared arms. Remaining spans are represented by compatible authored prefabs, without changing Blender pivots or introducing duplicate spans.
- This applies to endpoint joins, overlapping multi-arm placements and batched wall strokes/automatic enclosures. Windows, glass and door openings are not consumed merely for touching a solid junction.
- Selling a corner, T or cross selects the aimed arm independently. Only that arm is red; retained arms keep their original finish in the preview. Aim away from the exact joint when two arms would otherwise be equally close.
- Removal uses the existing transactional replacement path. The refund is the catalogue value of the old assembly minus the retained pieces. Deleted source IDs are recorded by the deletion ledger; retained sections become normal saved build records.
- A single-wall stroke can begin by joining an existing wall into a corner. Further spans are previewed only while the complete transaction is valid and affordable. Looking back retracts the pending stroke, right-click cancels, and releasing left-click builds and charges the retained batch once.
- Painted corner-start strokes retain the existing finish. Prefabs with incompatible finishes are not silently merged.

## Verification

Unity 6000.3.8f1 batch entry point: `CornerSectionsValidation.Full`.

The targeted suite passed 346 assertions with real catalogue prefabs in an isolated scene: the illustrated T/corner case in four rotations, direct and batch placement, an explicitly overlapping T, each arm of corners/T/crosses, painted previews and cancellation, deletion ledger entries, net costs, affordable drag extension, retraction and painted strokes.

The same run also executed `WallGridStrokeValidation.Full`, including its existing junction, stair, placement and editing regressions, and exited successfully with code 0. See `CornerSectionsValidation.log` and `CornerSectionsValidation.txt`.

The validation does not edit user scenes or saved games. Existing geometry is repacked locally when an affected construction operation occurs; this is not a whole-save migration.
