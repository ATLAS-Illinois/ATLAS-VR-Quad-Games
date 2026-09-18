# Select an ATLAS VR Quad Games project

| Folder | Editor version | Use |
| --- | --- | --- |
| [ATLASVRQuadGames](ATLASVRQuadGames/) | Unity **2022.3.18f1** | Original project retained unchanged. |
| [ATLASVRQuadGames_Unity6_3_AI](ATLASVRQuadGames_Unity6_3_AI/) | Unity **6000.3.15f1 / 6.3** | AI-assisted development and Unity 6.3 upgrade work. See its README for validation limits. |

In Unity Hub, add one of these folders directly and choose its matching editor. Keep each project's generated caches separate. The project name `Unity6_3_AI` describes its editor generation and development tooling; runtime assistant features also existed in the original project.

When contributing, state which project you changed. Keep fixes for the two versions explicit; avoid bulk replacement or automatic synchronization across their `Assets`, packages, or settings. Shared `.meta` GUIDs across two separate project roots are intentional and do not require regeneration.
