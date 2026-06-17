# Aetheria VR – Complete Implementation Guide

This document provides a detailed, step-by-step implementation of all AI/ML features: terrain textures, voice/message input, 3D model pool, and anime training roadmap.

---

## Part 1: Project Structure Overview

### 1.1 Key Scenes

| Scene | Location | Purpose |
|-------|----------|---------|
| `HelloVR` | `Assets/Scenes/HelloVR.unity` | Main VR scene: WeaponRoom, Island, Dungeon, Gesture training |
| `ChunkTest` | `Assets/Scenes/ChunkTest.unity` | Infinite chunk world testing |
| `DungeonTest` | `Assets/DungeonTest.unity` | Dungeon room layout testing |

### 1.2 Key GameObjects (HelloVR)

| GameObject | Components | Purpose |
|------------|------------|---------|
| **WorldManager** | ChunkGenerator, ChunkManager, DungeonGenerator, VRVoiceCommands, VoiceIntegrationSystem | Infinite terrain, dungeon graph, voice commands |
| **IslandWorldManager** | IslandWorldBuilder, GameStartManager, AmbientWorld, etc. | Island world, start position, 2-grip teleport |
| **VFXTestPlane** | VFXTextureRequest, VFXPresetGenerator | Texture generation (press G or 1–8) |
| **ModelPoolManager** | ModelPool, Model3DRequest | AI 3D models for chunk decor |
| **XR Origin (XR Rig)** | Player, Camera, Controllers | VR player |

### 1.3 Script Dependencies

```
ChunkManager
  ├── ChunkGenerator (required)
  ├── Player (required)
  ├── Materials (optional)
  ├── ModelPool (optional)
  └── TerrainTextureGenerator (calls SetTerrainTexture)

TerrainTextureGenerator
  ├── ChunkManager (required)
  └── VFXTextureRequest (required)

GenerationInputController
  ├── TerrainTextureGenerator (required)
  └── VoiceProcessor (optional, for voice)

VoiceIntegrationSystem
  ├── ChunkGenerator (optional)
  ├── DungeonGenerator (optional)
  ├── AbilitySystem (optional)
  └── GenerationInputController (optional, auto-finds)

ModelPool
  └── Model3DRequest (required)
```

---

## Part 2: Step-by-Step Implementation

### Step 2.1: Python Environment (One-Time)

1. **Open PowerShell** (or Command Prompt).

2. **Navigate to project root:**
   ```
   cd C:\Users\intru\aetheria-vr-rpg\aetheria-vr-rpg
   ```

3. **Create virtual environment:**
   ```
   python -m venv venv
   ```

4. **Activate virtual environment:**
   ```
   .\venv\Scripts\Activate.ps1
   ```
   If you get an execution policy error:
   ```
   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
   ```

5. **Install dependencies:**
   ```
   cd ml-training
   pip install -r requirements.txt
   pip install trimesh
   ```

6. **Verify installation:**
   ```
   python -c "import diffusers; import trimesh; print('OK')"
   ```

---

### Step 2.2: Start the Python Server

1. **From project root:**
   ```
   cd C:\Users\intru\aetheria-vr-rpg\aetheria-vr-rpg\ml-training
   .\venv\Scripts\Activate.ps1
   python server.py
   ```

2. **Expected output:**
   ```
   Aetheria ML API running at http://localhost:8000
     POST /gesture - hand pose -> gesture
     POST /generate_vfx - prompt -> image (Stable Diffusion)
     POST /generate_3d - prompt -> 3D mesh (Shap-E)
   ```

3. **Keep this terminal open.** Open `http://localhost:8000` in a browser for live logs.

---

### Step 2.3: Unity – Open Scene

1. Open **Unity Hub** → open your project.
2. Open **HelloVR** scene: `Assets/Scenes/HelloVR.unity`.

---

### Step 2.4: Create TerrainTextureGenerator GameObject

1. In **Hierarchy**, right-click → **Create Empty**.
2. Rename to **TerrainTextureGenerator**.
3. Position: (0, 0, 0) in Transform.
4. **Add Component** → search **Terrain Texture Generator** → Add.

5. **Configure Inspector:**
   - **Chunk Manager**: Drag **WorldManager** from Hierarchy (it has ChunkManager).
   - **Texture Request**: Drag **VFXTestPlane** (it has VFXTextureRequest).

   If **Chunk Manager** shows "None":
   - WorldManager is a prefab instance. Find it in Hierarchy (it may be under another parent).
   - If WorldManager is in the scene, drag it. If not, drag the GameObject that has **ChunkManager** component.

   If **VFXTestPlane** is missing:
   - Create empty GameObject **VFXTextureRequester**.
   - Add **VFXTextureRequest** component.
   - Set **Api Url** to `http://localhost:8000/generate_vfx`.
   - Set **Timeout Seconds** to `180`.
   - Assign to TerrainTextureGenerator.

