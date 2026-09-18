# ATLAS VR Quad Games: Unity 6 upgrade validation

Tested on 2026-09-05 with Unity **6000.3.15f1** and a connected **Quest 3 over Quest Link**.

**Result: the project runs in the Editor, but is not ready for an Android/standalone Quest build.** Android managed-script compilation failed. Several authored scenes also have startup errors. A changed TextMesh Pro fallback-font configuration explains the newly observed multilingual text problems.

## Comparison baseline

- Working branch: `upgrade/unity-6.3`; upgrade changes are uncommitted.
- Local `HEAD`, `main`, and `origin/main`: `5303983999b7ba50e651fc3328dc9ef8dbb93ed9` (2026-05-20).
- That snapshot specifies Unity **2022.3.18f1**. It was used for source/serialization comparisons; the old Editor was not run.
- Live GitHub branch verification was unavailable because the Git network request failed. The baseline above is an exact local snapshot, not a claim about today's latest remote commit.
- Current project remains **Built-in Render Pipeline**, Linear, Oculus XR loader, Android ARM64/IL2CPP. Meta Core/Interaction remain 62.0.0; Voice remains 60.0.0.
- Relevant dependency changes include Input System 1.8.1 -> 1.19.0, XRI 3.1.2 -> 3.3.1, Navigation 1.1.6 -> 2.0.12, uGUI 1 -> 2, and TMP registry 3.0.6 -> builtin 5.0.0. Changed packages alone do not prove an incompatibility.

## What was actually tested

All **eight authored scenes** were loaded and entered Play Mode using their existing cameras/rigs. MikeLee received a longer interactive headset session; the other seven received short startup smoke tests, approximately 1.7-12.2 seconds of recorded simulation time. This does not establish full gameplay completion.

The user confirmed these MikeLee checks:

- Campus appears in both eyes.
- View follows head movement.
- Both controllers are visible and working in the initial tracking check.
- Paper 1 pages change normally using the controller buttons.

A separate movement/jump/logo-block check was requested but no result had been received when this report was written. Paper paging must not be reported as failed: temporary diagnostic reads returned invalid Unity XR hand devices, but the actual user test succeeded.

Static inspection covered all eight authored scenes, 18 gameplay/shared prefabs and all 887 materials, using 17,854 indexed asset/package GUIDs. Live scene checks also counted missing scripts and checked materials assigned to Renderers.

### Scene results

Console numbers below are returned log rows, **not total repeated occurrences**. Per-frame errors can be collapsed. Counts include environment/tooling warnings. MikeLee includes interactive menu activity; other rows reflect startup.

| Scene | Errors | Warnings | Missing scripts | Renderer materials checked | Main result |
|---|---:|---:|---:|---:|---|
| `Assets/Scenes/QuadProjectMikeLee.unity` | 6 | 36 | 0 | 417 | Mammoth references; missing API configuration; translation startup exception; multilingual font warnings |
| `Assets/Scenes/Archery Scene.unity` | 9 | 23 | 22 | 37 | Nine unassigned `TargetRing.bullseye` references; missing legacy scripts on arrows |
| `Assets/Scenes/QuadProjectVictorLiu.unity` | 1 | 2 | 1 | 416 | Mammoth reference; missing script on `OVRControllers` |
| `Assets/Scenes/QuadProjectCarltonWoo.unity` | 1 | 1 | 0 | 434 | Mammoth reference |
| `Assets/QuadDay-VictorLiu.unity` | 1 | 1 | 0 | 394 | Mammoth reference |
| `Assets/Scenes/SampleScene.unity` | 0 | 0 | 0 | 9 | Startup passed; this sample has no OVR rig |
| `Assets/SampleScene.unity` | 0 | 2 | 0 | 37 | Startup passed; deprecated `OVRControllerHands` warning |
| `Assets/Scenes/QuadProjectAlexanderLau.unity` | 1 | 5 | 0 | 451 | Missing API configuration; two active EventSystems |

Each scene with an OVR rig had one active rig and a stereo `CenterEyeAnchor` during its runtime inspection. No checked Renderer material reported a missing, unsupported, or internal-error shader on the current PC graphics device. This is not an Android shader-variant test or a guarantee of correct lighting/stereo appearance. A camera-tool capture of MikeLee looked unusually dark; it was not independently confirmed as a headset defect.

## Confirmed changed configuration: multilingual font fallback chain

