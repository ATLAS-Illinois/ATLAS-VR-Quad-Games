# Quad landmark registry

Updated 2026-10-25 for `Assets/Scenes/QuadProjectAlexanderLau.unity`.

`ATLAS VR > Landmark Registry` edits `Assets/Resources/Map_Settings.asset`. The registry contains 19 destinations: the original three statue entries and 16 additions derived from the current scene. Alma Mater's original pivot-based position was subsequently corrected to a clear ground approach; the other entries retain their positions. `MapConfig` stores names and Unity world positions; these are not latitude/longitude coordinates.

## Original statue entries

| Name | World position (x, y, z) |
| --- | --- |
| Alma Mater | (-19.391598, 0.05, -68.97718) |
| Lincoln Statue Big | (-36.15, 1.77, -149.83) |
| Lincoln Statue Small | (-33.4746, 1.268, -54.7545) |

## Scene-derived additions

Coordinates below are rounded to three decimal places for readability; the asset retains the captured precision. Source paths identify scene hierarchy objects, not filesystem paths.

| Name | Source hierarchy | World position (x, y, z) |
| --- | --- | --- |
| Illini Union | `Quad Models/0023_IlliniUnion` | (-0.034, 0.050, 92.063) |
| English Building | `Quad Models/0044_EnglishBuilding` | (-47.817, 0.050, -62.358) |
| Noyes Laboratory | `Quad Models/0012_NoyesLab` | (58.241, 0.050, 34.372) |
| Henry Administration Building | `Quad Models/0046_Henry+AdministrationBuilding` | (-46.647, 0.050, 31.011) |
| Wohlers Hall | `0159_WholersHall` | (66.351, 0.050, -84.487) |
| Main Library | `0041_MainLibrary` | (60.975, 0.050, -246.982) |
| Colonel Wolf School | `0167_ColonelWolfSchool` | (-36.167, 0.050, -125.636) |
| Foreign Languages Building | `0172_ForeignLanguagesBuilding (2)` | (-336.973, 0.050, -191.170) |
| Archery Range | `Archery Game/FiringPlate` | (8.101, 0.050, -181.397) |
| Mammoth Tag | `Mammoth Tag/Pedestal` | (100.659, 0.110, 86.430) |
| Block Snap Game | `Blocksnap Endstate` | (-43.370, 0.050, -53.856) |
| Parkour Entrance | `Parkour Game/Starting Area/Ladder` | (-41.955, 0.050, 33.050) |
| Scavenger Hunt Start | `New Mystery Assets/Part 1/Secret Table/Cube` | (10.503, 0.050, -202.240) |
| Goose Sculpture | `uploads_files_1860668_goose` | (18.998, 0.050, -7.470) |
| Green Street | `New Mystery Assets/Green St/Green St Sign` | (4.611, 0.100, 216.521) |
| Quad Respawn Point | `Quad Respawn` | (-42.380, 1.274, -83.950) |

## Selection and placement

- Building identity follows the authored model names. Locations follow the virtual scene, not actual campus geography; the Foreign Languages Building is positioned far west in this scene. Do not substitute real-world map coordinates.
- New building destinations use exterior approach points two meters beyond visible bounds. Most activity destinations use a 1.25-meter clearance from fixed scenery; Archery uses its firing plate, and respawn uses its platform. Points were projected onto supporting ground/surfaces and checked for physical clearance.
- World transforms and renderer bounds were inspected instead of trusting imported model pivots. Parent offsets, rotations, and displaced model geometry make raw local coordinates unsuitable.
- Parkour targets its ground-level ladder entrance, not its elevated starting platform. Scavenger Hunt targets the fixed first table, not the movable paper. Mammoth Tag targets the pedestal area, not the roaming mammoth.
- Inactive alternate Noyes/Assembly Hall models, failed texture-research objects, generic props, movable items, and gated scavenger reward objects were excluded. The group named `Observatory` is an elevated reward course, not an independently available campus building.

These original registry checks established destination placement and local clearance only. The subsequent [map and pathway implementation](quad-map-pathways.md) adds a separate, checked waypoint network for all 19 destinations. Route endpoints use separate ground approach points where necessary, so users are guided beside a statue or obstacle rather than into its stored center. Alma Mater now uses the same corrected ground approach in both assets. The current display reports remaining route distance and a heading toward the next route segment; the legacy navigator retains its original straight-line calculation for its hidden UI.

## Alma Mater correction, 2026-10-25

The original registry position (-3.22, 3.98, -77.57) followed the imported parent pivot rather than the visible statue. The initial waypoint endpoint added 3.5 meters along X to that position, retaining the offset from the statue. The corrected registry position and waypoint node 233 are both (-19.391598, 0.05, -68.97718), on supported ground 1.5 meters south of the visible `polySurface17` base bounds. The model, its transforms, gameplay components and interactions were not moved or edited.

The sole destination edge (237) connects node 233 to node 241. The 8.05 m connection was checked for ground support at 0.4 m intervals using a 0.3 m body radius. The other 18 routes retained their graph paths.

## Maintenance and validation

When a landmark moves, update its registry position, waypoint approach, graph connections and overhead image as appropriate. Use identical destination names in both Resources assets. Rebuild the APK to deliver these changes to a headset.

On October 25, Editor Play Mode checks exercised all 19 destinations, selection wraparound, distance, heading and the Navigate/End controls. Post-correction checks passed for Alma's approach and simulated arrival. The broader spatial retest still showed one remaining-distance increase before Mammoth Tag arrival, also reproduced with the older Alma branch. Physical walking to all endpoints was outside that test. See the [map and pathways](quad-map-pathways.md) for route behavior and validation.
