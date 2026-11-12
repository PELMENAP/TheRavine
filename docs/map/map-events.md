# Map events: fire, later weather
Root: `Assets/Scripts/ObjectSystem/Map/GlobalEvents/`. Assembly: `ObjectSystem`. Namespace: `TheRavine.Generator`. Tests: `Assets/Tests/EditMode/FireMathTests.cs`.

## Entry points

| Type | File | Role |
|---|---|---|
| `GlobalMapEvents` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/GlobalMapEvents.cs` | Owns the map events; created, ticked and disposed by `MapGenerator` (`MapGenerator.Events`) |
| `IMapEvent` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/IMapEvent.cs` | One event: `Step(now, dt)` on the authority, `Render(camera)` everywhere; also `IMapSeason` |
| `MapEventRules` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/MapEventRules.cs` | Rules asset of all events (`fire` is a `FireRules`); `MapGenerator.mapEventRules`, defaults with a warning when unset |
| `FireEvent` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/Fire/FireEvent.cs` | Fire simulation; also a random-tick handler; `FireCell` per burning object |
| `FireMath` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/Fire/FireMath.cs` | Pure formulas: size, burn time, spread reach and attempts, heat |
| `FireView` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/Fire/FireView.cs` | Fire rendering and lights; settings `FireViewSettings` in `MapGenerator.fireView` |
| `SeasonClockSource` | `Assets/Scripts/EntitySystem/AI/Colony/SeasonClockSource.cs` | `IMapSeason` over `SeasonClock`; set by `EntityManager` |

## Data flow

1. `MapGenerator.SetUp` creates `GlobalMapEvents` after `RandomTickService`; it registers `FireEvent` as a tick handler.
2. `MapGenerator.Update` calls `GlobalMapEvents.Tick` after the random ticks: `Step` where `WorldAuthority.IsAuthority`, then `Render`.
3. An object burns only if `ObjectInfo.ObjectCanBurn`. Its fire size is the height of its baked collision shape, else `FireRules.defaultSize`, clamped.
4. A random tick on a burnable object ignites it with `ignitionChance` times the season factor; no fire in winter (`IMapSeason`).
5. Every `stepInterval`: time left drops, a burnt-out object is removed through `WorldCommandBuffer`, the rest try to spread.
6. Spread: `SpreadAttempts(size)` random cells within `SpreadReach(size)` cells, chance falls with distance and follows the wind.
7. A cell of a multi-cell object resolves to its primary cell; the burning key is always the primary cell.
8. Heat: `HeatDamageAt(p)` is `damagePerSecond` scaled by cover inside `HeatRadius(size)`; `HeatBounds` bounds all heat.
9. Agents take heat once per fire step (`EntityManager.ApplyFireHeat`, `StepIndex`, `LastStep`); players in `PlayerEntity` metabolism.
10. `FireView` rebuilds one `GraphicsBuffer` when `FireEvent.Version` changes and draws two mesh-less `Graphics.RenderPrimitives` calls.

## Rendering

- Shaders: `Assets/Resources/Fire/FireSmoke.shader` (scorch, up to 5 puffs), `Assets/Resources/Fire/FireFlame.shader` (glow, up to 6 tongues, up to 10 embers).
- Shared code: `Assets/Shaders/Effects/FireCommon.hlsl`. Instance: position and seed, life, terrain normal, size and total burn time.
- Tongue, ember and puff counts and all sizes grow with the fire size; tall objects get tongues and embers along their height.
- Materials are created from the shaders at runtime unless `FireViewSettings.flameMaterial` / `smokeMaterial` are set; the look is in material properties.
- Up to `FireViewSettings.lights` shadowless point lights on the heaviest fire clusters near the camera.
- The shaders extrapolate time left from `FireEvent.StepTime`, so a fire fades out without a pop. Time is `SimulationClock`: a pause freezes the fire.

## Testing

- `MapGenerator` context menu `Ignite Fire Near Viewer`: ignites up to five burnable objects within `debugIgniteRadius` cells (authority only).
- Existing `ObjectInfo` assets have `ObjectCanBurn` off; `Tools/Ravine/World/Mark Plant Food As Burnable` marks plant, toxic and large food.

## Invariants

- Fire state exists only on the authority; nothing is replicated, remote clients see no fire.
- Every world change made by fire goes through `WorldCommandBuffer`.
- A new event implements `IMapEvent`, is added through `GlobalMapEvents.Add`, keeps its rules in `MapEventRules`, and disposes its native memory.
- The player and agents take the same `HeatDamageAt`.

## Hot paths

`docs/map/HOT_PATHS.md`, section World.

## Open issues

- Fire is not replicated to remote clients.
