# Aetheria – Island World Setup (Step by Step)

This guide sets up the essential scene: **Start in weapon room → 2 grips → teleport to outside island** with road, forest, mountains, birds, wind, trees swaying, leaves, animals, and realistic portals.

---

## Overview

| Step | What |
|------|------|
| Part 1 | Create `Assets/GeneratedTextures/`, generate & save AI textures, create materials |
| Part 2 | Add IslandWorldManager, configure island, road, trees, mountains |
| Part 3 | Add PortalVFX to portals |
| Part 4 | Hand locomotion (already on XR Origin) |
| Part 5 | Optional: birds, leaves particle |

---

## Part 1: Assets/GeneratedTextures – Save & Use AI Textures

### Step 1.1 – Create the folder
- **Project** → right-click **Assets** → **Create** → **Folder** → name it `GeneratedTextures`
- Full path: `Assets/GeneratedTextures/`

### Step 1.2 – Generate textures
1. Start server: `python server.py` in `ml-training`
2. Open **HelloVR**, select **VFXTestPlane**
3. Add **VFXPresetGenerator** if missing
4. Press **Play**, then **G** or **1–8** to generate
5. Wait ~1 min (CPU) for texture to appear

### Step 1.3 – Save to Assets
1. While in **Play** mode, after texture appears: **Aetheria** → **Save Last Generated Texture**
2. Texture saves to `Assets/GeneratedTextures/` (e.g. `photorealistic mountain rock texture, detailed, 4k.png`)

### Step 1.4 – Create materials from textures
1. In **Project**, go to `Assets/GeneratedTextures/`
2. Select a texture
3. Right-click → **Aetheria** → **Create Material from Texture**
4. Material is created (e.g. `photorealistic mountain rock texture, detailed, 4k_Mat.mat`)

### Step 1.5 – Use in the world
- Drag materials into **IslandWorldBuilder** slots: Road Material, Grass Material, Tree Foliage Material, Mountain Material
- Or into **DungeonRoomVisualizer** for room pads

---

## Part 2: Island World Scene Setup

### Step 2.1 – Create IslandWorldManager
1. In **Hierarchy**, create empty GameObject: **IslandWorldManager**
2. Add **IslandWorldBuilder**
3. Add **GameStartManager**
4. Add **AmbientWorld**
5. Add **WindZone** (Component → Physics → Wind Zone) – or create child with WindZone

### Step 2.2 – Configure IslandWorldBuilder
| Field | Value |
|-------|-------|
| Island Radius | 40 |
| Road Start Position | (0, 1.2, 0) |
| Road Width | 3 |
| Road Length | 60 |
| Tree Count | 30 |
| Mountain Count | 5 |
| Road Material | Your road texture material |
| Grass Material | Your grass texture material |
| Tree Trunk Material | Brown/gray material |
| Tree Foliage Material | Green material |
| Mountain Material | Your mountain texture material |
| Animal Count | 5 (optional) |

### Step 2.3 – Configure GameStartManager
| Field | Value |
|-------|-------|
| Player | Drag **XR Origin** |
| Weapon Room Start | (0, 1.2, 6) |
| Island Builder | Drag **IslandWorldManager** (same object) |
| Gesture Teleport | Drag **XR Origin**’s GestureTeleport |

### Step 2.4 – Ensure GestureTeleport
- **XR Origin** must have **GestureTeleport**
- 1 grip = weapon room, 2 grips = outside (on road)

### Step 2.5 – Birds (optional)
1. Add bird chirp AudioClips to `Assets/Audio/Birds/` (or any folder)
2. Select **IslandWorldManager**
3. In **AmbientWorld**, set **Bird Clips** size and drag your clips

### Step 2.6 – Leaves particle (optional)
1. Create **Particle System** (GameObject → Effects → Particle System)
2. Name it **Leaves**
3. Configure: Shape = Box, Start Lifetime = 4, Start Speed = 0.5, Gravity Modifier = 0.2
4. Drag into **AmbientWorld** → **Leaves Particle**

---

## Part 3: Realistic Portals

### Step 3.1 – Add PortalVFX to existing portals
1. Find **PortalToWorld** (or your portal to outside)
2. Add **PortalVFX**
3. Add **Point Light** (child of portal) – set color blue/cyan, range 3
4. In **PortalVFX**: assign **Glow Light** = that Point Light
5. Optionally add **Particle System** for swirl – assign to **Swirl Particles**

### Step 3.2 – Portal particle swirl (optional)
1. Create Particle System on portal
2. Shape: Circle, Radius 0.5
3. Start Lifetime: 2, Start Speed: 0.5
4. Renderer: Add material with particle shader
5. Assign to PortalVFX → Swirl Particles

---

## Part 4: Hand Locomotion on Road

- **HandSwingLocomotion** on XR Origin already handles walk/run
- Swing arms naturally to walk, faster to run
- Road is a flat surface – CharacterController will move on it
- Ensure **XR Origin** has **CharacterController** and **HandSwingLocomotion**

---

## Part 5: Quick Checklist

- [ ] IslandWorldManager with IslandWorldBuilder, GameStartManager, AmbientWorld
- [ ] GameStartManager: Player, Island Builder, Gesture Teleport assigned
- [ ] Materials assigned to IslandWorldBuilder (road, grass, trees, mountains)
- [ ] XR Origin: GestureTeleport, HandSwingLocomotion, CharacterController
- [ ] Player starts at weapon room (0, 1.2, 6)
- [ ] 2 grips teleport to road start (0, 1.2, 0)
- [ ] WindZone for wind
- [ ] Bird clips for ambient (optional)
- [ ] PortalVFX on portals (optional)

---

## Part 6: Scene Order

In **Build Settings**, ensure **HelloVR** (or your main scene) is first. The scene should have:
- XR Origin (with GestureTeleport, HandSwingLocomotion)
- WeaponRoom (at 0, 0, 6)
- IslandWorldManager (builds island, sets start, ambient)
- Skybox (Mat_Skybox_Air for outside)
