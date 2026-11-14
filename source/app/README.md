# Select an ATLAS VR Quad Games project

| Folder | Editor version | Use |
| --- | --- | --- |
| [ATLASVRQuadGames](ATLASVRQuadGames/) | Unity **2022.3.18f1** | Legacy campus project with the standalone Quest rendering fix. |
| [ATLASVRQuadGames_Unity6_3_AI](ATLASVRQuadGames_Unity6_3_AI/) | Unity **6000.3.15f1 / 6.3** | AI-assisted development and Unity 6.3 upgrade work. See its README for validation limits. |

In Unity Hub, add one of these folders directly and choose its matching editor. Keep each project's generated caches separate. The project name `Unity6_3_AI` describes its editor generation and development tooling; runtime assistant features also existed in the original project.

When contributing, state which project you changed. Keep fixes for the two versions explicit; avoid bulk replacement or automatic synchronization across their `Assets`, packages, or settings. Shared `.meta` GUIDs across two separate project roots are intentional and do not require regeneration.

## Building the Unity 2022 campus

Open `ATLASVRQuadGames` with Unity 2022.3.18f1 and Android Build Support. Open `Assets/Scenes/QuadProjectAlexanderLau.unity`. In **File > Build Settings**, select **Android**, switch platform, and use **Add Open Scenes** to include and enable the AlexanderLau scene. Select the connected Quest under **Run Device**, then choose **Build And Run**.

To check rendering on the headset, stand at the golden-donut sign, face the mammoth and tilt your head upward while keeping the sign in view. On November 14, 2026, this test passed on a standalone Quest 3 with Unity 2022.3.18f1. The campus and sign stayed visible and the jagged lines were gone.
