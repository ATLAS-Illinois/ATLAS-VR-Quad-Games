# ATLAS VR Quad Games for Unity 6.3

Open this folder in Unity **6000.3.15f1** to explore the virtual Illinois Quad, play the campus games, and use the XR Assistant. The [original Unity 2022.3.18f1 project](../ATLASVRQuadGames/) is maintained separately.

## Open and run

1. Install Unity **6000.3.15f1** using Unity Hub. For standalone Quest builds, include Android Build Support, Android SDK & NDK Tools, and OpenJDK.
2. Add `source/app/ATLASVRQuadGames_Unity6_3_AI` in Unity Hub. Let Unity restore packages and import assets, then open [QuadProjectAlexanderLau.unity](Assets/Scenes/QuadProjectAlexanderLau.unity). This is the integrated campus scene.
3. For a standalone build, open **File > Build Profiles**, switch to **Android**, and explicitly include and enable **QuadProjectAlexanderLau** in the scene list for the build. The checked-in shared list contains a disabled reference to the old `QuadProject.unity` scene.
4. Connect the Quest by USB with Developer Mode enabled and authorize USB debugging in the headset. Under **Run Device**, refresh, select the Quest, and choose **Build and Run**. The app appears as **HelloWorldVR** under **Unknown Sources**. For an Editor preview, use Quest Link and enter Play Mode in the campus scene.

## XR Assistant and campus games

- The [XR Assistant display](.agent-docs/xr-assistant-display.md) includes Quick Settings, a scrolling launcher, Assistant, Captions, Camera, Photos, and Music. The Assistant camera uses the virtual campus view.
- [Maps](.agent-docs/quad-map-pathways.md) shows the player, destination, waypoint route, and remaining distance on an overhead view of the scene. Its [19 landmarks](.agent-docs/landmark-registry.md) and routes work offline.
- **Tutorials > Quad games** explains Archery, Mammoth Tag, Block Snap, Parkour, Scavenger Hunt, and Alma Ring Toss. Each guide has a **Show on map** shortcut to its starting point. See the [game guide](.agent-docs/quad-game-guide.md) for controls and route destinations.

## Local service setup

For cloud features, open **ATLAS VR > API Setup** and enter your own credentials for the services you use. Unity creates a local `Assets/Resources/API_Secrets.asset`. Its contents and metadata are excluded from Git. For Wit voice recognition, configure your own Wit application in the Meta Voice tooling. The published `xrassistant.asset` template has an empty `_clientAccessToken`. Keep populated tokens and the editor's `ProjectSettings/wit.config` out of commits. Unity packages Resources assets into player builds, so treat locally configured builds as private development artifacts.

## Standalone validation

On November 14, 2026, the locally configured main campus built with **0 errors and 14 warnings** and ran on a **standalone Quest 3**. From the golden-donut sign, the tester faced the mammoth and tilted their head up. The campus and sign stayed visible, and the jagged lines were gone. The build used Vulkan, Oculus Multiview, and **Optimize Buffer Discards** off. The Mammoth Standee's decorative wireframe renderer was disabled while the gameplay object remained active.

This headset check covered the reported rendering fault. The [display integration record](.agent-docs/xr-assistant-display.md) identifies the separate feature checks and their test environments. The [September upgrade diagnostics](Diagnostics/README.md) and [repository split checks](Diagnostics/Repository-split-2026-09-18.md) document earlier versions.

## Development

Read [AGENTS.md](AGENTS.md) for scene ownership, hardware configuration, and validation guidance. This project uses the Built-in Render Pipeline, Linear color space, and the Oculus loader. The [package manifest](Packages/manifest.json) and [lockfile](Packages/packages-lock.json) specify Unity AI Assistant, Meta Unity MCP, and XR dependencies. Compatible development clients can also use the project skills in `.agents/skills` and `.codex/skills`.

Keep Unity caches, IDE outputs, builds, and local service credentials out of commits. Make Unity 6.3 changes in this project folder and keep legacy backports separate.
