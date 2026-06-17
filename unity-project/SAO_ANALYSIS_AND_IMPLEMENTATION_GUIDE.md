# Sword Art Online – Analysis & Aetheria VR Implementation Guide

**Purpose:** Build a VR game inspired by SAO using Unity + AI/ML for infinite world generation.  
**Reference:** Sword Art Online anime/manga — Aincrad arc, game mechanics, world structure.

---

# Part 1: SAO Structure Analysis

## 1.1 World Architecture (Aincrad)

| Element | SAO Canon | Aetheria Mapping |
|---------|-----------|------------------|
| **Structure** | 100-floor floating castle | Each "floor" = one dungeon instance |
| **Floors** | Thematic (forest, desert, dungeon, etc.) | `floorIndex` + seed → DungeonGenerator |
| **Safe zones** | Towns, no-PvP areas | RoomType.Start, RoomType.Training |
| **Labyrinths** | Maze-like dungeons between safe zones and boss | DungeonGraph (Hubs, Keys, Corridors) |
| **Boss arena** | One per floor; must defeat to advance | RoomType.Boss |
| **Field areas** | Open world outside towns | ChunkManager infinite terrain |

**SAO Floor Flow:**
```
Town (Safe) → Field (Mobs) → Labyrinth (Dungeon) → Boss Room → Next Floor
```

---

## 1.2 Monsters

| Type | SAO Examples | Behavior | Aetheria Status |
|------|--------------|----------|------------------|
| **Field mobs** | Boars, wolves, goblins | Patrol, agro on sight | Not yet |
| **Dungeon mobs** | Skeletons, dark knights | Guard corridors, telegraphed attacks | Not yet |
| **Mini-bosses** | Stronger variants | Block key paths | RoomType.MiniBoss (room only) |
| **Floor bosses** | Unique per floor | Phases, telegraphs, raid-style | RoomType.Boss (room only) |

**SAO Combat Traits:**
- **Telegraphs:** Wind-up animations before attacks (0.6–1.2s)
- **Hitboxes:** Sword arcs, backstab cones
- **Stagger:** Heavy hits interrupt
- **No RNG crits:** Deterministic damage

---

## 1.3 Animals

| Type | SAO | Aetheria |
|------|-----|----------|
| **Passive** | Boars (until attacked), birds | AnimalWander (capsules) |
| **Hostile** | Wolves, field mobs | Not yet |
| **Ambient** | Birds, insects | AmbientWorld (birds audio) |

---

## 1.4 NPCs

| Type | SAO Role | Aetheria Status |
|------|----------|-----------------|
| **Merchants** | Sell items, repair | Not yet |
| **Quest givers** | Side quests, rewards | Not yet |
| **Town NPCs** | Ambient dialogue, lore | Not yet |
| **System NPC** | Tutorial, announcements | VoiceIntegrationSystem (partial) |

---

## 1.5 Animations & VFX

| Element | SAO Style | Aetheria |
|---------|-----------|----------|
| **Sword skills** | Named skills, glowing trails, particle bursts | AbilitySystem + AbilityVFX |
| **Hit feedback** | Sparks, impact flashes | AbilityVFX (hit sparks) |
| **Movement** | Run, jump, combat stance | HandSwingLocomotion, GestureTeleport |
| **UI** | HP bar, stamina, inventory, skill slots | Not yet |
| **Death** | Shatter effect | Not yet |

---

## 1.6 VR-Specific (SAO Full Dive → Real VR)

| SAO (Fictional) | Real VR (Aetheria) |
|-----------------|---------------------|
| NerveGear full dive | Meta Quest headset |
| Sword swings = real gestures | XR controllers, hand tracking |
| Voice commands | VoiceProcessor, GenerationInputController |
| Immersive world | ChunkManager, IslandWorldBuilder, URP |

---

# Part 2: AI/ML Integration Map

## 2.1 Current AI/ML Systems

| System | API | Purpose |
|--------|-----|---------|
| **Terrain textures** | POST /generate_vfx | Stable Diffusion → grass, mountain, water, etc. |
| **3D models** | POST /generate_3d | Shap-E → trees, rocks, props |
| **Voice** | (planned) POST /transcribe | Whisper → text → generation commands |
| **Gesture** | POST /gesture | Hand pose → pinch/fist/open |

## 2.2 AI/ML for Infinite World

