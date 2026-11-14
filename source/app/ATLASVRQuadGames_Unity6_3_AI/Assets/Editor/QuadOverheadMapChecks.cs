#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Isolated presentation checks; no authored asset or scene is changed.</summary>
public static class QuadOverheadMapChecks
{
    public static string RunGeometryChecks()
    {
        if (Application.isPlaying) return "Run overhead-map geometry checks in Edit Mode.";
        var lines = new List<string> { "Overhead map geometry checks", "UTC=" + DateTime.UtcNow.ToString("O") };
        int passed = 0, failed = 0;
        Action<bool, string> check = (condition, message) =>
        {
            if (condition) passed++; else failed++;
            lines.Add((condition ? "PASS " : "FAIL ") + message);
        };
        var scene = EditorSceneManager.NewPreviewScene();
        QuadWaypointMap map = null;
        Texture2D texture = null;
        try
        {
            var root = new GameObject("Temporary map geometry checks", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, scene);
            var image = Child(root.transform, "Background").gameObject.AddComponent<RawImage>();
            var graphic = Child(root.transform, "Overlay").gameObject.AddComponent<QuadOverheadMapGraphic>();
            graphic.SetBackground(image);
            map = ScriptableObject.CreateInstance<QuadWaypointMap>();
            map.hideFlags = HideFlags.HideAndDontSave;
            texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            map.overheadMap = texture;
            map.worldRect = new Rect(-50, -80, 100, 160);
            var points = new List<Vector3> { new Vector3(-20, 0, -40), new Vector3(20, 0, -40), new Vector3(20, 0, 40) };
            graphic.SetState(map, points, points[0], true, Vector3.forward, points[2], true, true, 0);
            Vector2 center = graphic.WorldToLocal(Vector2.zero);
            Vector2 east = graphic.WorldToLocal(Vector2.right * 10), north = graphic.WorldToLocal(Vector2.up * 10);
            check(east.x > center.x && north.y > center.y && Mathf.Abs(east.y - center.y) < .001f &&
                Mathf.Abs(north.x - center.x) < .001f, "+X maps right and +Z maps up");
            check(Mathf.Abs((east.x - center.x) - (north.y - center.y)) < .001f,
                "Horizontal and vertical meters retain equal scale in the wide viewport");
            check(graphic.rectTransform.rect.Contains(graphic.WorldToLocal(new Vector2(-20, -40))) &&
                graphic.rectTransform.rect.Contains(graphic.WorldToLocal(new Vector2(20, 40))),
                "Player, destination and route extents fit inside the viewport");
            check(Registered(image, graphic, map, Vector2.zero) && Registered(image, graphic, map, new Vector2(20, 40)),
                "Cropped texture and overlay register at center and target");
            check(image.enabled && image.uvRect.xMin >= 0 && image.uvRect.yMin >= 0 &&
                image.uvRect.xMax <= 1 && image.uvRect.yMax <= 1 && image.rectTransform.rect.width < 560,
                "Outside-map space does not stretch the texture border");
            Rect before = graphic.WorldViewport;
            graphic.SetState(map, points, points[0] + Vector3.right, true, Vector3.right, points[2], true, true, 0);
            check(graphic.WorldViewport == before, "Small player movement preserves stable viewport framing");
            graphic.SetState(map, null, Vector3.zero, true, Vector3.forward, Vector3.forward * 20, true, false, 1);
            check(graphic.WorldViewport.height >= 80 && graphic.WorldViewport.height < before.height,
                "Changing destination resets the fit while retaining minimum readable map extent");
            check(!image.raycastTarget && !graphic.raycastTarget, "Texture and overlay do not enter gesture/pointer focus");

            Vector2 a = new Vector2(-10, 0), b = new Vector2(10, 0);
            check(QuadOverheadMapGraphic.ClipSegment(ref a, ref b, new Rect(-5, -5, 10, 10)) &&
                (a - new Vector2(-5, 0)).sqrMagnitude < .00001f && (b - new Vector2(5, 0)).sqrMagnitude < .00001f,
                "Crossing route segment clips exactly to viewport edges");
            a = new Vector2(-10, 9); b = new Vector2(10, 9);
            check(!QuadOverheadMapGraphic.ClipSegment(ref a, ref b, new Rect(-5, -5, 10, 10)),
                "Parallel route outside the viewport is rejected");

            points.Clear();
            for (int i = 0; i < 4096; i++) points.Add(new Vector3(i * .05f, 0, (i % 2 == 0 ? -.1f : .1f)));
            graphic.SetState(map, points, points[0], true, Vector3.forward, points[4095], true, true, 2);
            int vertexCount = PopulateCount(graphic);
            check(vertexCount > 32000 && vertexCount < 65535,
                "Worst-case 4096-point zigzag with joins and both markers fits uGUI mesh limit; vertices=" + vertexCount);

            var copiedImage = Child(root.transform, "Copied background").gameObject.AddComponent<RawImage>();
            var copy = Child(root.transform, "Copied overlay").gameObject.AddComponent<QuadOverheadMapGraphic>();
            copy.SetBackground(copiedImage);
            copy.ClonePresentationStateFrom(graphic);
            check(copy.WorldViewport == graphic.WorldViewport && copiedImage.uvRect == image.uvRect &&
                PopulateCount(copy) == vertexCount, "Preview clone preserves viewport, texture crop and runtime geometry");
        }
        catch (Exception exception) { check(false, exception.ToString()); }
        finally
        {
            if (map != null) UnityEngine.Object.DestroyImmediate(map);
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        lines.Add("RESULT passed=" + passed + " failed=" + failed);
        Directory.CreateDirectory("Diagnostics/2026-10-25-RayBanUI");
        string report = string.Join(Environment.NewLine, lines);
        File.WriteAllText("Diagnostics/2026-10-25-RayBanUI/waypoint-ui-geometry-checks.txt", report);
        return report;
    }

    private static RectTransform Child(Transform parent, string name)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(560, 220);
        return rect;
    }

    private static bool Registered(RawImage image, QuadOverheadMapGraphic graphic, QuadWaypointMap map, Vector2 point)
    {
        Vector2 uv = new Vector2((point.x - map.worldRect.xMin) / map.worldRect.width,
            (point.y - map.worldRect.yMin) / map.worldRect.height);
        Vector2 imageLocal = new Vector2(image.rectTransform.rect.xMin + (uv.x - image.uvRect.xMin) / image.uvRect.width * image.rectTransform.rect.width,
            image.rectTransform.rect.yMin + (uv.y - image.uvRect.yMin) / image.uvRect.height * image.rectTransform.rect.height);
        Vector3 actual = graphic.rectTransform.InverseTransformPoint(image.rectTransform.TransformPoint(imageLocal));
        return ((Vector2)actual - graphic.WorldToLocal(point)).sqrMagnitude < .0001f;
    }

    private static int PopulateCount(QuadOverheadMapGraphic graphic)
    {
        using (var mesh = new VertexHelper())
        {
            typeof(QuadOverheadMapGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(VertexHelper) }, null)
                .Invoke(graphic, new object[] { mesh });
            return mesh.currentVertCount;
        }
    }
}
#endif
