# Shared button prefabs

Buttons authored in 01_MainMenu and 10_World_City, including their shared UI and printer prefabs, now use nested instances of:

- Assets/Prefabs/UI/Button_sample_small.prefab for RectTransforms of exactly 150 x 45.
- Assets/Prefabs/UI/Button_sample.prefab for other sizes.

Names, RectTransforms, labels, actions and existing object references were preserved and checked during conversion. Appearance and text-state palette inherit from the selected sample; label references and individual layouts remain overrides. Changes to a sample propagate without running any editor migration.

SlotRow and SlotRowCoop are deliberately retained as specialized prefabs with their existing sprites and layout. Their ButtonTextStateColors.styleSource points to Button_sample, so they share its text palette. Unity does not support converting the root of loaded prefab contents to another prefab instance without invalidating its consumers' overrides.

Runtime controls, video dropdown entries and construction/property catalogue buttons instantiate the normal sample via serialized buttonTemplate references on ControlsMenuUI, VideoMenuUI and BuildMenuView. Invisible click blockers are not restyled.

Verification: Documentation/ButtonPrefabValidation.txt (91 checks), plus existing sample state, slot layout and animation validation. Native conversions also checked original object identities, serialized references, labels, active flags and layouts before saving. Original asset and scene snapshots are under Documentation/ButtonPrefabBackup.

ButtonPrefabMigration is an explicit editor-only diagnostic/migration class, with no menu entry or automatic startup hook. It is not required for the game or for further sample edits.
