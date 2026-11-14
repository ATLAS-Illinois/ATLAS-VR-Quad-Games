# XR Assistant display integration

The shared `Assets/Prefabs/XR Assistant Rig.prefab` supplies the display in `Assets/Scenes/QuadProjectAlexanderLau.unity`. Its three pages are Quick Settings, local app activity, and a scrolling twelve-app launcher. The display uses the existing camera rig, Quest optical hand tracking, controller and finger pointers, and uGUI navigation.

## Standalone Quest 3 rendering

On November 14, 2026, the development main build of HelloWorldVR was built with Unity 6000.3.15f1 (0 errors, 14 warnings), installed, and launched on a standalone Quest 3. From the golden-donut sign, the tester faced the mammoth and tilted their head upward. The campus and sign stayed visible and the jagged lines were gone. The same test previously passed on the separate GitHub comparison build.

The mammoth standee's decorative MeshRenderer is disabled. Its GameObject stays active for gameplay. The comparison test showed that disabling its wireframe rendering removed the streaks. The remaining world blackout cleared with Android Vulkan, Oculus Multiview and Optimize Buffer Discards disabled.

## Display and input

The 600 x 600 canvas uses local scale 0.000396 under its unit-scale display parent, for a visible 0.2376 m square. The rig follows horizontal head rotation. Its existing positions, offsets and right-hand gesture assignments are authored in the scene and prefab.

The display uses the Meta PointableCanvasModule for controller-ray and finger input. The redundant standalone EventSystem in the campus scene is disabled. Keyboard arrows, Enter/Space, and Escape/Backspace operate focused controls in the Editor. The controller Menu button hides or restores the display. The existing right OVRHand and OVRSkeleton provide pinch-and-twist for Camera zoom and Music volume.

Captions supports English, Spanish, Simplified Chinese, Korean and Japanese. The regular and bold Noto display fonts fall back to Nanum Gothic for Korean. Regional Noto fonts and dynamic atlases cover Chinese and Japanese. The fonts and their licenses are in `Assets/Art/RayBanDisplay/Fonts`. Use **ATLAS VR > Assistant > Repair caption fonts** to restore the shared caption font assignments.

## Connected features

| Screen | Existing Quad service | Behavior |
| --- | --- | --- |
| Camera / Photos | `VRCameraCapture`, `VR_Snapshot_Camera`, existing RenderTexture, `GalleryDataManager` / `GalleryPhotoStore` | Renders the virtual campus and saves the displayed crop in a local album across launches. Up to 24 photos, maximum 1024 pixels per edge. Photos offers confirmed deletion and preserves older captures when full. |
| Music | `Audio Player/AudioPlayerRoot` and `SimpleAudioPlayer` | Existing tracks/art, play/pause, next/previous, per-player volume through twist. |
| Assistant | `AI Behaviour`, `AIManager`, `WakeWordManager`, Wit voice/TTS | Existing destination configuration and persona. Opens idle; Listen starts recognition. Close cancels work and releases microphone/voice ownership. |
| Captions | `Captions Page/CaptionsRoot`, `LiveTranslationManager` | Existing Azure backend, language choices and translation view. Opens idle and closes/disposes its recognizer. |
| Maps | `VRMapNavigator`, `Resources/Map_Settings`, `RayBanMapBridge`, `Resources/Quad_WaypointMap` | 19 landmarks on a baked overhead scene map, with offline waypoint guidance, remaining route distance, arrival and deviation recovery. North-up map with player/destination markers and route overlay; no phone GPS. See [registry provenance](landmark-registry.md) and [map/pathway behavior](quad-map-pathways.md). |

Music pauses while Assistant owns the audio and resumes after the response if it was previously playing. In the Android player, Assistant uses the managed Wit voice path configured from the existing AppVoice settings. Closing Assistant cancels recognition and speech. Captions uses its separate translation recognizer. Configure the services locally as described in the [project README](../README.md).

Quick Settings exposes app volume, display brightness, Audio only, Do not disturb and feature shortcuts. Settings includes accessibility, launcher order, app activity, welcome-guide replay and display-preference reset. Photos and route arrivals appear in local app activity. The first-run tutorial introduces feature controls. **Tutorials > Quad games** opens the [six game guides](quad-game-guide.md).

The camera timer fixes the selected crop when the countdown begins. The Photos viewer supports image details, centered zoom and confirmed deletion. Music exposes track selection, volume, seeking and repeat. Captions lets users browse earlier transcript pages and return to the latest output.

Instagram, WhatsApp, Messenger, Messages and Calls display in-app sample conversations, posts and calls. They use local sample state. The adaptation draws on the [public Meta Display toolkit](https://github.com/facebook/meta-ray-ban-display-ui-toolkit-web). Live third-party social accounts, SMS and phone calls are outside the scope of these previews.

## Authoring and validation

Open the integrated campus scene before using **ATLAS VR > Assistant > Install or rebuild display UI**. **Validate display UI** checks the serialized structure, and **Export display previews** renders the authored canvas. Under **Tools > Ray-Ban Display**, the runtime integration, ISDK pointer, head-following and Assistant/music validation commands exercise their corresponding systems.

The November 8 settings, app order, media, captions and photo viewer Play Mode checks passed 88 of 88 assertions. These used synthetic captions, test audio clips and temporary album data. Use a standalone headset for physical input, real microphone/TTS behavior, and rendering acceptance. For local navigation, see the [landmark registry](landmark-registry.md) and [map and pathways](quad-map-pathways.md).
