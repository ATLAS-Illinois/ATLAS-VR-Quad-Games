# ATLAS VR Quad Games

A University of Illinois campus VR experience with exploration, mini-games, and an XR assistant.

## Choose a Unity project

| Project | Required Unity Editor | Purpose and status |
| --- | --- | --- |
| [ATLASVRQuadGames](source/app/ATLASVRQuadGames/) | **2022.3.18f1** | Preserved original project. This addition does not modify its tracked contents. |
| [ATLASVRQuadGames_Unity6_3_AI](source/app/ATLASVRQuadGames_Unity6_3_AI/) | **6000.3.15f1 (Unity 6.3)** | Unity 6.3 project for AI-assisted development, including Unity AI Assistant, Meta Unity MCP tooling, and project skills. Upgrade validation is ongoing. |

Clone this repository, then add the desired project folder in Unity Hub and open it with the exact editor version above. The repository root and `source/app` are not Unity project roots. Each project has its own `Assets`, `Packages`, `ProjectSettings`, and generated `Library`.

The two source copies are independent. Changes to one do not automatically update the other. Preserve the 2022 version for legacy work; use the Unity 6.3 AI project for ongoing upgrade and AI-tooling development. Do not open the legacy folder with Unity 6 to select the newer version.

See the [project selector](source/app/README.md) and [Unity 6.3 AI setup and known issues](source/app/ATLASVRQuadGames_Unity6_3_AI/README.md). The new project remains Built-in Render Pipeline with the Oculus loader; its name does not imply a URP or OpenXR migration.
