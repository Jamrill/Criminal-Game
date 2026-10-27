# Options layout and Back repair

The sample sprites' preferred width competed with action labels in each HorizontalLayoutGroup, compressing labels to near-zero width. SettingsRowLayout now gives each row two explicit columns: a white, left-aligned action caption and a right-aligned 300x45 button (responsive width on narrower panels). The existing vertical scroll lists remain; horizontal layout groups on individual rows are disabled. These settings apply to authored control/video rows and runtime-generated controls/reflection rows. Button sample inheritance and state colors remain intact.

OptionsPanelNavigation holds an explicit serialized Back reference. MainMenuUI binds it to CloseOptions; PauseMenuUI binds its spawned panel to CloseOverlay. Legacy persistent Back handlers were cleared in the shared prefab and MainMenu instance. The MainMenu instance had no persistent return callback. Back hides category panels before the owner's animated return.

Play raycast checks also exposed the Controls scroll viewport intercepting Back. Navigation buttons are now brought in front of category panels on opening, without replacing their positions or animation.

The world's HUD used a different scaling context: at 640x480 its Back appeared outside the screen. Pause creates a dedicated runtime Options canvas with Scale With Screen Size, reference 1920x1080, match 0.5, and sorting order one above the pause UI. It is cleaned up with PauseMenuUI and does not alter the world's HUD, inventory or SlotsPanel.

The Accessibility caption now uses LocalizedText: English "Accessibility", Spanish "Accesibilidad". Its existing object name/references are retained.

Backups: Documentation/OptionsMenuRepairBackup. Native layout/localisation checks: OptionsMenuValidation.txt. Real Play checks: OptionsNavigationPlayValidation.txt (19 checks), including UI raycasts, return animations for Controls and Video in both scenes, and Time.timeScale=0 in World City. The Play test disables normal boot with StandalonePreviewScene and never invokes saving or loading a user slot.
