using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class QuadRayBanDisplayPreview
{
    /// <summary>Renders only a clone of the authored Canvas, in an isolated scene.</summary>
    public static void Capture(RayBanDisplayShell shell, string path)
    {
        if (shell == null || shell.VisualRoot == null) throw new InvalidOperationException("Display has not been authored.");
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D image = null;
        var previous = RenderTexture.active;
        try
        {
            var copy = UnityEngine.Object.Instantiate(shell.VisualRoot.gameObject);
            foreach (var feature in copy.GetComponentsInChildren<RayBanFeatureScreen>(true)) feature.MarkAsRenderCopy();
            foreach (var component in copy.GetComponentsInChildren<Behaviour>(true))
                if (component.GetType().Namespace != null && component.GetType().Namespace.StartsWith("Oculus.Interaction", StringComparison.Ordinal)) component.enabled = false;
            SceneManager.MoveGameObjectToScene(copy, scene);
            copy.SetActive(true);
            copy.transform.SetParent(null, false);
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            copy.transform.localScale = Vector3.one;
            foreach (var node in copy.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
            var originalTiles = shell.VisualRoot.GetComponentsInChildren<RayBanTile>(true);
            var copiedTiles = copy.GetComponentsInChildren<RayBanTile>(true);
            for (int i = 0; i < originalTiles.Length; i++)
                copiedTiles[i].SetVisualFocus(originalTiles[i].Surface != null && originalTiles[i].Surface.Focus > .5f);
            foreach (var text in copy.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
            var cameraObject = new GameObject("Temporary display preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 300;
            camera.aspect = 1;
            camera.transform.position = new Vector3(0, 0, -1000);
            camera.nearClipPlane = 1;
            camera.farClipPlane = 2000;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .065f, .095f, 1);
            camera.cullingMask = 1 << 31;
            target = new RenderTexture(1000, 1000, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = target;
            copy.GetComponent<Canvas>().worldCamera = camera;
            Canvas.ForceUpdateCanvases();
            var originalMaps = shell.VisualRoot.GetComponentsInChildren<QuadOverheadMapGraphic>(true);
            var copiedMaps = copy.GetComponentsInChildren<QuadOverheadMapGraphic>(true);
            for (int i = 0; i < Mathf.Min(originalMaps.Length, copiedMaps.Length); i++)
                copiedMaps[i].ClonePresentationStateFrom(originalMaps[i]);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0);
            image.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [MenuItem("ATLAS VR/Assistant/Export display previews")]
    public static void ExportPreviews()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Use Capture directly during Play Mode.");
        var root = PrefabUtility.LoadPrefabContents(QuadRayBanDisplayTools.PrefabPath);
        try
        {
            var shell = root.GetComponentInChildren<RayBanDisplayShell>(true);
            shell.SetPage(2);
            Capture(shell, QuadRayBanDisplayTools.ReportDirectory + "/launcher.png");
            shell.SetPage(0);
            Capture(shell, QuadRayBanDisplayTools.ReportDirectory + "/quick-settings.png");
            shell.SetPage(1);
            Capture(shell, QuadRayBanDisplayTools.ReportDirectory + "/notifications-sample.png");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("[QuadDisplay] Isolated UI previews saved.");
    }
}
