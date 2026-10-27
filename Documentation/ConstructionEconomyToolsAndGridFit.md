# Editor economy tools and plot-fitted grid

## Economy

- Tools / Criminal Game / Construccion / Costes cero (desarrollo): existing toggle.
- Tools / Criminal Game / Construccion / Restaurar precios reales: explicitly disables free costs and refreshes catalogue prices in an active Play session. Original prices are never zeroed or reconstructed.
- Tools / Criminal Game / Desarrollo / Saldo a 5.000.000 (Play): sets the loaded game's balance to exactly 5,000,000, not an accumulating grant. Enabled only during Play with a valid current game. Uses EconomyService so HUD listeners update. It does not save the game automatically; saving normally will retain the modified balance.

All tool implementations live in Assets/Editor and are absent from exported players. In addition, BuildCatalog.FreeConstruction now checks UNITY_EDITOR only, so even a development player build ignores the serialized developmentFreeConstruction flag. That flag is stored in the project catalogue asset, not in this computer's preferences; changing it can be shared through version control, but cannot enable free construction in an exported build.

The user's current free-cost selection and saved games were not changed by validation. Temporary test services verified raising/lowering the balance to five million, HUD events, free/real price switching, unchanged original prices and real deductions.

## Grid

Grid Origin is still the authored reference for height and orientation and for the existing wall grid. A new BuildPlot setting, Fit Furniture Grid To Plot, derives the largest centred rectangle of complete 4x4 cells inside an aligned rectangular plot. Furniture snapping and the rendered surface grid use the same derived X/Z origin; existing objects and the Grid Origin Transform are not moved. Aiming within the plot near a corner clamps the candidate to the closest complete cell in which its footprint fits. Walls keep their existing one-unit snapping and topology rules.

Measured Solar_1: volume 44.5 x 44.5, authored Grid Origin (474,101,507). The fitted grid starts at (452,101,485), uses 11 x 11 full cells and leaves 0.25 on each side. Previously the grid phase admitted only 10 x 10 complete interior cells. The fitted phase also matches the existing floor tiles. A 44.5-unit side cannot be filled exactly by full 4-unit cells without changing cell size, allowing partial cells or resizing the plot; none of those changes were made.

Fitting is enabled on Solar_1. Non-axis-aligned grid/plot pairs and parcels too small for a complete cell retain the authored-grid fallback. Object overlap, real colliders, ceiling support and property bounds are still checked after snapping; fitting does not permit construction through buildings or outside the parcel.

The scene diff against the backup contains only `fitFurnitureGridToPlot: 1`. All 266 existing placed-object transforms and the Grid Origin Transform were checked unchanged. Backup: Documentation/ConstructionEconomyGridBackup/20261021_113707/10_World_City.unity.

Validation: 21 new economy/grid checks, 23 guide checks, 122 wall-family checks, 88 wall reconfiguration checks, 32 mounting checks, 25 value/economy checks, preview regression and scene wall graph checks. Logs: ConstructionEconomyGridRegressions.log.
