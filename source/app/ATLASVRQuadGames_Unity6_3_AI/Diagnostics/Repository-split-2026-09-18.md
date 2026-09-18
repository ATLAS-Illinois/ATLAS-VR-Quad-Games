# Repository split validation - September 18, 2026

The review branch adds `source/app/ATLASVRQuadGames_Unity6_3_AI` beside the original `source/app/ATLASVRQuadGames`. The original tracked Unity 2022 project is unchanged from main at `5303983999b7ba50e651fc3328dc9ef8dbb93ed9`.

## Source preservation and publication checks

- Legacy project Git tree before and after: `a7da77f797025bd7a1b8b8413df1ce6d7ba0be2a`.
- Copied and SHA-256-verified 6,617 source files from the existing Unity 6.3 working tree before documented publication adjustments.
- Preserved source assets, `.meta` GUIDs, manifest/lock and project settings. Intentional adjustments are documentation/path updates, project-local ignore rules, omission of local credential files, and a blank token field in the referenced Wit runtime template.
- Original working copy and its uncommitted upgrade changes were left intact on `upgrade/unity-6.3`.
- Verified version files, copied-source integrity, publication scope, known credential exclusions, Markdown links and JSON syntax.
- No new blob exceeds GitHub's 100 MiB file limit. Existing large assets remain the same Git objects; the sibling does not require re-uploading every unchanged asset.

## Editor checks

| Project | Exact Editor | Project script assemblies | Recorded C# errors |
| --- | --- | --- | ---: |
| Unity 6.3 AI | 6000.3.15f1 | `Scripts.dll` and `Assembly-CSharp.dll` generated; 151 editor script assemblies observed | 0 |
| Original | 2022.3.18f1 | `Scripts.dll` and `Assembly-CSharp.dll` generated; 104 editor script assemblies observed | 0 |

Both checks used hidden batch-mode, no-graphics Editor launches with fresh Library folders. The first Unity 6 attempt encountered a Meta MCP package-cache rename error; a retry restored the packages and generated the project assemblies.

The temporary editors were stopped after the script-compilation evidence was obtained while remaining assets/shaders were still importing. **Full asset-import completion, rendering, Play Mode, headset behavior and Android compilation were not retested here.** No clean full-Editor-exit claim is made. Initial import can continue when the project is next opened normally.

Existing warning-level deprecations and historical gameplay/build limitations are not repaired by this repository split. See the September 5 reports for those findings and their limits. API service checks require user-provided credentials in the new copy.

The import regenerated three texture files in the new checkout; those incidental working-tree changes were restored to the committed source snapshot. No legacy tracked files changed. Raw Editor logs remain in ignored `Temp/CodexSplitValidation` and are not published.

See [machine-readable results](Repository-split-2026-09-18.json) and [setup instructions](../README.md).