| Use Case | Model | Unity Integration |
|----------|-------|-------------------|
| **Terrain textures** | Stable Diffusion (anime LoRA) | TerrainTextureGenerator |
| **Field decor** | Shap-E | ModelPool → ChunkManager |
| **Dungeon props** | Shap-E | ModelPool (pillar, torch) |
| **Monster variety** | (Future) Shap-E or prefab variants | Enemy spawner |
| **NPC dialogue** | (Future) LLM | AIClient, LLMIntentExtractor |
| **Floor themes** | Prompt templates per floorIndex | TerrainTextureGenerator.GenerateWithPrompt |

---

# Part 3: Step-by-Step Implementation Plan

## Phase A: Foundation (Weeks 1–2)

### Step A1: Floor System Architecture

**Goal:** Treat each "floor" as a theme + dungeon + field.

1. Create `FloorManager.cs`:
   - Holds `currentFloorIndex`, `seed`
   - Calls `DungeonGenerator.SetSeedAndFloor(seed, floorIndex)`
   - Calls `ChunkGenerator.SetSeed(seed + floorIndex * 1000)` for field variation
   - Provides `GetFloorTheme()` → "forest", "desert", "snow", etc.

2. Create `FloorThemeConfig` ScriptableObject:
   - Fields: themeName, terrainPrompts (mountain, grass, water), dungeonPalette (materials)

3. Wire FloorManager to DungeonGenerator and ChunkManager.

---

### Step A2: AI Terrain per Floor Theme

1. In `TerrainTextureGenerator`, add `GenerateForFloor(int floorIndex)`:
   - Map floorIndex to theme (1–10 forest, 11–20 desert, etc.)
   - Use theme-specific prompts: `anime style [theme] mountain texture...`
   - Call `GenerateForTerrain()` for each tile type

2. Trigger on floor load (or voice: "generate floor 5 terrain").

---

### Step A3: Voice → Floor/Theme Commands

1. In `GenerationInputController.ProcessInput()`, add:
   - "go to floor 5" → FloorManager.LoadFloor(5)
   - "generate forest floor" → theme = forest, regenerate
   - "generate desert world" → theme = desert

2. Extend regex: `floor\s+(\d+)`, `generate\s+(\w+)\s+floor`

---

## Phase B: Monsters & Combat (Weeks 3–4)

### Step B1: Enemy Spawner

1. Create `EnemySpawner.cs`:
   - Spawns enemies in rooms by RoomType (TrapRoom, MiniBoss, Boss)
   - Uses DeterministicRng for placement
   - Pool of enemy prefabs (start with simple capsules)

2. Create `EnemyController.cs`:
   - HP, damage, telegraph state machine
   - States: Idle, Telegraph, Attack, Hit, Dead
   - Telegraph duration: 0.6–1.2s

3. Create `EnemyDatabase` ScriptableObject:
   - List of EnemyConfig (prefab, HP, damage, telegraphTime, role)

---

### Step B2: Combat Integration

1. Extend `AbilitySystem`:
   - On skill use, raycast or overlap for hit detection
   - Call `EnemyController.TakeDamage()`
   - Call `AbilityVFX.PlayHitSparks(hitPoint)`

2. Add damage window (e.g. 0.2s after skill trigger).

---

### Step B3: Field Mobs (Optional)

1. In `ChunkManager.CreateTile()` or separate system:
   - Spawn passive/hostile mobs on Grass/Forest tiles
   - Use `VegetationDensityGenerator`-style density for spawn chance
   - Simple AI: wander, agro on player proximity

---

## Phase C: Animals & Ambient (Week 5)

### Step C1: Animal Variety

1. Replace capsule animals with prefabs (boar, bird, etc.)
2. Add `AnimalType` enum: Passive, HostileWhenAttacked
3. `AnimalWander` + optional `AnimalFlee` when player attacks

---

### Step C2: Ambient Life

1. Expand `AmbientWorld`:
   - More bird clips, insect sounds
   - Optional: particle fireflies, dust

---

## Phase D: NPCs (Weeks 6–7)

### Step D1: NPC Spawner

1. Create `NPCSpawner.cs`:
   - Spawns in Start room, Hub rooms
   - Types: Merchant, QuestGiver, Ambient

2. Create `NPCController.cs`:
   - Idle animation, head tracking (look at player)
   - Interaction trigger (player in range)

---

### Step D2: NPC Dialogue (LLM)

1. Add `/npc_dialogue` to `server.py`:
   - Input: NPC id, player message, context
   - Output: LLM-generated response

