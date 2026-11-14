# Quad map and pathways

Updated 2026-10-25 for `Assets/Scenes/QuadProjectAlexanderLau.unity`.

Maps uses an overhead image of the authored virtual scene and an offline waypoint graph. The virtual scene is the coordinate authority; real campus geography and GPS are not used. This is visual guidance only: it does not move the player or change locomotion.

## Assets and presentation

- `Assets/Resources/Quad_WaypointMap.asset` contains the scene path, map bounds, conservative building footprints, 259 nodes, 263 undirected connections and 19 named destinations.
- `Assets/Art/RayBanDisplay/Maps/QuadOverhead.png` is the baked scene image. Its world bounds are X = -450 to 180 and Z = -340 to 250. Tree canopies, display UI and elevated parkour renderers were temporarily hidden for capture and restored afterward.
- `Assets/Resources/Map_Settings.asset` remains the existing landmark registry. Its 19 names and ordering are preserved; Alma Mater's position was subsequently corrected together with its route endpoint. Destination names match the waypoint map exactly.
- `QuadOverheadMapGraphic` renders a north-up view that automatically fits the route or selected destination with the player. The route is cyan, the player is blue and the destination is gold. The existing four Maps controls and Back gesture remain in the feature-screen navigation.

The view is a static image with live overlays, avoiding a continuously rendering map camera. Changes to scene geometry do not automatically update the baked image or waypoint connections.

## Path construction and runtime guidance

The authored graph follows primary walkway centerlines and checked grass/perimeter branches to landmark approaches. All connections use supported ground and body-clearance checks. Conservative building footprints also reject crossings through visible buildings whose proximity scripts may disable their colliders. Existing Mammoth NavMesh data was not changed. NavMesh-proposed detours were accepted only after separate scene-physics checks; runtime routing uses the saved graph.

`QuadWaypointMap` stores the data, `QuadWaypointRouter` computes and tracks guidance, and `RayBanMapBridge` adapts the existing `VRMapNavigator` selection and player references. Dijkstra search chooses a route on the authored graph. This does not guarantee the shortest possible walk through the continuous scene, and the network is not a randomly sampled PRM.

At route start, the router projects the player onto supporting ground and checks candidate connections within 24 meters of graph nodes. These connections require ground support, capsule clearance and building-footprint clearance. If none is usable, Maps asks the user to move closer to a walkway; it does not draw a direct line through obstacles. The active scene must match the map asset's scene path.

Progress advances sequentially along route segments. Maps shows remaining route distance and guides toward the next segment. Arrival requires being within 1.6 meters horizontally of the approach point, within 1.25 meters vertically and within the final 3 meters of route distance. Moving more than 4 meters off the current route, backtracking sufficiently, or jumping more than 12 meters triggers recalculation, throttled to once every 2 seconds. Selecting another destination, ending guidance or leaving Maps clears the route.

Some approach points intentionally differ from registry centers: the Lincoln statues use clear ground beside their bases, the respawn target is adjusted beside its platform, and Illini Union uses (-20, 0, 85.5) to avoid a tree root. Alma Mater's registry and route endpoint now share a clear ground approach beside its visible base. Building targets are exterior approaches, not verified entrances. Parkour ends at its ground-level entrance; it does not guide through the elevated course.

The map, routing and location tracking are entirely local to the Unity scene. They require no phone, room pairing, hotspot, external map service or AI request.

## Validation and limits

October 25 Editor tests passed 19 route progression and lifecycle checks, 25 asset and graph checks, and 12 map display geometry checks. The display started routes to all 19 destinations from the tested player position. The initial spatial test used simulated positions to exercise arrival and End cleanup. Walking every route with a physical headset and testing against moving obstructions are separate checks.

Alma Mater's registry entry and waypoint node 233 were subsequently aligned to the visible statue's ground approach. All 25 graph checks passed after the update. All 18 other routes retained their paths. One Mammoth Tag simulation reached arrival but showed an increase in remaining distance on approach. It behaved the same when the old Alma branch was restored. See the [landmark registry](landmark-registry.md) for coordinates.

The graph assumes the checked static scene geometry. Buildings have exterior approaches, and Parkour ends at its ground-level entrance. Dynamic obstacles and changes to saved edges require separate handling.

## Maintenance

After moving buildings, landmarks, trees, pathways or obstacles, update the affected registry entry if necessary, its waypoint approach, graph connections and building footprints. Re-bake the overhead image using the same world bounds, or update the bounds alongside the image. Re-run asset, progression and spatial checks before testing the changed routes in Play Mode and on Quest.

Preserve the existing Mammoth NavMesh and gameplay components while authoring guidance. Do not interpret a scene-level NavMesh reference or a successful endpoint check as proof that an entire new route is traversable. Rebuild and install the APK to deliver saved map changes to standalone Quest.