[Main font asset](<../Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset#L7715>) lost three entries from `m_FallbackFontAssetTable` compared with the original snapshot:

| Removed fallback | Existing asset |
|---|---|
| MSYH | `Assets/Scripts/XR Assistant/MSYH SDF.asset` |
| NANUMGOTHIC | `Assets/Scripts/XR Assistant/NANUMGOTHIC SDF.asset` |
| MSGOTHIC | `Assets/Scripts/XR Assistant/MSGOTHIC SDF.asset` |

All three assets still exist unchanged and contain relevant Chinese/Japanese/Korean glyphs. Only the ordinary LiberationSans fallback remains linked.

The MikeLee Console reported **31 missing-glyph warning rows** from translation dropdown labels. This is a concrete regression in the changed TMP configuration. The evidence does not establish which import or action removed the links. Repair should restore the intended fallback chain and retest both dropdowns and translated captions; no repair was made during this diagnostic run.

## Android compilation: failed

Executed Unity's `PlayerBuildInterface.CompilePlayerScripts` for `BuildTarget.Android`, Android group, DevelopmentBuild, with output under `Temp/CodexAndroidScriptValidation-20260905`.

**Result: zero returned assemblies, 11 compiler diagnostics plus one “Failed to compile player scripts” summary.**

| Source | Compiler failure |
|---|---|
| [MultiShotBow.cs:4](<../Assets/Scripts/Archery/MultiShotBow.cs#L4>) | CS0234: `UnityEditor.PackageManager` unavailable |
| [QuiverScript.cs:5](<../Assets/Scripts/Archery/QuiverScript.cs#L5>) | CS0234: `UnityEditor.Callbacks` unavailable |
| [NavMeshMovement.cs:2](<../Assets/Scripts/General Quad/NavMeshMovement.cs#L2>) | CS0234: `UnityEditor.Experimental.GraphView` unavailable |
| [IsGoldenDonutGrabbed.cs:5](<../Assets/Scripts/Mammoth/IsGoldenDonutGrabbed.cs#L5>) | CS0234: `UnityEngine.Rendering.VirtualTexturing` unavailable; import is unused |
| [APIConfigWindow.cs:5](<../Assets/Scripts/XR Assistant/APIConfigWindow.cs#L5>) | Three CS0246 errors: EditorWindow/MenuItem types |
| [MapConfigWindow.cs:5](<../Assets/Scripts/XR Assistant/Editor/MapConfigWindow.cs#L5>) | Four CS0246 errors: EditorWindow/SerializedObject/MenuItem types |

The offending imports/editor classes already exist in the original snapshot. `Scripts.asmdef` includes all platforms; the nested `Editor` folder does not establish a separate editor assembly here. These are newly measured Android failures with pre-existing source causes, not evidence that Unity 6 introduced those lines.

Compilation stopped before IL2CPP, native plugins, shader builds, Gradle, APK creation or device installation. Further errors may surface after these first blockers are repaired.

The existing build list also contains only a disabled entry for missing `Assets/Scenes/QuadProject.unity`. This predates the upgrade. There is no designated main scene; any future APK test must explicitly select the relevant scene set.

## Existing scene and service problems reproduced

### Mammoth

- MikeLee `Mammoth Tag/Golden Donut`: [MammothEatDonut.Start:15](<../Assets/Scripts/Mammoth/MammothEatDonut.cs#L15>) dereferences unassigned `goldenDonut`.
- MikeLee `Mammoth Model`, plus VictorLiu/CarltonWoo/QuadDay: [NavMeshMovement.Update:162](<../Assets/Scripts/General Quad/NavMeshMovement.cs#L162>) dereferences unassigned `goldenDonutIdol`.
- MikeLee scene serialization stores obsolete field names instead of the expected fields. The original snapshot has the same scene/code mismatch.
- Merely assigning the first null field would not fully fix MikeLee: `MammothEatDonut` also calls `GetComponent<NavMeshMovement>()` on Golden Donut, while that movement component is on Mammoth Model.

### Standalone Archery Scene and Victor rig

- Nine TargetRing components in `Archery Scene` serialize old `points` data but lack the required renderer references. [TargetRing.Start:27](<../Assets/Scripts/Archery/TargetRing.cs#L27>) fails on `bullseye`.
- The same scene has 22 dangling script GUIDs on arrows: `31aba413f8b0d0f4e8161fb267c167f6`. Valid Arrow components also exist; do not replace them blindly.
- VictorLiu's `OVRControllers` has one dangling script GUID: `c49e26153d69c77449a52a7d10d3fddb`.
- These exact references and the relevant TargetRing code already occur in the original snapshot. This is separate from the archery areas in the contributor scenes.

### XR assistant and translation

- `Assets/Resources/API_Secrets.asset` is absent and Git-ignored. AIManager startup logs the missing file; using the assistant also reports a missing Gemini API key.
- Opening translation logs the same missing configuration. Toggling its microphone reached `SpeechTranslationConfig.FromSubscription` and threw `ApplicationException 0x5` at [LiveTranslationManager.cs:92](<../Assets/Scripts/XR Assistant/LiveTranslationManager.cs#L92>).
- Missing configuration leaves the private key/region unset, but the existing code permits translation startup anyway. That unguarded path is unchanged from the baseline. The exception alone does not establish an Android native-plugin or Unity 6 regression.
- Credentials and transcripts were not written into this report/evidence artifact. Valid cloud assistant/translation operation remains unverified.

### AlexanderLau UI

The scene has two enabled, active EventSystems:

- `XR Assistant Rig/PointableCanvasModule`
- Root `EventSystem`

Runtime warnings explicitly report the duplicate. The scene/shared prefab content is unchanged against the baseline; no source edit was made during testing. UI interactions need retesting after ownership is resolved.

## Other warnings and asset-scan limits

- Local dimming unsupported: Meta runtime/environment warning; not a C# exception.
- Meta Voice telemetry consent absent: telemetry is not being sent; no consent setting was changed.
- Unity AI Account API timeout: editor tooling/connectivity warning.
- `SimpleAudioPlayer` emits warning-level `PLAY` messages during ordinary music interaction.
- `OVRControllerHands` in root SampleScene is explicitly deprecated.
- Four material shader GUIDs are unresolved in the whole-asset scan: `tower0.mat`, TMP's `Ground - URP.mat` and `Crate - URP.mat`, and fireworks `Grid.mat`. The latter three are referenced by vendor demo scenes; no serialized usage was found for tower0.mat. They did not produce unsupported-material findings in the eight authored scene checks.
- Another 16 unresolved script references on 13 materials are hidden metadata subassets, not 16 additional missing gameplay components.
- No unresolved prefab-source GUIDs were found in the scanned scope. GUID checks cannot validate every prefab subobject/fileID or UnityEvent contract.
- `UpgradeLog.htm` and `UpgradeLog2.htm` are Visual Studio solution migration reports, not Unity asset/compile reports.
- The existing test folder has an asmdef but no test implementation files.

## Remaining validation

After addressing the recorded blockers, perform complete archery grab/nock/fire/score/retrieve/reset, snap assembly/wall completion/re-entry, parkour jump/climb/checkpoint/timer/teleport, mammoth pursuit/contact/game-over, scavenger chapter/sensor progression and assistant lifecycle tests in their relevant scenes.

Standalone Quest testing still requires a successful Android build, installation/launch, and checks of native plugins, permissions, stereo/shaders, audio/haptics and sustained device performance. Quest Link runs the Editor workload on the PC and cannot establish standalone performance or Android compatibility. See [Meta's Quest Link guidance](https://developers.meta.com/horizon/documentation/unity/unity-link/).

## Final state and artifacts

- Original `QuadProjectMikeLee` scene restored, clean, with Play Mode stopped.
- Existing Built-in renderer, Oculus XR setup, Android target and gravity `(0,-4.9,0)` retained.
- No gameplay scripts, scenes, prefabs, materials, packages or settings were intentionally edited during this validation.
- Unity regenerated Voice manifest/runtime/compiler cache data during testing. The generated Voice manifest was already listed as modified before testing; its final tracked content diff is empty apart from line-ending handling.
- Comparing Git status with the earlier scan found no newly listed changes to existing assets. New entries were the earlier requested `AGENTS.md` and this `Diagnostics/` folder.
- Whole-tree `git diff --check` reports whitespace issues in the pre-existing TMP working-tree changes; these were left untouched.
- This report: `Diagnostics/Unity6-upgrade-2026-09-05.md`.
- Sanitized per-scene/compile Console evidence: `Diagnostics/Unity6-upgrade-2026-09-05.json`.
- Final Console retains the Android compiler failures for inspection rather than presenting an artificially clean result.
