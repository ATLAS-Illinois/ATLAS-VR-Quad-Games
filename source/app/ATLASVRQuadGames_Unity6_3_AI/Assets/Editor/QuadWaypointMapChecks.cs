#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Explicit, offline diagnostics. Never changes a scene object, player pose,
/// asset, microphone state, or Play Mode. Spatial checks use the loaded scene's
/// colliders and simulated position values, not physical headset locomotion.
/// </summary>
public static class QuadWaypointMapChecks
{
    private const string DirectoryPath = "Diagnostics/2026-10-25-RayBanUI";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private sealed class Report
    {
        private readonly List<string> lines = new List<string>();
        private int passed, failed;
        public Report(string title)
        {
            lines.Add(title);
            lines.Add("UTC=" + DateTime.UtcNow.ToString("O"));
        }
        public void Check(bool condition, string detail)
        {
            if (condition) passed++; else failed++;
            lines.Add((condition ? "PASS " : "FAIL ") + detail);
        }
        public void Note(string text) => lines.Add(text);
        public string Save(string fileName)
        {
            lines.Add("RESULT passed=" + passed + " failed=" + failed);
            Directory.CreateDirectory(DirectoryPath);
            string report = string.Join(Environment.NewLine, lines);
            File.WriteAllText(Path.Combine(DirectoryPath, fileName), report);
            return report;
        }
    }

