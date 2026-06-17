# islandProto Merge - Avatar / Body Hub + Hands

The `islandProto` project was a fork of this same codebase. Its only genuinely
unique value over this repo was a VR **avatar / Quest Body Hub** system and an
**XR Hands** runtime bridge. Those scripts and their editor wizards have been
merged here (in `Assets/Scripts/VR/` and `Assets/Editor/`). islandProto is now
retired; this is the single canonical Unity project.

## What was merged (already in this repo)

Runtime (`Assets/Scripts/VR/`):
- `AutoHumanoidUpgrader.cs` - runtime RPM/Wolf3D GLB -> Humanoid upgrade, attaches IK
- `RpmAvatarFirstPersonHider.cs` - hides avatar head/hair so the HMD view isn't blocked
- `QuestHandPoseInputDeviceBridge.cs` - fallback hand targets from XR device poses
- `BodyHub/QuestBodyHubDirector.cs` - spawns the selected RPM avatar under the rig (PlayerPrefs)
- `BodyHub/QuestBodyHubGazeSelector.cs` - controller-free, gaze-dwell avatar switching
- `BodyHub/QuestBodyHubGazePedestal.cs` - gaze target marker per avatar slot
- `BodyHub/QuestBodyHubApplySavedAvatar.cs` - applies the saved avatar in non-hub scenes
- `XRHands/XRHandsRuntimeBridge.cs` (+ `Aetheria.VR.XRHands.asmdef`) - bridges
  `com.unity.xr.hands` joints into named transforms the player controller reads

Editor wizards (`Assets/Editor/`):
- `QuestBodyHubSceneWizard.cs` - menu `Aetheria/Quest/Create Quest Body Hub Scene`
- `AddXRHandsBridgeTool.cs`, `AddAutoHumanoidUpgraderTool.cs` - one-click rig wiring
- `ReadyPlayerMeUrpFixer.cs`, `FantasySkyboxUrpFixer.cs` - URP material fixers
- `FirstPersonAvatarSetup.cs`, `HideOwnAvatarHeadTool.cs` - first-person head hiding

These compile under the existing `Aetheria.VR` assembly (identical asmdef in both
projects) plus the self-contained `Aetheria.VR.XRHands` assembly. All required
packages (xr.hands, gltfast, animation.rigging, xr.interaction.toolkit) are
already in `Packages/manifest.json`.

## Finish in the Unity editor (cannot be scripted from outside Unity)

1. Open the project in Unity 2022.3 (optionally upgrade to 2022.3.62f1 for parity
   with islandProto), let it compile, and confirm there are no console errors.
2. Create the avatar-select boot scene: menu **Aetheria > Quest > Create Quest
   Body Hub Scene**. Save it as `Assets/Scenes/QuestBodyHub.unity`.
3. In **File > Build Settings**, add `QuestBodyHub.unity` as scene index 0 and keep
   `HelloVR.unity` as index 1 (boot into avatar pick, then load the world).
4. Open `HelloVR.unity`, select the XR Origin / rig, and run:
   - **Aetheria > VR > Add XR Hands Bridge** (adds `XRHandsRuntimeBridge`)
   - **Aetheria > VR > Add Auto Humanoid Upgrader** (adds `AutoHumanoidUpgrader`)
   - add a `QuestBodyHubApplySavedAvatar` component so the chosen avatar loads.
   (Menu paths follow the `[MenuItem]` names in the copied editor scripts.)
5. Press Play to verify the avatar spawns, the head is hidden in first person, and
   hand tracking drives locomotion.

## Intentionally NOT imported

- `islandProto/Assets/ImportedContent/islandPromo/` (~2.4 GB of redundant v1/v2/v3
  AI mesh variants, individual files over GitHub's 100 MB limit). The island in
  `HelloVR` is already built by `IslandWorldBuilder` using the lighter Mountain
  Terrain + ALP tree + Suimono assets already in this repo. If you want specific
  coastal props later, import individually from the backup zip in
  `C:\Users\intru\_consolidation_backups\`.
- islandProto's `.unity` scenes (`QuestBodyHub`, `CleanVRPreview`) - regenerated via
  the wizard above instead of copied, to avoid cross-project GUID breakage.
- Wholesale overwrite of `VRPlayerController.cs` / `VRFullBodyIkController.cs` with
  islandProto's larger variants - skipped to avoid silently breaking the working
  player rig. Port specific hand-tracking tweaks in-editor if desired (diff against
  the backup zip).
