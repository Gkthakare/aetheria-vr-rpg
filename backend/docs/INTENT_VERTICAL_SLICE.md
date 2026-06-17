# Intent + spawn vertical slice

## Backend (`3d-monster-gen`)

- `GET /health` — health check (Unity `AIClient.CheckHealth`).
- `POST /api/v1/intent/parse` — body `{ "text": "...", "locale": "en" }` — returns `IntentParseResponse` JSON (heuristic stub; mentions **wolf** or **dragon** to trigger spawn).

Run:

```powershell
cd web\backend
python main.py
```

Default port **8001** (matches Unity `AIClient` default).

## Unity (`aetheria-vr-rpg/unity-project`)

**Layout:** `unity-project/Docs/PROJECT_STRUCTURE.md`.

1. Ensure **AIClient** exists in the scene (or rely on `AIClient.Instance` auto-create) and `apiBaseUrl` is `http://localhost:8001` (or your PC LAN IP for Quest).
2. Add **SpawnDirector** to a GameObject; assign **Player** (XR camera or rig root).
3. **Catalog (easiest):** On `SpawnDirector`, find **Catalog (easiest — drag prefabs here)** → add one row per creature. Examples: **Catalog Id** `creature.wolf.v1` → boar/capsule prefab; **Catalog Id** `creature.dragon.v1` → `ImportedContent/Creatures/creature.dragon.v1/Dragon.prefab`. Optional: **Material Override** (URP Lit) for server-driven tinting.
   - Do **not** put prefabs in the optional **Scriptable Catalog** slots unless you created **Creature Catalog Entry** assets (`Assets → Create → Aetheria → Creature Catalog Entry`); those slots only accept `.asset` files, not raw prefabs.
4. Add **IntentSpawnTest** to an active GameObject (required); assign **SpawnDirector**; in Play Mode use the **Test Wolf Spawn** button in the Game view, press **I** with the Game view focused, or right-click the component → **Test Intent Spawn**. If nothing happens, the component is not on a loaded object or the backend is not reachable (check Console for `[IntentSpawnTest] API failed`).
5. Spawns are clamped to **~8m** in front of the player by default so you can see them in small scenes; increase **Max Forward Spawn Meters** on `SpawnDirector` if needed.

## Files

- Schema: `docs/schemas/intent_response_v1.schema.json`
- Unity: `Assets/Scripts/AI/IntentModels.cs`, `AIClient.ParseIntent`, `Gameplay/CreatureCatalogEntry.cs`, `Gameplay/SpawnDirector.cs`, `AI/IntentSpawnTest.cs`
