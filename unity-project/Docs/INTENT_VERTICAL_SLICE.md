# Intent + spawn vertical slice

Mirror of `3d-monster-gen/docs/INTENT_VERTICAL_SLICE.md` — keep both in sync.

## Backend (`3d-monster-gen`)

- `GET /health`
- `POST /api/v1/intent/parse` — body `{ "text": "...", "locale": "en" }`

Run `python main.py` in `3d-monster-gen/web/backend` (port **8001**).

## Unity setup

1. **AIClient** — `apiBaseUrl` = `http://localhost:8001` (or LAN IP for device builds).
2. **SpawnDirector** — assign player. Under **Catalog (easiest)** add rows: e.g. `creature.wolf.v1` → enemy prefab, `creature.dragon.v1` → `ImportedContent/Creatures/creature.dragon.v1/Dragon.prefab`. The optional ScriptableObject list is only for `.asset` Creature Catalog Entry files, not raw prefabs.
3. **HelloVR:** **IslandWorldManager** already has **SpawnDirector** + **IntentSpawnTest** wired; catalog includes wolf + dragon; **Max Forward Spawn Meters** is set higher for dragon distances.
4. **IntentSpawnTest** — assign **SpawnDirector** (auto-finds if null). In Play Mode use **Test Wolf Spawn**, **I** with Game view focused, or context menu **Test Intent Spawn**. Backend stub: say **“dragon”** or **“wolf”** in the test phrase (`3d-monster-gen/docs/INTENT_VERTICAL_SLICE.md`).
5. If the API fails, see Console for `[IntentSpawnTest] API failed` — run `python main.py` in `3d-monster-gen/web/backend` and set AIClient URL to `http://127.0.0.1:8001`.
6. Increase **Max Forward Spawn Meters** if intent asks for farther spawns than you allow.

## Scripts

- `Assets/Scripts/AI/IntentModels.cs`
- `Assets/Scripts/AI/AIClient.cs` — `ParseIntent`
- `Assets/Scripts/Gameplay/CreatureCatalogEntry.cs`
- `Assets/Scripts/Gameplay/SpawnDirector.cs`
- `Assets/Scripts/AI/IntentSpawnTest.cs`

Schema: `Docs/schemas/intent_response_v1.schema.json`
