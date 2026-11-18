using System;
using UnityEditor;
using UnityEngine;
using Whacamole.Gameplay;
using Object = UnityEngine.Object;

/// <summary>
/// Keeps the whac-a-mole scene in sync with the scripts, so you don't have to remember to run
/// Whacamole > Build Scene after the scene setup changes.
///
/// If the open scene has the Arena but is missing a piece the current scripts expect
/// (nut spawner, hammer grabber, nuts with the Nut script), it rebuilds the scene
/// right after scripts compile, and again just before entering Play mode if needed.
/// </summary>
[InitializeOnLoad]
public static class WhacamoleSceneAutoUpdate
{
    private static bool failedOnce;

    static WhacamoleSceneAutoUpdate()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) RebuildIfOutdated();
        };
        EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.ExitingEditMode) RebuildIfOutdated();
        };
    }

    private static void RebuildIfOutdated()
    {
        if (failedOnce || !IsOutdated()) return;

        Debug.Log("[Whacamole] Scene is out of date with the scripts. Rebuilding it now (same as Whacamole > Build Scene).");
        try
        {
            WhacamoleSceneBuilder.Build();
        }
        catch (Exception e)
        {
            failedOnce = true; // don't retry every time; fix the error and run Whacamole > Build Scene
            Debug.LogException(e);
        }
    }

    private static bool IsOutdated()
    {
        // Only ever touch the whac-a-mole scene (other Quad Games scenes could have an "Arena" object).
        if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name != WhacamoleSceneBuilder.SceneName) return false;
        if (GameObject.Find("Arena") == null) return false; // not the whac-a-mole scene

        if (Object.FindFirstObjectByType<NutSpawner>() == null) return true;
        if (Object.FindFirstObjectByType<HammerGrabber>() == null) return true;

        GameObject holes = GameObject.Find("Holes");
        if (holes != null)
        {
            foreach (Transform child in holes.transform)
                if (child.name.StartsWith("Nut_") && child.GetComponent<Nut>() == null) return true;
        }
        return false;
    }
}
