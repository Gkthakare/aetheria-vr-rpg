# Aetheria VR RPG – Project Notes

Copy-paste friendly for OneNote, Notion, or any note app.

---

## Build Commands

### Unity Build (Android / Quest)
1. **File → Build Settings**
2. Platform: **Android** → Switch Platform
3. Scenes In Build: **HelloVR** (or your main scene)
4. **Build** → save as: `C:\Builds\Aetheria\aetheria-vr-sao.apk`

### ADB Install & Run (PowerShell)
```powershell
cd "C:\Builds\Aetheria"
adb devices
adb install -r ".\aetheria-vr-sao.apk"
adb shell am start -n com.Aetheria.AetheriaVR/com.unity3d.player.UnityPlayerActivity
```

### Package Name
`com.Aetheria.AetheriaVR`

---

## Scene Flow

| Location | Description |
|----------|-------------|
| **StartCube** | Player spawns inside. Simple cube on ground. |
| **PortalToWeaponRoom** | Inside StartCube. Teleports to WeaponRoom. |
| **WeaponRoom** | Training area. 360 skybox (Mat_Skybox_Training). |
| **PortalToWorld** | Inside WeaponRoom. Teleports to outside world. |

---

## Hand Gestures

| Gesture | Action |
|---------|--------|
| **Arm swing** | Walk (natural swing) / Run (faster swing) |
| **1 pinch** (one grip) | Teleport to WeaponRoom |
| **2 pinches** (both grips) | Teleport to outside world |
| **Teleport ray** | Aim at ground → release to teleport |

---

## Key Scripts

| Script | Where | Purpose |
|--------|-------|---------|
| **StartRoomCube** | StartCube | Sets player spawn position inside cube |
| **TeleportPortal** | Portal quads | Teleport on trigger enter |
| **WeaponRoomSkyboxSwitcher** | WeaponRoom | Switches skybox when entering/exiting |
| **HandSwingLocomotion** | XR Origin | Walk/run from arm swing |
| **GestureTeleport** | XR Origin | 1 pinch = weapon room, 2 pinches = world |
| **ChunkGenerator** | WorldManager | Procedural terrain (water/sand/grass) |
| **ChunkManager** | WorldManager | Loads/unloads chunks, tile materials |
| **DungeonRoomVisualizer** | WorldManager | Spawns room pads + portals (if enabled) |

---

## Materials (Assets/Art/Materials)

| Material | Use |
|---------|-----|
| Mat_Skybox_Air | Default sky (outside) |
| Mat_Skybox_Training | Weapon room 360 skybox |
| Mat_Ground_Stone | Ground / chunks |
| Mat_Portal_Surface | Portal doors |
| Mat_Shadow_Circle | Magic circle on pads |
| Mat_Sand, Mat_Grass | Sand/grass around pads |

---

## Important Positions

| Object | Default Position |
|--------|------------------|
| WeaponRoom | (0, 0, 6) |
| GestureTeleport weapon target | (0, 1.2, 6) |
| GestureTeleport outside target | (8, 1.2, 8) |

---

## Unity Setup Checklist

- [ ] XR Origin has: CharacterController, Locomotion System, Continuous Move, Teleportation Provider
- [ ] Right Controller has: XR Ray Interactor, XR Interactor Line Visual
- [ ] Ground: Teleportation Area (or removed if only room teleport)
- [ ] WorldManager → Dungeon Room Visualizer: Ground Height = 1, yLift = 0.2
- [ ] WeaponRoom → WeaponRoomSkyboxSwitcher: Skybox In = Mat_Skybox_Training, Skybox Out = Mat_Skybox_Air

---

## Disable Procedural World (Optional)

To show only StartCube + WeaponRoom + Ground:
- WorldManager: **uncheck** Chunk Generator, **uncheck** Dungeon Room Visualizer

---

## Training Room (if using dungeon)

- RoomType.Training always linked from Start
- TrainingRoomManager creates gesture + weapon stations
- Keys 1/2/3 = Pinch/Fist/Open (HandPoseStub), J = weapon skill

---

## File Paths

```
unity-project/
├── Assets/
│   ├── Art/Materials/     # Mat_*
│   ├── Art/Textures/      # AI textures
│   ├── Prefabs/Rooms/     # RoomPad, DoorPortal
│   ├── Scripts/
│   │   ├── VR/            # HandSwingLocomotion, GestureTeleport
│   │   ├── Dungeons/      # TeleportPortal, DungeonRoomVisualizer
│   │   ├── Training/      # Gesture/Weapon stations
│   │   └── ...
│   └── Scenes/HelloVR.unity
└── ProjectSettings/
```

---

## ML + AI

See **ML_AI_ROADMAP.md** for VFX image training, gesture ML, and API server.
- `ml-training/server.py` – run for gesture API
- `APIGestureRecognizer` – Unity script for ML gestures via HTTP

---

## Quick Test (Editor)

1. Play
2. Spawn in StartCube
3. Walk through PortalToWeaponRoom → weapon room (360 skybox)
4. Walk through PortalToWorld → outside
5. Grip once → weapon room; grip twice → outside