---

### Step 2.5: Create GenerationInputController GameObject

1. In **Hierarchy**, right-click → **Create Empty**.
2. Rename to **GenerationInputController**.
3. **Add Component** → search **Generation Input Controller** → Add.

4. **Configure Inspector:**
   - **Terrain Texture Generator**: Drag **TerrainTextureGenerator**.
   - **Voice Processor**: Drag the GameObject with **VoiceProcessor** (if it exists). Otherwise leave empty.

   If **VoiceProcessor** does not exist:
   - Create empty GameObject **VoiceProcessor**.
   - Add **VoiceProcessor** component.
   - Assign to GenerationInputController.

   **Note:** VoiceIntegrationSystem will auto-find GenerationInputController via `FindObjectOfType` if not assigned.

---

### Step 2.6: Connect VoiceIntegrationSystem to GenerationInputController

1. Select **WorldManager** in Hierarchy.
2. In Inspector, find **Voice Integration System** component.
3. **Generation Input** slot: Drag **GenerationInputController**.
   - If empty, it will auto-find at runtime.

---

### Step 2.7: Configure ChunkManager (WorldManager)

1. Select **WorldManager** in Hierarchy.
2. In Inspector, find **Chunk Manager** component.

3. **Required references:**
   - **Chunk Generator**: Should be auto-linked (same GameObject).
   - **Player**: Drag **XR Origin** → expand → **Camera Offset** → drag **Main Camera** (or the Camera child).  
     Or drag **XR Origin** if ChunkManager accepts Transform.

4. **Optional – AI decor:**
   - **Model Pool**: Drag **ModelPoolManager**.
   - **AI Decor Chance**: e.g. `0.15` (15% of grass/forest tiles get decor).

5. **Optional – materials:**
   - Assign **Water Material**, **Sand Material**, **Grass Material**, etc. if you have them.
   - If empty, ChunkManager uses fallback colors.

---

### Step 2.8: Configure ModelPoolManager

1. Select **ModelPoolManager** in Hierarchy.
2. In Inspector, find **Model Pool** and **Model 3D Request** components.

3. **Model 3D Request:**
   - **Api Url**: `http://localhost:8000/generate_3d`
   - **Timeout Seconds**: `600`

4. **Model Pool:**
   - **Model Request**: Should reference the same GameObject’s Model3DRequest (or leave empty for auto-add).
   - **Tree Prompts**: e.g. `fantasy dead tree stump`, `small rock boulder`.
   - **Pillar Prompts**: e.g. `fantasy dungeon pillar stone`, `medieval torch holder`.
   - **Preload On Start**: Checked.

---

### Step 2.9: Configure VFXTextureRequest (VFXTestPlane)

1. Select **VFXTestPlane** in Hierarchy.
2. In Inspector, find **VFX Texture Request** component.
3. **Api Url**: `http://localhost:8000/generate_vfx`
4. **Timeout Seconds**: `180`

5. **For Quest builds:** Replace `localhost` with your PC’s IP, e.g. `http://192.168.1.12:8000/generate_vfx`.

---

### Step 2.10: Save Scene

1. **File** → **Save** (Ctrl+S).
2. Save the scene.

---

## Part 3: Verification (Testing)

### 3.1 Test Texture Generation (Keyboard)

1. Start the Python server (Step 2.2).
2. In Unity, press **Play**.
3. Press **G** (or **1**) – VFXPresetGenerator generates mountain texture.
4. Check: Console shows `[VFXPreset] Generating: Mountain` and `[VFXPreset] Applied: Mountain`.
5. Server logs at `http://localhost:8000` show `VFX request`.

### 3.2 Test Terrain Texture Generation

1. With **TerrainTextureGenerator** set up:
2. In Play mode, create a small test script or use **DevControls**:
   ```csharp
   FindObjectOfType<TerrainTextureGenerator>().GenerateForTerrain(TileType.Mountain);
   ```
3. Or press **V** to record voice, say "generate mountain" (if VoiceProcessor + transcription are set up).
4. Or call from code:
   ```csharp
   FindObjectOfType<GenerationInputController>().ProcessInput("generate mountain");
   ```

### 3.3 Test Voice Input (Text Simulation)

1. Press **V** to start recording.
2. Release **V** to stop – VoiceIntegrationSystem uses simulated commands.
3. Or press **1** – sends "generate forest world" (may trigger world seed change, not texture).
4. For **texture** generation, call: `ProcessInput("generate mountain")`.

### 3.4 Test 3D Model Pool

1. Ensure server is running.
2. Enter Play mode.
3. Wait ~30–60 seconds per model (first run downloads Shap-E).
4. Console: `[ModelPool] Ready. Trees: X, Pillars: Y`.
5. Move around the infinite world – grass/forest tiles may show AI decor.

