# Construction grid, footprints and alignment

Construction mode now draws a local 4x4-unit grid on the placement surface. Ground guides face up; ceiling guides face down and require an actual BuildFloorCoverage belonging to the plot. Grid segments are sampled against solid surfaces so they do not simply draw an infinite plane across missing floor/ceiling areas. Lines use ordinary depth testing, are offset slightly to avoid z-fighting, cast no shadows and have no colliders.

Selected furniture shows its reserved cells in green or red according to placement validity and affordability. Nearby furniture on the same surface shows its footprint; a brighter white line connects matching sides or centre axes when items share a row/column. Other storeys and floor/ceiling mounting types are excluded. All these graphics are transient: they are not built objects, affect neither economy nor saved state, and disappear on mode exit/tablet/pause. Alt freezes the last preview and guides instead of moving them with the UI pointer.

## Inspector

- On Player / ConstructionController: Show Placement Guides, Guide Grid Radius (nearby cells), Guide Alignment Range (world units).
- In ConstructionCatalog: Footprint Cells is the number of 4x4 cells along the prefab's local X and Z axes. Rotating the object rotates its footprint.
- Chair, ceiling lights and floor lamp: 1x1 cell (4x4 units).
- Sofa, bed and table: 2x1 cells (8x4 units), matching these meshes' long local X axis. Other catalogue furniture was initialized from its existing X/Z dimensions; counters and long shelving also use two cells.

Furniture/ceiling previews snap to the centres of these footprints. Reserved rectangles may touch, but may not overlap on the same support level. Real collider checks still apply. Walls, floor tiles and doors keep their prior placement/mounting rules, especially the 1-unit wall-origin adjustment and topology replacement. Ceiling fixtures additionally verify support at the corners of the full reserved footprint.

Existing scene objects and prefab origins have not been moved. Already placed furniture reserves its footprint around its existing position, even if it predates grid-centred placement. Saved definitions/positions/rotations are sufficient to restore reservations; no SaveData schema migration is needed. Selling/deleting an object immediately releases its cells.

Implementation: BuildFootprint (snapping/reservations), BuildPlacementGuides (one temporary mesh with three materials, cached surface grid) and ConstructionController integration. It reuses the URP Unlit shader already used by previews. No geometry or colliders are added to the scene/prefabs by the setup.

## Verification

ConstructionGuidesValidation checks 23 cases: dimensions, rotation, cell-centre snapping, overlapping/touching reservations, storeys, row/column alignment, ground/ceiling mesh generation, small gaps between modular tiles, double-cell dividing line, mounted fixtures, save/restore, sale release and rotated grid origins. ConstructionMountValidation covers 32 mounting cases. Its former edge test now verifies that aiming near a tile edge snaps to the supported cell centre rather than leaving a partially hanging light. The final run also passed wall families (122), wall reconfiguration (88), economy/value (25), previews and the graph regression against 59 scene walls.

Catalogue backup before assigning footprint sizes: Documentation/ConstructionGuidesBackup/20261021_091043/ConstructionCatalog.asset. Detailed setup and test outputs are alongside this document.