2. Create `NPCDialogueUI.cs`:
   - World-space canvas, input field
   - Sends to AIClient, displays response

---

## Phase E: AI/ML Enhancements (Ongoing)

### Step E1: Anime Texture Model

1. In `ml-training/image_generator.py`:
   - Use `Linaqruf/anything-v3.0` or add LoRA
   - See `ML_ANIME_TRAINING_ROADMAP.md`

---

### Step E2: Monster 3D Models (Shap-E)

1. Add monster prompts to ModelPool or new `MonsterPool`:
   - "anime style fantasy goblin", "skeleton warrior"
   - Spawn in dungeon rooms

---

### Step E3: Floor-Specific Prompts

1. `FloorThemeConfig` holds prompts per theme
2. On floor load, TerrainTextureGenerator uses those prompts

---

# Part 4: Implementation Order (Prioritized)

## Week 1: Core Loop

| Step | Task | Files |
|------|------|-------|
| 1 | FloorManager + FloorThemeConfig | New |
| 2 | Wire floor → DungeonGenerator, ChunkGenerator | FloorManager |
| 3 | Voice: "floor X", "generate [theme] floor" | GenerationInputController |
| 4 | TerrainTextureGenerator.GenerateForFloor() | TerrainTextureGenerator |

## Week 2: AI Terrain + Polish

| Step | Task | Files |
|------|------|-------|
| 5 | Floor theme prompts in TerrainTextureGenerator | TerrainTextureGenerator |
| 6 | Anime model in image_generator.py | ml-training |
| 7 | Post-processing, fog (SAO_IMPLEMENTATION_STEPS) | Scene |
| 8 | Combat VFX (AbilityVFX, particles) | AbilitySystem, prefabs |

## Week 3–4: Enemies

| Step | Task | Files |
|------|------|-------|
| 9 | EnemySpawner, EnemyController | New |
| 10 | EnemyDatabase ScriptableObject | New |
| 11 | AbilitySystem hit detection → damage | AbilitySystem |
| 12 | Enemy telegraph + attack animation | EnemyController |

## Week 5–6: Animals + NPCs

| Step | Task | Files |
|------|------|-------|
| 13 | Animal prefabs, AnimalFlee | AnimalWander |
| 14 | NPCSpawner, NPCController | New |
| 15 | Head tracking for NPCs | NPCController |
| 16 | (Optional) LLM dialogue endpoint | server.py, AIClient |

## Week 7+: Scale & Polish

| Step | Task |
|------|------|
| 17 | Field mobs in ChunkManager |
| 18 | Boss-specific mechanics |
| 19 | UI: HP, stamina, inventory |
| 20 | Multiple floor themes, more room types |

---

# Part 5: Quick Start Checklist

**Already in Aetheria:**
- [x] DungeonGenerator (room graph, Start→Hubs→Keys→Boss)
- [x] DungeonRoomVisualizer (pads, portals)
- [x] ChunkManager (infinite terrain)
- [x] ChunkGenerator (noise-based tiles)
- [x] TerrainTextureGenerator (AI textures)
- [x] ModelPool (AI 3D decor)
- [x] AbilitySystem (weapon skills)
- [x] AbilityVFX (combat particles)
- [x] VoiceIntegrationSystem, GenerationInputController
- [x] AnimalWander, AmbientWorld

**To Build (in order):**
1. FloorManager + floor themes
2. Voice commands for floor/theme
3. EnemySpawner + EnemyController
4. Combat hit detection
5. NPC spawner + head tracking
6. Animal prefabs
7. UI (HP, stamina)

---

# Part 6: SAO Reference Summary

| SAO Element | Implementation |
|-------------|----------------|
| 100 floors | FloorManager.currentFloorIndex, regenerate per floor |
| Labyrinths | DungeonGenerator graph |
| Boss per floor | RoomType.Boss, EnemyController |
| Field mobs | ChunkManager spawn or separate spawner |
| Sword skills | AbilitySystem + AbilityVFX |
| Safe zones | RoomType.Start, Training |
| NPCs | NPCSpawner, NPCController |
| Animals | AnimalWander, prefabs |
| AI textures | TerrainTextureGenerator + Stable Diffusion |
| AI 3D | ModelPool + Shap-E |
| Voice | VoiceProcessor, GenerationInputController |

---

*Follow phases in order. Each phase builds on the previous. AI/ML runs in parallel with gameplay.*
