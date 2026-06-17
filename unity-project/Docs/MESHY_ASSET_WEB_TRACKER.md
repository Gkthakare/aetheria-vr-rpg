# Meshy Asset Web Tracker (Aetheria)

Use this to keep your Meshy month productive and visible.

## Where to see created assets on the web

- Open [https://www.meshy.ai/workspace](https://www.meshy.ai/workspace)
- Filter by your project tags (recommended):
  - `aetheria-enemy`
  - `aetheria-road`
  - `aetheria-tree`
  - `aetheria-boss`

## Naming convention (important)

Use these exact names in Meshy so Unity mapping stays predictable:

- `aetheria.enemy.goblin.v1`
- `aetheria.enemy.wolf.v1`
- `aetheria.enemy.voidknight.v1`
- `aetheria.road.brick.segment.v1`
- `aetheria.tree.alpstyle.oak.v1`

## What to export from Meshy

- Prefer **FBX** for easiest Unity import.
- Keep one output folder per asset:
  - `Assets/ImportedContent/Enemies/<asset-id>/`
  - `Assets/ImportedContent/Road/<asset-id>/`
  - `Assets/ImportedContent/Trees/<asset-id>/`

## Where to plug assets in Unity (new gameplay systems)

- `IslandWorldBuilder`:
  - `Road Segment Prefab` -> meshy brick road segment
  - `Tree Prefabs[]` -> ALP-style tree prefabs
  - `Enemy Prefab` -> base enemy model

- `DungeonFloorCombatController` (auto-added at runtime on `IslandWorldManager`):
  - `Enemy Prefab` -> dungeon enemy model

- `SpawnDirector`:
  - add `catalogId` rows to `Catalog Entries` for voice/API spawn mapping

## Existing imported Meshy sample in this repo

- `Assets/ImportedContent/Creatures/creature.dragon.v1/creature_dragon_v1_task.json`
  - contains Meshy-generated output URLs (`glb`, `fbx`, etc.)

If you want, add similar `*_task.json` per new Meshy asset to keep source URLs tracked.
