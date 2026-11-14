# Quad game tutorial guide

Added November 8, 2026 to the shared `Assets/Prefabs/XR Assistant Rig.prefab` for `QuadProjectAlexanderLau`.

Open **Tutorials > Quad games**. Six cards open offline instructions for the existing scene activities. Previous/Next move through short sections; the last Next becomes **All games**. Back returns to the catalog and then the original Assistant tutorial. The selected game/section is retained during the current session. Reading does not claim gameplay completion or save new progress.

| Game | Exact registry / waypoint destination | Guide coverage |
| --- | --- | --- |
| Archery | Archery Range | Bow/quiver, nocking, drawing/releasing, ring scores, equipment recovery |
| Mammoth Tag | Mammoth Tag | Donut proximity starts pursuit, carrying/evading, capture, pedestal location and no replay promise |
| Block Snap | Block Snap Game | Same-letter assembly, top/bottom connectors, middle-piece carry, six wall slots |
| Parkour | Parkour Entrance | A jump, A/B climb, X vault, checkpoint recovery, Waystone controls and timed tower |
| Scavenger Hunt | Scavenger Hunt Start | First paper, upright reading, holding-hand page buttons, ordered held-page-10 checklist |
| Alma Ring Toss | Alma Mater | Hoop table, grab/throw, one-point scoring targets and replacement hoops |

**Show on map** opens the existing Maps screen and selects the exact authored destination. It does not start guidance automatically; choose **Navigate**. Back returns to the same instruction and releases the route, following existing Maps lifecycle. A missing destination returns an explicit message rather than selecting a different landmark. Routes are to game entrances/approaches, not a parkour traversal or scavenger solution.

The original eight-step Assistant guide, its read markers and first-run Skip/Finish flow remain. The Quad games header entry is hidden during first-run onboarding and reappears when Tutorials is opened normally. No cloud request, microphone activation, camera capture, player movement, mini-game reset or progression callback is added. Existing pointer, focused directional and Back controls handle the new tiles.

## Ownership and maintenance

- `QuadGameGuideCatalog.cs` owns immutable guide content, stable guide IDs and exact landmark keys. Keep instructions synchronized with actual gameplay scripts. It deliberately describes local gameplay rather than generating rules from AI.
- `RayBanFeatureScreen.QuadGames.cs` authors the two guide pages, catalog controls, view transitions and map return context. `EnsureQuadGameGuide()` is additive/idempotent; existing display/input objects and backend references remain in place.
- `RayBanMapBridge.SelectLandmark(string)` selects an exact registry name only while acquired. A missing name returns false without changing the current selection or active route. Successful selection stops old guidance; existing `maps.start` starts the new route.
- The catalog is authored as six two-column cards. If games are added, extend the layout/pagination before adding content beyond those six slots.
- Lincoln landmarks participate in the scavenger hunt; they are not presented as separate mini-games. Mammoth Tag still targets the golden-donut sign. No landmark coordinates or map geometry were changed.

## Content provenance

Current scene components were inspected through Unity MCP and confirmed active: bow/string/socket, mammoth AI, ring scoring/scoreboard, letter snapping, player parkour/climb/timer, and scavenger progression/paper controls.

- Archery: `Archery/NockScript.cs`, `MetaStringInteraction.cs`, `MultiShotBow.cs`, `TargetRing.cs`, `ResetButton.cs`, `QuiverScript.cs`.
- Mammoth: `General Quad/NavMeshMovement.cs`, `Mammoth/MammothEatDonut.cs`, `MammothHitPlayer.cs`, `IsGoldenDonutGrabbed.cs`.
- Blocks: `Snap Game/SnapBlock.cs`, `EndSnap.cs`.
- Parkour: `Nav & HUD/SmoothJump.cs`, `ParkourTimerManager.cs`; `Parkour Game/PlayerClimbController.cs`, `Waystone.cs`, `PlayerParkourRespawn.cs`.
- Scavenger: `SecretTrees/Page No.cs`, `TextHideWithPaper.cs`, `SecretProgression.cs`, and authored `Dialogue/Paper Text` scripts.
- Ring toss: root `Assets/DetectScore.cs`, `Assets/Score.cs`, `Assets/RespawnHoop.cs`, `Assets/torus_working.prefab`.

Paths above are under `Assets/Scripts/` except where explicitly stated. These source facts do not establish fresh end-to-end physical play acceptance of each game.

## Validation

The Editor check in `Assets/Editor/QuadGameGuideChecks.cs` covers guide text and focus paths, exact destination selection, explicit route starts and endpoints, Back behavior, existing onboarding preferences and idle voice/media services. Its November 8 Play Mode run passed all 153 checks, including 20 instruction sections and six routes from the test player position. The November 14 standalone main build contained all six game guides. Its headset acceptance test covered rendering by the mammoth; physical playthroughs of the six games were outside that test.
