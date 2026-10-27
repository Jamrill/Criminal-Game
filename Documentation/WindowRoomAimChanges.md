# Windows, room reflections and aiming

- The three Window_Wall prefab variants now use `Assets/Materials/Glass.mat`, matching Glass_wall. Their embedded FBX glass was a different material. Frames and wall finishes are unchanged.
- Wall transform changes invalidate room detection. Rebuilding a room updates its reflection bounds and capture origin; an obsolete room volume is removed. Existing zones also update their native ReflectionProbe influence volume, projection and capture position rather than only their editor gizmo. An in-flight capture of obsolete geometry is discarded.
- Right mouse button toggles directly into/out of Pistol Idle with a short blend. While aiming, movement, jumping and crouching are blocked; movement keys do not cancel the pose. Construction mode cancels aiming. Pause preserves it.
- Pistol Draw and Pistol Holster were removed from PlayerLocomotion.controller. Pistol Aim source files remain recoverable but are not used by the controller. No avatar or imported animation curves were regenerated.

Verification: `WindowRoomAimRepair.Apply` completed in Unity batch mode, including 11 targeted checks, 10,807 animation checks and 39 automatic-room construction checks. Reports: WindowRoomAimValidation.txt, MixamoAnimationValidation.txt and RoomConstructionValidation.txt.

Native prefab/controller backups: `Documentation/WindowRoomAimBackup/20261023_153242`.