---

## Part 4: API URLs for Quest

For Quest builds, Unity cannot use `localhost` (it refers to the headset). Use your PC’s LAN IP:

1. On your PC, run `ipconfig` (Windows) or `ifconfig` (Mac/Linux).
2. Find your IPv4 address (e.g. `192.168.1.12`).
3. In Unity, set:
   - **VFXTextureRequest** Api Url: `http://192.168.1.12:8000/generate_vfx`
   - **Model3DRequest** Api Url: `http://192.168.1.12:8000/generate_3d`
   - **APIGestureRecognizer** Api Url: `http://192.168.1.12:8000/gesture`
4. PC and Quest must be on the same Wi‑Fi.

---

## Part 5: File Reference

### 5.1 New/Modified Files

| File | Type | Purpose |
|------|------|---------|
| `Assets/Scripts/AI/TerrainTextureGenerator.cs` | New | Generates terrain textures via SD, applies to ChunkManager |
| `Assets/Scripts/AI/GenerationInputController.cs` | New | Parses voice/text for "generate X" commands |
| `Assets/Scripts/AI/OBJLoader.cs` | New | Parses OBJ text into Unity Mesh |
| `Assets/Scripts/AI/Model3DRequest.cs` | New | Requests 3D mesh from server |
| `Assets/Scripts/AI/ModelPool.cs` | New | Caches 3D models for chunk decor |
| `Assets/Scripts/ChunkManager.cs` | Modified | SetTerrainTexture, runtimeMaterials, ModelPool integration |
| `Assets/Scripts/VoiceIntegrationSystem.cs` | Modified | GenerationInputController integration |
| `ml-training/model_3d.py` | New | Shap-E text-to-3D, outputs OBJ |
| `ml-training/server.py` | Modified | POST /generate_3d endpoint |
| `ml-training/requirements.txt` | Modified | Added trimesh |

### 5.2 Voice Commands (GenerationInputController)

| Say | Action |
|-----|--------|
| "generate mountain" | Generate mountain texture |
| "generate water" | Generate water texture |
| "generate sand" | Generate sand texture |
| "generate grass" | Generate grass texture |
| "generate forest" | Generate forest texture |
| "generate snow" | Generate snow texture |
| "generate mountain with volcanic rock" | Custom prompt |
| "generate all terrain" | Generate all terrain types |

---

## Part 6: Troubleshooting

| Issue | Solution |
|-------|----------|
| `[TerrainTexture] Missing ChunkManager or VFXTextureRequest` | Assign both in TerrainTextureGenerator Inspector |
| `[ModelPool] Ready. Trees: 0, Pillars: 0` | Server not running, or timeout; check logs |
| ChunkManager shows colored cubes, no textures | Materials not assigned; terrain textures not generated yet |
| Voice not triggering generation | Ensure GenerationInputController is in scene; call `ProcessInput("generate mountain")` from code to test |
| Client disconnected before response sent | 3D: timeout increased to 600s; mesh decimated. Ensure server is running. |
| Pink/magenta objects | Missing material; ModelPool creates default brown material |

---

## Part 7: Anime Training (Optional)

See **ML_ANIME_TRAINING_ROADMAP.md** for:
- Data collection for anime textures
- LoRA fine-tuning
- Using existing anime models (Anything V5, Counterfeit)

---

## Part 8: WorldManager Prefab (Optional)

If you want **TerrainTextureGenerator** and **GenerationInputController** on WorldManager so they appear in every scene that uses it:

1. Open **WorldManager** prefab: `Assets/Scenes/WorldManager.prefab`.
2. Add **TerrainTextureGenerator** component.
3. Add **GenerationInputController** component.
4. Assign references (ChunkManager = self, VFXTextureRequest = find or create child).
5. Add **VFXTextureRequest** as child if not present.
6. **Apply** prefab changes.

**Note:** TerrainTextureGenerator and GenerationInputController can also live as separate scene objects; they use `FindObjectOfType` for missing refs.

---

## Part 9: Quick Setup Checklist

- [ ] Python venv created and activated
- [ ] `pip install -r requirements.txt trimesh` run
- [ ] Server running (`python server.py`)
- [ ] TerrainTextureGenerator GameObject created
- [ ] TerrainTextureGenerator → ChunkManager, VFXTextureRequest assigned
- [ ] GenerationInputController GameObject created
- [ ] GenerationInputController → TerrainTextureGenerator assigned
- [ ] VoiceIntegrationSystem → GenerationInputController (or auto-find)
- [ ] ChunkManager → Player, Model Pool assigned
- [ ] ModelPoolManager → Model3DRequest Api Url set
- [ ] VFXTestPlane → VFXTextureRequest Api Url set
- [ ] Scene saved