    public static string RunSyntheticChecks()
    {
        var report = new Report("Waypoint graph and progress regression checks");
        var map = ScriptableObject.CreateInstance<QuadWaypointMap>();
        map.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            map.scenePath = "Synthetic/Quad.unity";
            map.worldRect = new Rect(-10f, -20f, 40f, 80f);
            map.nodes = new List<QuadMapNode>
            {
                Node("A", 0f, 0f), Node("B", 10f, 0f), Node("C", 20f, 0f), Node("Island", 0f, 20f)
            };
            map.edges = new List<QuadMapEdge>
            {
                Edge(1, 0, new Vector3(10f,0f,0f), new Vector3(5f,0f,2f), Vector3.zero),
                Edge(1, 2, new Vector3(10f,0f,0f), new Vector3(20f,0f,0f)),
                Edge(0, 2, Vector3.zero, new Vector3(0f,0f,50f), new Vector3(20f,0f,50f), new Vector3(20f,0f,0f))
            };
            map.destinations = new List<QuadMapDestination> { new QuadMapDestination { landmarkName = "C", nodeIndex = 2 } };
            var points = new List<Vector3>();
            float expectedCost = 2f * Mathf.Sqrt(29f) + 10f;
            report.Check(map.TryValidate(out _), "Synthetic graph validates");
            bool forward = QuadWaypointRouter.TryFindGraphPath(map, 0, 2, points, out float length);
            report.Check(forward && Mathf.Abs(length - expectedCost) < .001f && points.Count == 4 &&
                points[0] == Vector3.zero && points[1] == new Vector3(5f, 0f, 2f) && points[3] == new Vector3(20f, 0f, 0f),
                "Dijkstra chooses lower polyline distance over the direct but long edge; reversed stored edge expands correctly");
            bool reverse = QuadWaypointRouter.TryFindGraphPath(map, 2, 0, points, out length);
            report.Check(reverse && Mathf.Abs(length - expectedCost) < .001f && points.Count == 4 &&
                points[0] == new Vector3(20f, 0f, 0f) && points[2] == new Vector3(5f, 0f, 2f) && points[3] == Vector3.zero,
                "Undirected reverse route preserves corner order and distance");
            report.Check(!QuadWaypointRouter.TryFindGraphPath(map, 3, 2, points, out length) && points.Count == 0,
                "Disconnected node fails without inventing a straight-line route");
            map.edges[0].corners[0] += Vector3.right;
            report.Check(!map.TryValidate(out _), "Mismatched authored endpoint is rejected");
            map.edges[0].corners[0] -= Vector3.right;
            map.destinations.Add(new QuadMapDestination { landmarkName = "C", nodeIndex = 1 });
            report.Check(!map.TryValidate(out _), "Duplicate destination name is rejected");
            map.destinations.RemoveAt(map.destinations.Count - 1);
            map.blockedRegions = new[] { new Rect(4f, -2f, 2f, 4f) };
            report.Check(QuadWaypointRouter.CrossesBlockedRegion(map, Vector3.zero, Vector3.right * 10f),
                "Straight connector through building footprint is rejected without needing an enabled collider");
            report.Check(QuadWaypointRouter.CrossesBlockedRegion(map, new Vector3(5f, 0f, 0f), new Vector3(5f, 0f, 1f)),
                "Connector beginning inside a building footprint is rejected");
            report.Check(QuadWaypointRouter.CrossesBlockedRegion(map, new Vector3(0f, 0f, 2.2f), new Vector3(10f, 0f, 2.2f)),
                "Building footprint includes body-clearance margin");
            report.Check(!QuadWaypointRouter.CrossesBlockedRegion(map, new Vector3(0f, 0f, 4f), new Vector3(10f, 0f, 4f)),
                "Parallel exterior connector remains available");
            Vector2 uv = map.WorldToUV(new Vector3(10f, 50f, 20f));
            report.Check((uv - new Vector2(.5f, .5f)).sqrMagnitude < .000001f &&
                map.WorldToUV(new Vector3(10f, 0f, 30f)).y > uv.y,
                "Overhead mapping centers correctly, ignores height, and maps positive Z upward");

            var router = new QuadWaypointRouter();
            report.Check(!router.Build(map, "Wrong/Scene.unity", Vector3.zero, null, "C") && !router.HasRoute &&
                router.Instruction.Contains("different scene"), "Mismatched scene fails before spatial routing");

            var shortSegments = new List<Vector3>();
            for (float x = 0f; x < 10f; x += .75f) shortSegments.Add(new Vector3(x, .05f, 0f));
            shortSegments.Add(new Vector3(10f, .05f, 0f));
            SeedProgress(router, shortSegments);
            UpdateGroundProgress(router, new Vector3(0f, .05f, 0f));
            report.Check(Mathf.Abs(router.RemainingDistance - 10f) < .001f, "Remaining distance does not advance before the player moves");
            UpdateGroundProgress(router, new Vector3(3f, .05f, 0f));
            report.Check(Mathf.Abs(router.RemainingDistance - 7f) < .001f,
                "A three-meter update crosses short .75m connector segments without getting stuck");
            float previousDistance = router.RemainingDistance;
            UpdateGroundProgress(router, new Vector3(2.8f, .05f, 0f));
            report.Check(Mathf.Abs(previousDistance - router.RemainingDistance) < .001f, "Small tracking reversal does not regress route progress");

            SeedProgress(router, shortSegments);
            previousDistance = float.PositiveInfinity;
            bool monotonic = true;
            for (float x = 0f; x <= 10f; x += .5f)
            {
                UpdateGroundProgress(router, new Vector3(x, .05f, 0f));
                monotonic &= router.RemainingDistance <= previousDistance + .001f;
                previousDistance = router.RemainingDistance;
            }
            report.Check(monotonic && router.Arrived && router.RemainingDistance == 0f,
                "Half-meter walking increments yield decreasing remaining distance and arrival");
            SeedProgress(router, shortSegments);
            UpdateGroundProgress(router, new Vector3(10f, 5f, 0f));
            report.Check(!router.Arrived, "Same X/Z on a different height does not trigger arrival");
            SeedProgress(router, new List<Vector3>
            {
                Vector3.zero, Vector3.right * 10f, new Vector3(10f,0f,10f), Vector3.forward * 10f,
                Vector3.zero, Vector3.back * 10f
            });
            UpdateGroundProgress(router, new Vector3(.2f, 0f, 0f));
            report.Check(router.RemainingDistance > 49f, "A later crossing at the start cannot skip the loop");
            router.Stop();
            report.Check(!router.HasRoute && !router.Arrived && router.RoutePoints.Count == 0 && router.RemainingDistance == 0f,
                "Stop clears route geometry, arrival state, and remaining distance");
        }
        catch (Exception exception) { report.Check(false, exception.ToString()); }
        finally { UnityEngine.Object.DestroyImmediate(map); }
        report.Note("Synthetic progress checks inject a known route to isolate geometry from scene physics; public Build is separately tested below.");
        return report.Save("waypoint-synthetic-checks.txt");
    }

    public static string RunAssetGraphChecks()
    {
        var report = new Report("Saved Quad waypoint asset and registered destinations");
        var map = Resources.Load<QuadWaypointMap>("Quad_WaypointMap");
        var registry = Resources.Load<MapConfig>("Map_Settings");
        report.Check(map != null && registry != null, "Map and landmark registry are available");
        if (map == null || registry == null) return report.Save("waypoint-asset-checks.txt");
        report.Check(map.TryValidate(out string reason), "Map validates: " + reason);
        report.Check(map.overheadMap != null, "Overhead texture is assigned");
        report.Check(map.scenePath == SceneManager.GetActiveScene().path, "Map belongs to the current scene");
        report.Check(registry.landmarks.Count == 19 && map.destinations.Count == registry.landmarks.Count,
            "All 19 registered destinations have waypoint bindings");
        int spawn = map.FindDestination("Quad Respawn Point");
        report.Check(spawn >= 0, "Common Quad respawn node is registered");
        var points = new List<Vector3>();
        foreach (var landmark in registry.landmarks)
        {
            int goal = map.FindDestination(landmark.name);
            bool reached = goal >= 0 && spawn >= 0 && QuadWaypointRouter.TryFindGraphPath(map, spawn, goal, points, out _);
            float sum = 0f;
            if (reached) for (int i = 1; i < points.Count; i++) sum += Vector3.Distance(points[i - 1], points[i]);
            report.Check(reached && points.Count > 0 && (points[0] - map.nodes[spawn].position).sqrMagnitude < .0001f &&
                (points[points.Count - 1] - map.nodes[goal].position).sqrMagnitude < .0001f,
                landmark.name + ": complete graph connection from common respawn; route=" + sum.ToString("F1") + "m");
        }
        report.Note("Graph connectivity does not establish live obstacle clearance; spatial diagnostics exercise current scene physics.");
        return report.Save("waypoint-asset-checks.txt");
    }

    public static string RunEditorSpatialDiagnostics()
    {
        var report = new Report("Live-scene ground connectors and simulated waypoint walking");
        report.Note("This does not move the actual player or start/stop Play Mode. Editor/Link geometry checks are not physical Quest walking validation.");
        var map = Resources.Load<QuadWaypointMap>("Quad_WaypointMap");
        var registry = Resources.Load<MapConfig>("Map_Settings");
        string scenePath = SceneManager.GetActiveScene().path;
        if (map == null || registry == null || map.scenePath != scenePath || !map.TryValidate(out _))
        {
            report.Check(false, "Current scene and valid map/registry are required");
            return report.Save("waypoint-spatial-checks.txt");
        }
        int spawn = map.FindDestination("Quad Respawn Point");
        if (spawn < 0) { report.Check(false, "Common spawn node is unavailable"); return report.Save("waypoint-spatial-checks.txt"); }
        var navigator = UnityEngine.Object.FindObjectsByType<VRMapNavigator>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(item => item.gameObject.scene.path == scenePath && item.playerCamera != null);
        Transform ignoredRoot = null;
        if (navigator != null)
        {
            var character = navigator.playerCamera.GetComponentInParent<CharacterController>();
            ignoredRoot = character != null ? character.transform : navigator.playerCamera.root;
        }
        Vector3 headAtSpawn = map.nodes[spawn].position + Vector3.up * 1.7f;
        Physics.SyncTransforms();
        foreach (var landmark in registry.landmarks)
        {
            try
            {
                var router = new QuadWaypointRouter();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                bool built = router.Build(map, scenePath, headAtSpawn, ignoredRoot, landmark.name);
                watch.Stop();
                report.Check(built && router.HasRoute, landmark.name + ": supported and clear player connector; build=" +
                    watch.Elapsed.TotalMilliseconds.ToString("F2") + "ms; " + router.Instruction);
                if (!built) continue;
                var path = new List<Vector3>(router.RoutePoints);
                float now = Time.unscaledTime + .2f;
                float previous = router.RemainingDistance;
                bool monotonic = true, finite = true;
                int samples = 0;
                for (int i = 1; i < path.Count && !router.Arrived; i++)
                {
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(path[i - 1], path[i]) / .5f));
                    for (int step = 1; step <= steps && !router.Arrived; step++)
                    {
                        Vector3 head = Vector3.Lerp(path[i - 1], path[i], step / (float)steps) + Vector3.up * 1.7f;
                        now += .11f;
                        router.Update(head, now);
                        monotonic &= router.RemainingDistance <= previous + .025f;
                        finite &= !float.IsNaN(router.RemainingDistance) && !float.IsInfinity(router.RemainingDistance);
                        previous = router.RemainingDistance;
                        samples++;
                    }
                }
                report.Check(monotonic && finite && router.HasRoute && router.Arrived && router.RemainingDistance == 0f,
                    landmark.name + ": .5m/.11s simulated walk arrives with monotonic remaining distance; samples=" + samples + "; " + router.Instruction);
                router.Stop();
                report.Check(!router.HasRoute && router.RoutePoints.Count == 0, landmark.name + ": End clears the route");
            }
            catch (Exception exception) { report.Check(false, landmark.name + ": " + exception); }
        }
        return report.Save("waypoint-spatial-checks.txt");
    }

    private static QuadMapNode Node(string name, float x, float z) => new QuadMapNode { name = name, position = new Vector3(x, 0f, z) };
    private static QuadMapEdge Edge(int from, int to, params Vector3[] corners) => new QuadMapEdge { from = from, to = to, corners = corners };

    private static void SeedProgress(QuadWaypointRouter router, List<Vector3> points)
    {
        router.Stop();
        var route = (List<Vector3>)typeof(QuadWaypointRouter).GetField("route", Private).GetValue(router);
        var cumulative = (List<float>)typeof(QuadWaypointRouter).GetField("cumulative", Private).GetValue(router);
        route.AddRange(points);
        cumulative.Add(0f);
        for (int i = 1; i < route.Count; i++) cumulative.Add(cumulative[i - 1] + Vector3.Distance(route[i - 1], route[i]));
        typeof(QuadWaypointRouter).GetField("navigating", Private).SetValue(router, true);
    }

    private static void UpdateGroundProgress(QuadWaypointRouter router, Vector3 foot) =>
        typeof(QuadWaypointRouter).GetMethod("UpdateProgress", Private).Invoke(router, new object[] { foot });
}
#endif
