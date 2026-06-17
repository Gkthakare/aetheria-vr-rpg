# Island Creation – Step-by-Step Guide

You've saved your AI-generated textures. Follow these steps to build the island world.

---

## Step 1: Create Materials from Your Saved Textures

1. In **Project** window, go to `Assets/GeneratedTextures/`
2. For each texture you want to use (road, grass, mountain, etc.):
   - **Select** the texture
   - **Right-click** → **Aetheria** → **Create Material from Texture**
   - A `.mat` file appears next to the texture
3. **Recommended materials to create:**
   - **Road** – from a dirt/stone texture (or generate: "dirt road texture, photorealistic")
   - **Grass** – from grass texture (you may have from preset 3)
   - **Mountain** – from mountain texture (you may have from preset 1)
   - **Tree foliage** – from forest/green texture (or use a simple green material)
   - **Tree trunk** – brown material (create from "tree bark texture" or use default)

---

## Step 2: Create the IslandWorldManager GameObject

1. Open **HelloVR** scene
2. In **Hierarchy**, right-click → **Create Empty**
3. Rename it to **IslandWorldManager**
4. Set its **Position** to (0, 0, 0) in the Inspector

---

## Step 3: Add IslandWorldBuilder Component

1. Select **IslandWorldManager**
2. In **Inspector**, click **Add Component**
3. Search for **Island World Builder**
4. Add it

---

## Step 4: Assign Materials and Tree Prefab to IslandWorldBuilder

1. With **IslandWorldManager** selected, find **Island World Builder** in the Inspector
2. Expand each section and drag your materials:
   - **Road Material** → your road/dirt material
   - **Grass Material** → your grass material
   - **Tree Trunk Material** → brown material (or leave empty for default gray)
   - **Tree Foliage Material** → green/forest material
   - **Mountain Material** → your mountain material
3. **Realistic trees:** Drag `OakBigTree01_pr` from `Assets/ALP_Assets/Big Oak Tree FREE/Prefabs/` into **Tree Prefab**. Set **Tree Prefab Scale** to 0.12–0.18.
4. Leave **Grass Patch Material** empty to use Grass Material, or assign a different one
5. **Optional:** Set **Animal Count** to 5 (or 0 to disable animals)

---

## Step 5: Add GameStartManager Component

1. Select **IslandWorldManager**
2. **Add Component** → search **Game Start Manager**
3. Add it

---

## Step 6: Configure GameStartManager

1. In **Game Start Manager** section:
   - **Player:** Drag **XR Origin** from the Hierarchy into this slot
   - **Weapon Room Start:** Leave as (0, 1.2, 6)
   - **Island Builder:** Drag **IslandWorldManager** (the same object) into this slot
   - **Gesture Teleport:** Find **XR Origin** in Hierarchy → expand it → find **Camera Offset** → under it, find the object with **Gesture Teleport** component. If GestureTeleport is on XR Origin itself, drag **XR Origin** here. Otherwise drag the GameObject that has GestureTeleport.

**Tip:** GestureTeleport is usually on **XR Origin** or **XR Rig**. Drag that object into **Gesture Teleport**.

---

## Step 7: Add AmbientWorld, WindZone, and ALP Wind Integration

1. Select **IslandWorldManager**
2. **Add Component** → search **Ambient World** → Add
3. **Add Component** → search **Wind Zone** (under Physics) → Add
4. **Add Component** → search **ALP Wind Integration** → Add (for ALP tree wind sway)
5. In **ALP Wind Integration**, assign **Wind Zone** (or leave empty to auto-find)
6. **Wind Zone** settings (optional):
   - Mode: Directional
   - Wind Main: 0.3
   - Wind Turbulence: 0.5
7. **Ambient World** auto-creates falling leaves if **Leaves Particle** is empty. Adjust **Leaves Emit Rate** and **Leaves Area Size** as needed.

---

## Step 8: Verify XR Origin Has Required Components

1. Select **XR Origin** in Hierarchy
2. Ensure it has:
   - **Character Controller**
   - **Hand Swing Locomotion**
   - **Gesture Teleport**
3. If any are missing, add them via **Add Component**

---

## Step 9: Position WeaponRoom (if it exists)

1. Find **WeaponRoom** in the Hierarchy
2. Set its **Position** to (0, 0, 6) so you start inside/near it
3. If you don't have WeaponRoom, the player will still start at (0, 1.2, 6)

---

## Step 10: Save the Scene and Test

1. **File** → **Save** (or Ctrl+S)
2. Press **Play**
3. You should:
   - **Start** at weapon room position (0, 1.2, 6)
   - See the **island** built (ground, road, trees, mountains)
   - **Squeeze both grips** → teleport to the **road** at (0, 1.2, 0)
   - **Swing arms** to walk/run on the road

---

## Step 11 (Optional): Add Birds

1. Download or add bird chirp AudioClips to your project (e.g. `Assets/Audio/Birds/`)
2. Select **IslandWorldManager**
3. In **Ambient World**, set **Bird Clips** size to the number of clips
4. Drag your bird AudioClips into the slots

---

## Step 12 (Optional): Add Falling Leaves

1. **GameObject** → **Effects** → **Particle System**
2. Rename it to **Leaves**
3. Move it to a visible area (e.g. Position 0, 5, 0)
4. In **Particle System**:
   - **Shape:** Box (Size 20, 2, 20)
   - **Start Lifetime:** 4
   - **Start Speed:** 0.5
   - **Gravity Modifier:** 0.2
   - **Start Color:** Brown/green
   - **Renderer:** Material = Default-Particle (or any particle material)
5. Drag **Leaves** into **Ambient World** → **Leaves Particle** slot on IslandWorldManager

---

## Step 13 (Optional): Realistic Portal VFX

1. Find your **PortalToWorld** (or portal that teleports to outside) in the Hierarchy
2. **Add Component** → **Portal VFX**
3. Create a **Point Light** as child of the portal:
   - Right-click portal → **Light** → **Point Light**
   - Color: Blue or Cyan
   - Range: 3
   - Intensity: 1
4. In **Portal VFX**, assign **Glow Light** = that Point Light

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| Island doesn't appear | Check IslandWorldBuilder has no errors. Ensure IslandWorldManager is active. |
| Player doesn't teleport on 2 grips | Verify GestureTeleport is assigned in GameStartManager. Check both controllers work. |
| Player falls through ground | Ensure CharacterController is on XR Origin. Ground has a collider by default. |
| Trees don't sway | TreeSway is added automatically. If not, add it to tree root objects. |
| No materials on island | Assign materials in IslandWorldBuilder. Use created materials from Step 1. |

---

## Quick Reference: What You Need

- **IslandWorldManager** with: IslandWorldBuilder, GameStartManager, AmbientWorld, WindZone
- **Materials:** Road, Grass, Tree Trunk, Tree Foliage, Mountain (from your saved textures)
- **XR Origin** with: GestureTeleport, HandSwingLocomotion, CharacterController
- **GameStartManager** references: Player (XR Origin), Island Builder (IslandWorldManager), Gesture Teleport (XR Origin)
