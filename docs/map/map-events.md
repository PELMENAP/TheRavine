# Map events: fire, wild winds, weather
Root: `Assets/Scripts/ObjectSystem/Map/GlobalEvents/`. Assembly: `ObjectSystem`. Namespace: `TheRavine.Generator`.
Tests: `Assets/Tests/EditMode/FireMathTests.cs`, `Assets/Tests/EditMode/WildWindMathTests.cs`, `Assets/Tests/EditMode/WeatherMathTests.cs`, `Assets/Tests/EditMode/WeatherFieldJobTests.cs`.

## Entry points

| Type | File | Role |
|---|---|---|
| `GlobalMapEvents` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/GlobalMapEvents.cs` | Owns the map events; created, ticked and disposed by `MapGenerator` (`MapGenerator.Events`) |
| `IMapEvent` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/IMapEvent.cs` | One event: `Step(now, dt)` on the authority, `Render(camera)` everywhere; also `IMapSeason` |
| `MapEventRules` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/MapEventRules.cs` | Rules asset of all events (`fire`, `wildWind`, `weather`); `MapGenerator.mapEventRules`; defaults when unset |
| `FireEvent` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/Fire/FireEvent.cs` | Fire simulation; also a random-tick handler; `FireCell` per burning object |
| `FireMath` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/Fire/FireMath.cs` | Pure formulas: size, burn time, spread reach and attempts, heat |
| `FireView` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/Fire/FireView.cs` | Fire rendering and lights; settings `FireViewSettings` in `MapGenerator.fireView` |
| `WildWindEvent` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/Wind/WildWindEvent.cs` | Gusts in an altitude band on the ravine walls; `GlobalMapEvents.Wind`; Burst `WildWindStepJob` |
| `WildWindRules` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/Wind/WildWindRules.cs` | `MapEventRules.wildWind`: band, fade, step, gusts, radius, speed, drift, lost target, reports, damage |
| `WildWindMath` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/Wind/WildWindMath.cs` | Pure formulas: band, fade, falloff, gust damage, wall z, activity, pursuit step, wrap |
| `WildWindView` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/Wind/WildWindView.cs` | One GPU boid flock per gust near the camera; settings `WildWindViewSettings` in `MapGenerator.wildWindView` |
| `WeatherEvent` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/Weather/WeatherEvent.cs` | Storm cells and the weather map; `GlobalMapEvents.Weather`; lightning in `WeatherEvent.Lightning.cs` |
| `WeatherFieldJob` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/Weather/WeatherFieldJob.cs` | Burst job: one texel of the weather map per index |
| `WeatherRules` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/Weather/WeatherRules.cs` | `MapEventRules.weather`: step, map, spawn, cells, ground, pressure and wind, lightning |
| `WeatherMath` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/Weather/WeatherMath.cs` | Pure formulas: falloff, envelope, heat, spawn rate, ground rain, gust front, wetness, strikes |
| `WeatherView` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/Weather/WeatherView.cs` | Map texture, rain, cloud layer, `SkyCover`; settings `WeatherViewSettings` in `MapGenerator.weatherView` |
| `LightningView` | `Assets/Scripts/ObjectSystem/Map/GlobalEvents/Weather/LightningView.cs` | Bolts, `_LightningFlash`, one point light; settings `WeatherViewSettings.lightning` |
| `SkyCover` | `Assets/Scripts/Core/SkyCover.cs` | Static cloud cover and rain over the camera; written by `WeatherView`, read by `DayCycle` and `FogCycle` |
| `SeasonClockSource` | `Assets/Scripts/EntitySystem/AI/Colony/SeasonClockSource.cs` | `IMapSeason` over `SeasonClock`, incl. `WinterApproach`; set by `EntityManager` |

`MapGenerator.mapEventRules` is the rules asset; unset gives defaults and a warning.
## Data flow

1. `MapGenerator.SetUp` creates `GlobalMapEvents` after `RandomTickService`; it registers `FireEvent` as a tick handler.
2. `MapGenerator.Update` calls `GlobalMapEvents.Tick` after the random ticks: `Step` where `WorldAuthority.IsAuthority`, then `Render`.
   With a season source set, `Tick` also publishes the shader globals `_Season` and `_WinterApproach` once per second of `SimulationClock`.
3. `GlobalMapEvents` adds `WeatherEvent` before `FireEvent`, so the fire step reads the weather of the same frame; no `WeatherEvent` when `rules.weather` is null.
3. An object burns only if `ObjectInfo.ObjectCanBurn`. Its fire size is the height of its baked collision shape, else `FireRules.defaultSize`, clamped.
4. A random tick on a burnable object ignites it with `ignitionChance` times the season factor times `1 - WetnessAt`; no fire in winter (`IMapSeason`).
   The code default of `ignitionChance` is 0: lightning is the cause of fire.
5. Every `stepInterval`: time left drops (slower under rain), a burnt-out object is removed through `WorldCommandBuffer`, the rest try to spread.
6. Spread: `SpreadAttempts(size)` random cells within `SpreadReach(size)` cells, chance falls with distance and follows the wind plus `WeatherEvent.WindAt`.
7. A cell of a multi-cell object resolves to its primary cell; the burning key is always the primary cell.
8. Heat: `HeatDamageAt(p)` is `damagePerSecond` scaled by cover inside `HeatRadius(size)`; `HeatBounds` bounds all heat.
9. Agents take heat once per fire step (`EntityManager.ApplyFireHeat`, `StepIndex`, `LastStep`); players in `PlayerEntity` metabolism.
   In the same pass `FindNearestBurning` (`TryFindNearestBurning`) gives an agent within `fireSenseRadius` cells the nearest burning cell,
   and `EntityModel.ReportHazard` turns it into a threat source before any damage.
10. `FireEvent` keeps the burning keys in a dense list in step with the hash (`FireCell.Slot`, swap-back);
    `TryFindNearestBurning` scans it when it holds fewer than (2R+1)² cells, else the square (`FindNearestBurningDense` and `FindNearestBurning` agree, ties included).
11. `FireView` rebuilds one `GraphicsBuffer` when `FireEvent.Version` changes and draws two mesh-less `Graphics.RenderPrimitives` calls.

## Wild winds

1. Created by `GlobalMapEvents` after `FireEvent` (`GlobalMapEvents.Add`); not created when `ravineWallHeight` is 0. State lives on the authority only.
2. `Active`: the simulated square reaches the wall (`WildWindMath.Reaches`: window centre `|z|` plus `(NalReach + 0.5) * chunkSize` ≥ `halfWidth`); callers check it first.
3. On activation the gusts are seeded on both walls at mid band; along x they wrap inside the simulated square.
4. A creature in the band calls `ReportCreature(position)` into a fixed buffer of `reportCapacity`; an overflowing report is dropped.
5. Every `stepInterval`: a gust takes the nearest report as target and pursues it at `gustSpeed`, keeps a lost target for `lostTargetSeconds`,
   otherwise drifts along x with `WindField` and returns to its wall; y stays in the band. The report buffer is cleared after the step.
6. `DamageAt(position)`: zero outside the band; inside, the sum over gusts of `damagePerSecond * Falloff(distance, gustRadius) * BandFade`.
7. Agents: `EntityManager.ApplyWildWind` once per wind step after `ApplyFireHeat`: report, then `DamageAt * LastStep` with `DeathCause.Wind`.
   Players: `PlayerEntity.WindDamage` in the metabolism next to `BurnDamage`: report once per wind step, then `DamageAt * dt` with `DeathCause.Wind`.
8. `WildWindView` reuses `Assets/Shaders/Boids/BoidsStep.compute` and `Assets/Shaders/Boids/BoidsRender.shader` through `BoidFlockGpu`, centred on the gust (`_Center`), time `SimulationClock`.

## Weather

1. `WeatherEvent` keeps up to `maxCells` storm cells as SoA `NativeArray`s, dense with swap-back. State lives on the authority only.
2. Every `stepInterval` of `SimulationClock`: cells drift with the `WindField` mean times `driftFactor` and age; dead or outside cells leave.
3. Then spawn, strikes and `WeatherFieldJob`, in that order.
4. Spawn rate per hour (`WeatherMath.SpawnRate`) = convective part (follows `Heat` of the `DayCycle` phase, zero in winter) plus stratiform part (not tied to the phase).
5. Moisture and charge are drawn independently; only charge is scaled by `ChargeGrowth(heat)`.
   Rain, thunderstorm and dry thunderstorm are not types; they come out of the two numbers and the heat.
6. The weather map: `side * side` texels of `texelSize`, world-anchored and toroidal around `MapGenerator.WindowCenter`. A slot stores its world key and resets wetness when the key changes.
7. Map A: rain at the ground, cloud cover, charge, wetness. Map B: gust-front wind and pressure.
8. `GroundRain` is moisture minus evaporation by heat and dryness: a hot dry cell rains into the air (virga).
9. Wetness is the only accumulated channel (`WetnessStep`: grows with rain, dries faster with heat and wind).
10. Reads are O(1) bilinear: `RainAt`, `WetnessAt`, `WindAt`, `ChargeAt`, `CloudCoverAt`, `PressureAt`.
11. `GustFront` is minus the gradient of the cold-pool high under a charged cell: zero at the centre, outward, strongest at the cell radius.
12. Debug: `MapGenerator` context menu `Spawn Rain Near Viewer`, `Spawn Thunderstorm Near Viewer`, `Spawn Dry Thunderstorm Near Viewer` (authority only, `debugWeatherRadius`).

## Fire under rain

- `FireCell.Wet` grows by `RainAt * step`; the fire goes out (the object stays) when `Wet >= FireMath.QuenchNeed(size)`.
- A small fire needs less than a step of heavy rain; the largest need a long strong rain.
- Burn time stretches under rain (`FireMath.BurnRate`).
- Spread chance is multiplied by `FireMath.RainDamp(rain at the source, wetness at the target)`; a target above `wetIgnitionLimit` does not ignite.
- Spread direction uses `WindSampler.Sample + WeatherEvent.WindAt`: a gust front pushes fire outward from a storm.

## Lightning

1. Per step each cell fires `StrikeRate(charge * envelope)` strikes.
2. A strike takes `candidateSamples` random cells within `strikeRadius` and picks the tallest burnable object by `FireEvent.SizeOf`; no object means the ground.
3. A strike on a burnable object calls `FireEvent.TryIgnite` with chance `ignitionPerStrike * (1 - WetnessAt)`.
4. Below `groundThreshold` a cell only flashes in the cloud (no ground point).
5. All flashes go into a fixed ring (`StrikeCount`, `StrikeAt`); `Ground` marks the ones with a ground point.
6. `LightningDamageAt(p)` and `TryFindNearestStrike` read the ground strikes of the last step; the cause is `DeathCause.Lightning`.
7. Agents: `EntityManager.ApplyLightning` once per weather step after `ApplyWildWind`: `ReportHazard` within `fireSenseRadius` of a strike, then `LightningDamageAt`.
8. Players: `PlayerEntity.LightningDamage` in the metabolism next to `WindDamage`: the same function, the same `StepIndex`-seen pattern.

## Weather rendering

- `WeatherView` uploads the map to one `RGBAHalf` texture `_WeatherMap` and `_WeatherRect` only when `WeatherEvent.Version` changes.
- Globals `_WeatherWind` and `_WeatherShadow`; the shader side is `Assets/Shaders/Effects/WeatherCommon.hlsl`.
- `Assets/Resources/Weather/Rain.shader`: mesh-less drops wrapped in a box around the camera; a drop is kept by the local rain.
- A second material with keyword `RAIN_SHAFT` draws one camera-facing shaft per cell; its bottom is lifted by the evaporation share.
- `Assets/Resources/Weather/CloudLayer.shader` draws three world layers at Y = 1000, 2000 and 4000 by default. Rain shafts, lightning and storm shading use the lowest layer.
- Fair Weather Cover controls background coverage. Cloud Clumping favors broad shapes on the two lower layers. The highest layer uses stretched, warped procedural noise.
- The cloud quads are drawn at the far depth (no depth output), so they show only over sky; the shader samples world coordinates.
- Noise comes from one tileable `R8` texture `_CloudNoise` (`CloudNoise.Create`), shared density code is `Assets/Shaders/Effects/CloudDensity.hlsl`.
- The two lower layers get volume from the sun-side taps: Beer absorption, powder, silver rim, dark thick cores and one view-parallax tap.
- Rain and Shaft use the same shader with separate material settings. A blank Shaft field uses the Rain material as its source.
- In the Editor, changes to the source materials reach their render copies during Play Mode.
- `LightningView` builds each bolt on the CPU (midpoint displacement, 2 to 3 branches, fixed segment budget) into one `GraphicsBuffer`.
- `Assets/Resources/Weather/Lightning.shader` draws the bolt as ribbons with return strokes; one pooled point light follows the strokes.
- `_LightningFlash` is read by the rain, cloud and sky (`Assets/Shaders/Skybox/Skybox.shader`) shaders.
- `CloudShadowMap` bakes the lowest layer density into a small world-anchored `R8` render texture every `cloudShadowInterval` of `SimulationClock`.
- Terrain and grass shaders darken the sun term by one read of it (`WeatherCloudShade`, shifted along the sun to the layer); without it they read the map cloud channel.
- `WeatherView` writes `SkyCover.Value` and `SkyCover.Rain`; `DayCycle.ApplyVisuals` dims the sun, `FogCycle.Apply` thickens the fog.
- Time is `SimulationClock`: a pause freezes rain.

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

- Fire and weather state exist only on the authority; nothing is replicated, remote clients see neither.
- Every world change made by fire goes through `WorldCommandBuffer`.
- A new event implements `IMapEvent`, is added through `GlobalMapEvents.Add`, keeps its rules in `MapEventRules`, and disposes its native memory.
- The player and agents take the same `HeatDamageAt`.
- The player and agents take the same `DamageAt` and report through the same `ReportCreature`; nothing in either tells a player from an agent.
- The player and agents take the same `LightningDamageAt`; nothing in either tells a player from an agent.
- The simulation never reads `SkyCover`; it reads the map through the `WeatherEvent` API.
- Weather state changes only in `WeatherEvent.Step`; `Spawn` is the only entry for a new cell.

## Hot paths

`docs/map/HOT_PATHS.md`, section World.

## Open issues

- Fire is not replicated to remote clients.
