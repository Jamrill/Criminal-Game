# Automatic solid-wall junctions and glass-door border fit

## Endpoint joins

WallReconfiguration.Placement previously considered only walls whose whole segments overlapped the candidate. Touching at an endpoint fell through to ordinary construction, so two perpendicular solid walls remained two flat prefabs.

It now also plans a consolidation of directly touching solid pieces on the same storey. Exact coverage against catalogue templates selects Corner, T or Cross when a compatible prefab can replace the connected arms with fewer objects. The resulting preview, validation, transaction, persistent tombstones and variant refresh use the existing WallChangePlan flow. Value is the resulting prefab cost minus the value of removed pieces, not an additional full charge for both representations.

Only matching whole segments are packed. Collinear runs without a longer matching template remain separate. Unchanged neighbours retain their identity. Windows, doorways and glass are not silently replaced by solid corners, and pieces with incompatible finishes are not recoloured to force a merge. A painted matching neighbour retains its finish. Different storeys and exact duplicates retain the previous rules. Existing saved walls are not globally rewritten on load; this applies when placing a new compatible wall, or using explicit compound replacement.

## Glass-door wall

All three Glass_Wall_door variants had a 0.5-unit main wall but a 0.6-unit fixed internal Glass_door frame. Catalogue bounds correctly included that frame, so a centreline placed 0.25 inside the property boundary protruded by 0.05 and was rejected. The prefab is also 12 units long, unlike the normal 4-unit straight wall: it still needs its full length to fit.

The fixed frame now uses a shared derived mesh, Assets/Meshes/Construction/GlassDoorFrame_WallDepth.asset, fitted only on its depth axis. Its three fixed-frame BoxColliders were fitted to the same thickness and the catalogue bounds recalculated. Vertices, normals and tangents were adjusted consistently; original FBX files were not edited. Root/child transforms, Sheet geometry, handle, hinge, materials and DoorController settings were preserved. The visible prefab's total depth is approximately 0.5 (floating-point mesh bounds differ by a few millionths).

No plot expansion or extra boundary tolerance was introduced. Real out-of-bounds positions and overlapping duplicates still fail. Future replacement/re-export of the fixed frame should retain this fitted depth or explicitly regenerate the derived mesh.

## Verification

WallAutoJoinValidation passed 133 checks: Corner/T/Cross construction from ordinary wall arms in all four orientations, exact span coverage, pricing, save/restore, paint, duplicate/storey/window protections, all three glass-door variants at four plot borders beside existing walls, and genuine outside/duplicate rejection. Existing wall-family (122), wall reconfiguration (88), mounting (32), economy (25), preview and scene graph (69 walls) regressions also passed.

Backups of the three glass-door prefabs and catalogue: Documentation/WallJoinBackup/20261021_121247. The only changes to each backed-up glass-door prefab are the fixed-frame MeshFilter reference and its three fixed-frame BoxColliders. No scene or already placed wall transforms were saved/rewritten by this task.
