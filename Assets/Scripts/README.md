# Script organisation

Runtime code lives with its feature: Player, Camera, Inventory, Interaction, Printing, Construction, Environment, UI, Services, Scenes, States, Localization and Core.

Feature-specific development code lives in that feature's `Tools` subfolder. Larger tool groups have `Validation`, `Setup` or `Migrations` subfolders. Every complete tool file is wrapped in `#if UNITY_EDITOR` so it cannot enter a player build. This replaces reliance on a top-level `Assets/Editor` folder. New tools here must use the same guard, including their `using UnityEditor` directives. Existing third-party packages retain their own directory structure.

- Player/Tools: animation setup, foot placement and movement diagnostics.
- Construction/Tools: catalogue setup, prefab migrations, furniture, validation, development pricing and explicit Play-to-scene capture.
- UI/Tools: menu/button setup and UI diagnostics.
- Inventory/Tools: custom item inspector.
- Environment/Tools: lighting, shadow diagnostics and sea scene setup.
- World/Tools: sidewalk migration.
- Core/Tools/Validation: script organisation and player compilation checks.

Printer definitions and visuals are under Printing; main-menu UI is under UI/Main_Menu; reflection management is under Environment/Reflections. Existing namespaces and class names are preserved to avoid unrelated API or serialization changes. Scripts retain their `.meta` GUIDs when moved.

Related static development utilities were combined without changing their class names or menu commands: DevelopmentEconomyTools is in ConstructionValueSetup.cs; WallReconfigurationRegression is in WallReconfigurationValidation.cs. MonoBehaviour and ScriptableObject files remain separate to retain Unity component references.

Placement, dragged-wall and replacement previews use MeshPreview for their shared mesh cloning and cached material assignment. This removes duplicate visual code and per-frame material-array creation when the tint stays the same. File moves alone do not improve frame rate.

The move manifest, validation reports and pre-change backup are under Documentation/ScriptOrganization*. The backup is outside Assets and is not compiled or exported. ScriptOrganizationValidation.Run checks preserved GUIDs, prefab components, previews, menus, the inventory inspector and compilation for Windows without editor tools.
