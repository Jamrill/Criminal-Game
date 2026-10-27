# Construction UI: authoring and language

## Layout

`ConstructionController` uses its `embeddedUI` reference in `Assets/Prefabs/Player.prefab`. The nested instance of `Assets/Prefabs/UI/ConstructionUI.prefab` has its own RectTransform overrides. These have been preserved, not applied indiscriminately to the parent prefab. Editing a value on the nested instance overrides that same value on the source prefab, as usual in Unity.

The tablet is currently 1820 x 980 and the construction HUD 500 x 760. Opening/closing Properties no longer changes tablet height to 620/230. Outer layout groups have been removed from TabMenu, BuildHUD and PropertiesPanel while preserving their authored child RectTransforms. You can move and resize the navigation buttons, status text, Catalog viewport and property panel manually in the nested Player UI. The migration intentionally does not redesign the manually positioned property panel.

Only generated lists are automatic: Properties keeps a single list layout; Catalog uses one GridLayoutGroup plus vertical ContentSizeFitter. `BuildCatalogGrid` maintains two columns and adapts card width to the content rect. Change its Row Height and the GridLayoutGroup's Spacing/Padding for card styling; change Catalog's RectTransform for the list's position and visible area. Do not add another parent layout to control these same rectangles.

## Language

The shared OptionsPanel now has a LanguageSettings row with an English/Español dropdown, used in both main-menu and pause options through the shared prefab. English is the default. `GameLanguage` saves the choice under `Game.Language` in PlayerPrefs and broadcasts changes. Tests restore the previous preference and do not leave Spanish selected.

Construction navigation, generated catalogue labels, property messages, building/painting/selling hints and wall-conversion error messages use this setting. Definition IDs, prefab names, save data and user-authored property names are unchanged. Options' video values and control action labels also refresh when language changes. `LocalizedText` supplies explicit English/Spanish Inspector fields for static TMP labels; new interfaces can use it or `GameLanguage.T` for dynamic messages. This is an explicit localization foundation, not a blanket replacement of arbitrary text throughout the game; other legacy interfaces still need to be migrated individually.

## Validation and backup

Native Unity compilation succeeded. `ConstructionUIValidation.txt` records 12 checks covering dimensions, absence of outer layout groups, embedded instance selection, two-column placement, readable names and language preferences. Wall-family (122), wall-conversion (88), mounting (32), values (25) and preview regressions also passed. Two old message assertions were made language-aware without weakening their behavior checks.

Original prefab copies are in `Documentation/ConstructionUIBackup/20261020_114100`. No world/main-menu scene was saved by this migration.
