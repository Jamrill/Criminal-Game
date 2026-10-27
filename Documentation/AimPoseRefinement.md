# Aim pose refinement

- `PlayerLocomotionAnimation.Aim Blend Duration`: 0.4 seconds for both entering and leaving Pistol Idle, without slowing walking/running transitions.
- `PlayerAimHands` is on the active Animator in Player.prefab. `Hand Separation` defaults to 0.045 world metres per hand. Humanoid arm IK applies only to the Pistol Idle contribution and preserves the animated wrist rotation; the correction fades with the state transition. Set separation or correction weight to zero to disable it.
- The source Pistol Idle FBX and avatar remain unchanged. Other poses have zero hand correction. Right mouse button still toggles aiming.
- Isolated Play validation measures actual wrist separation, not just configured offsets, and verifies entry/exit blends and zero residual idle correction. Reports: AimPoseRefinementValidation.txt and AimPosePlayValidation.log. Existing animation regression checks also run before entering Play.

Prefab backup: `Documentation/AimPoseBackup/20261023_154312`.
