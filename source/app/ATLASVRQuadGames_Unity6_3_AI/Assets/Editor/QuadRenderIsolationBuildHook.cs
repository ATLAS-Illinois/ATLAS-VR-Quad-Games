#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in only; injects into Unity's processed build scene, never the authored scene or prefab.</summary>
public static class QuadRenderIsolationBuildHook
{
    public static bool Enabled { get; set; }

    [PostProcessScene(1000)]
    public static void OnPostProcessScene()
    {
        if (!Enabled || !BuildPipeline.isBuildingPlayer) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/QuadProjectAlexanderLau.unity") return;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponent<QuadRenderIsolationProbe>() != null) return;
        var probe = new GameObject("Quad diagnostic render isolation");
        SceneManager.MoveGameObjectToScene(probe, scene);
        probe.AddComponent<QuadRenderIsolationProbe>();
        Debug.Log("[QuadRenderProbe] Injected diagnostic control into processed build scene.");
    }
}
#endif
