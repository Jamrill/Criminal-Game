# Construction gestures, controls and UI language

## Wall gesture

- Hold left click to queue a consecutive wall stroke. Nothing is charged or built until release.
- Moving the aim back retracts the queued end of the stroke, including mesh previews and price. The first span is always retained.
- Crossing the origin starts extending on the other side; old-side spans are removed.
- Cancel Placement (right click by default) discards the entire gesture. Its binding is editable in Options > Controls > Construction. The HUD reads that binding rather than assuming right click.
- Solid walls keep the consolidated corner/T/cross preview. Glass retains individual mesh-only previews.

## Adding controls

Every new input must be documented in `ControlsMenuUI.OrganizeSections` in its appropriate section, with English and Spanish captions. Add editable bindings through `GameInputAction` (append enum values; never renumber), `GameInput.GetActionPath`, and `InputSystem_Actions.inputactions`. Register translations in `ControlRebindButtonUI`. Fixed gestures should be explained in the section's help text.

Normal controls, Construction and Driving are separate sections. Driving currently contains only a localized availability notice; no nonexistent vehicle actions or bindings are advertised.

## Adding menu/tablet content

- Static labels: add `LocalizedText` with both languages in the Inspector, or call `LocalizedText.Bind(text, english, spanish)` when creating the label. It refreshes when the language changes and when re-enabled.
- `LocalizedText.BindMenu` connects the known authored captions on existing menu roots, without changing their layout or button inheritance. Extend its caption table only for static UI captions.
- Dynamic text: use `GameLanguage.T` and refresh on `GameLanguage.Changed` (or the existing view refresh). Construction catalogue titles use `ConstructionText.Title`.
- Never translate user-entered save names, editable input contents, IDs, file paths, or arbitrary object names. Saved names remain unchanged when the locale changes. Save dates use the selected locale.
- New tablet features must supply both translations and refresh existing content, not only use the locale during initial creation.
- Validate English → Spanish → English, opening previously hidden panels, and language persistence. UI tests restore the previous language preference and do not save the user's scene.

## Closed-room shadow seams

The PC pipeline already uses a 100 m shadow distance. The corner fixture reproduced a bright roof/wall seam; raising distance to 150 m worsened several views. A constant contact offset was insufficient when the camera rotated into a coarser shadow cascade.

`WorldWall` now computes the vertical contact correction in shadow-map texels for the current cascade. `Wall_Building` exposes `Wall shadow contact (shadow texels)` (3); the existing 0.05 m minimum and wall-base fade are retained. Correction is capped at 0.5 m and affects shadow sampling only: no mesh/pivot/collider positions, ceiling height, global ambient lighting or shadow-map resolution are changed.

Validation entry points:

- `WallGridStrokeValidation.Full`: construction regressions plus reversible controller queues in four rotations for solid/glass walls.
- `OptionsNavigationPlayValidation.Start`: actual menu/pause navigation and runtime locale/controls/tablet checks in isolated Play (launch without `-quit`).
- `LargeRoomShadowDistanceValidation.CornerSeamsFinal`: 36 corner/sun/camera combinations against sun-off and old-contact baselines, followed by base/exterior and small-room captures.

Verified 2026-11-04: 350 wall-grid/stroke checks and the complete construction regression chain passed; the isolated menu Play run passed 52 checks. The large-room rendered comparison reduced the accumulated direct-sun seam excess from 246.1443 to 0 across all 36 views (same geometry, camera, lighting and 100 m pipeline distance). Measurements and before/after PNGs are in `Documentation/CornerSeamDiagnosis`; menu and construction reports use the validation class names.
