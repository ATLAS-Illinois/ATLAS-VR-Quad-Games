# ATLAS VR Quad Games - Unity 6.3 AI Development

This is the **Unity 6000.3.15f1** copy of ATLAS VR Quad Games for AI-assisted development. The [original Unity 2022.3.18f1 project](../ATLASVRQuadGames/) remains alongside it without tracked changes.

## Open the project

1. Install Unity **6000.3.15f1** through Unity Hub. Install Android Build Support with its SDK, NDK and OpenJDK if you will build for Quest.
2. Add **this folder**, `source/app/ATLASVRQuadGames_Unity6_3_AI`, to Unity Hub. Open it with 6000.3.15f1 and allow package restoration and asset import to finish.
3. Open `Assets/Scenes/QuadProjectAlexanderLau.unity`, the user-designated main integrated scene. The existing build list still points at a disabled missing scene; select AlexanderLau explicitly when preparing a future build.
4. For editor headset testing, connect Quest Link and run the scene. Quest Link testing does not establish Android build compatibility or standalone device performance.
5. For cloud assistant and translation features, use **ATLAS VR > API Setup** to provide your own service settings locally. `Assets/Resources/API_Secrets.asset` and its `.meta` are intentionally excluded from Git and from this published copy. Configure your own Wit application/client authentication using the Meta Voice tooling. The referenced `xrassistant.asset` is retained as a template with `_clientAccessToken` blank; `ProjectSettings/wit.config` is excluded because it contains editor/server authentication. Runtime token edits in the tracked template must stay out of commits; a `.gitignore` rule does not hide changes to tracked files. Never commit service secrets.

## AI development setup

- The package manifest includes Unity AI Assistant **2.19.0-pre.2** and the Meta Unity MCP extension. Package availability, sign-in, licenses and external AI-client setup are separate from this source snapshot.
- Read [AGENTS.md](AGENTS.md) before making changes. It records the real renderer, rig, gameplay ownership and validation constraints; generic new-project tutorial assumptions do not override it.
- Project skills are included under `.agents/skills` and `.codex/skills` for compatible clients/importers. These are instruction files; they do not install or authenticate an external Codex/Meta plugin. Connect the AI client to this project and verify the connected project path before using editor tools.
- Keep the existing Built-in Render Pipeline, Linear color space, Oculus loader and OVR rig. Meta Core/Interaction remain 62.0.0. This split does not migrate the renderer, XR backend or SDKs.

## Validation status and known limitations

The copied upgrade was inspected in Unity 6000.3.15f1 on September 5, 2026. See [validation history](Diagnostics/README.md). Those gameplay checks were performed in the original local checkout before this folder split. [September 18 split validation](Diagnostics/Repository-split-2026-09-18.md) additionally generated the project script assemblies under both matching editor versions with zero recorded C# errors; full asset import and gameplay were not completed in that batch check.

- The user confirmed conversational assistant, English-to-Spanish translation, photo/gallery/map, mammoth pursuit/capture, basic parkour and archery firing through Quest Link.
- Automated positioning/handler checks exercised logo assembly/wall placement, ring scoring and archery scoring/reset. They do not replace controller-driven end-to-end tests.
- Severe tree-shade darkness is the first gameplay retest priority: the inspected ambient probe was entirely black. The cause was not proven specific to Unity 6.
- Manual quiver retrieval/holstering was inconclusive because of visibility. An independently recorded ArrowRetrieval null-parent exception and holster reference gaps still need attention.
- The assistant image/QR camera reference is intentionally unassigned. Preserve it unless a requested workflow requires changing it.
- Multilingual font fallbacks, duplicate EventSystems and arrow-reset physics warnings remain recorded concerns.
- Android player-script compilation previously failed with **11 compiler diagnostics**. This copy is for development and is not advertised as a working standalone Quest release. Full scavenger progression, all parkour stages, replay/reset behavior and standalone performance remain unverified.

No gameplay or lighting repairs are included merely by separating the project folders. The exact editor version is recorded in `ProjectSettings/ProjectVersion.txt`; update the documentation if that version changes.

## Source and contribution policy

This sibling was copied from the existing Unity 6.3 working tree on September 18, 2026, preserving its assets, `.meta` files, package manifest/lock and project settings. Repository baseline: `5303983999b7ba50e651fc3328dc9ef8dbb93ed9`.

Keep generated `Library`, `Temp`, `Logs`, `obj`, `UserSettings`, IDE outputs, builds and credentials out of commits. The project-local `.gitignore` is required because repository-root cache rules do not cover every nested Unity project. Review the staged file list before publishing changes.

Use this folder for Unity 6.3 work and make any legacy backports separately. Do not share or copy a `Library` directory between editor versions. Preserve the neighboring legacy project's tracked contents during upgrade-only changes.
