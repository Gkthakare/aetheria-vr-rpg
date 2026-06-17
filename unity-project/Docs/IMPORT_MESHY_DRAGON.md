# Import a Meshy / ai-3d-agent model into Aetheria (e.g. `creature.dragon.v1`)

## Why `.glb` looks like “Default Asset”

Unity **2022.3 does not ship a GLB importer**. A `.glb` in the Project window may appear as a generic asset with almost no Inspector until you add **`com.unity.cloud.gltfast`** (already listed in this repo’s `Packages/manifest.json` — let Unity refresh packages, then reimport the `.glb`).

**Fastest path:** use the **`.fbx`** Meshy provides — Unity imports FBX natively as a **Model** with meshes and materials.

## 1. Generate files (on your PC)

From `ai-3d-agent-starter-main`:

```powershell
cd C:\Users\intru\Downloads\ai-3d-agent-starter-main\ai-3d-agent-starter-main
copy .env.example .env
# Edit .env: set MESHY_API_KEY=sk-...
pip install -r requirements.txt
python main.py --prompt "flying dragon for VR island" --wait-download --catalog-id creature.dragon.v1
```

**Expected log when it works:** polling lines `Task ... status=... progress=...%`, then downloads, then `Wrote Unity sidecar -> .../aetheria_unity_import.json`.

If you see **`MESHY_API_KEY is not configured`**, the `.env` file is missing or empty.

## 2. Copy into Unity

1. Open `aetheria_unity_import.json` in the download folder — note **`catalog_id`** and file names (`.fbx`, `.glb`, etc.).
2. In Explorer create (if needed):  
   `unity-project\Assets\ImportedContent\Creatures\creature.dragon.v1\`
3. Copy at least the **`.fbx`** (recommended) or rely on **glTFast** + **`.glb`**, plus thumbnail/JSON if you want them beside the asset.
4. In Unity **Project** window, select the **FBX** (or GLB after glTFast):
   - **Model** tab: adjust **Scale Factor** if the dragon is tiny or huge in Scene (try **0.01** or **1** depending on export; aim for ~2–8 m “character” size in VR).
   - **Rig** tab: **Humanoid** only if you need retargeting; otherwise **Generic** or **No rig** for a flying prop.
5. Drag the model into the **Hierarchy** once to verify size next to the island / XR rig.
6. **Add collider** (Box is safest for VR performance) for gameplay.
7. **Prefab:** drag the configured Hierarchy object → `Assets/ImportedContent/Creatures/creature.dragon.v1/Dragon.prefab`.
8. **Flying loop:** add **`FlyingPatrolSimple`** on the prefab root; set **center** to the island (or an empty at island center). Default radii (~45 m) assume a large sky arena — **shrink `radiusX` / `radiusZ` / `heightAboveCenter`** if your dragon should hug the island.

## 3. SpawnDirector catalog

**HelloVR:** **SpawnDirector** and **IntentSpawnTest** already live on **`IslandWorldManager`**. A **`Dragon.prefab`** (root + `FlyingPatrolSimple` + box collider + nested FBX) ships next to the Meshy files; the scene catalog includes **`creature.dragon.v1`** → that prefab, and **Max Forward Spawn Meters** is raised so dragon distances from the backend are not clamped to ~8 m.

1. If you use another scene: add **SpawnDirector** (see `Docs/INTENT_VERTICAL_SLICE.md`), assign **Player** = XR camera / rig.
2. **Catalog (easiest)** → row with **Catalog Id** = `creature.dragon.v1` and **Prefab** = `Assets/ImportedContent/Creatures/creature.dragon.v1/Dragon.prefab`.
3. On the prefab instance in the scene (or on the prefab asset), set **Flying Patrol Simple → Center** = **`IslandWorldManager`** transform (world position `(-20, 0, -30)` in HelloVR) so the orbit matches the island. If **Center** stays empty, the script uses world origin.

`SpawnDirector` applies **`spawn.scale`** from the intent payload on the **root** transform. Keep the prefab root at **scale 1** and tune the **FBX import scale** so a `scale` of **1** in gameplay looks correct.

**Voice / intent test:** With `3d-monster-gen/web/backend` running, use a phrase containing **“dragon”** (heuristic stub returns `creature.dragon.v1`). Example: `spawn a large dragon`.

## 4. Test

Use **IntentSpawnTest** or your intent API so `creature.dragon.v1` spawns; confirm motion with **FlyingPatrolSimple**.
