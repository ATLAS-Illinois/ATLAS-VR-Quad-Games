using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Routes through the authored undirected graph. Only the short player-to-network
/// connection is discovered at runtime, using supported ground and body clearance.
/// This supplies guidance and never moves the player or changes locomotion.
/// </summary>
public sealed class QuadWaypointRouter
{
    private const float ConnectorRadius = 24f;
    private const int MaximumConnectorAttempts = 12;
    private const float CapsuleRadius = .28f;
    private const float CapsuleHeight = 1.65f;
    private const float GroundOffset = .05f;
    private const float ArrivalRadius = 1.6f;
    private const float VerticalTolerance = 1.25f;
    private const float DeviationDistance = 4f;
    private const float RecalculateInterval = 2f;

    private readonly List<Vector3> route = new List<Vector3>();
    private readonly List<float> cumulative = new List<float>();
    private readonly List<Vector3> connector = new List<Vector3>();
    private readonly List<Candidate> candidates = new List<Candidate>();
    private readonly RaycastHit[] groundHits = new RaycastHit[64];
    private readonly Collider[] overlaps = new Collider[64];
    private QuadWaypointMap data;
    private string scenePath;
    private string destinationName;
    private Transform playerRoot;
    private Vector3 lastAttemptPosition;
    private Vector3 lastPlayerPosition;
    private float lastAttemptTime;
    private float lastUpdateTime;
    private float progress;
    private int segment;
    private bool navigating;

    private struct Candidate { public int node; public float cost; }

    public IReadOnlyList<Vector3> RoutePoints => route;
    public bool HasRoute => navigating && route.Count >= 2;
    public bool Arrived { get; private set; }
    public float RemainingDistance { get; private set; }
    public string Instruction { get; private set; } = "Choose a Quad landmark";
    public Vector3 GuidancePosition => HasRoute ? route[Mathf.Min(segment + 1, route.Count - 1)] : Vector3.zero;

    public bool Build(QuadWaypointMap map, string currentScenePath, Vector3 playerPosition,
        Transform ignoredPlayerRoot, string landmarkName)
    {
        data = map;
        scenePath = currentScenePath;
        destinationName = landmarkName;
        playerRoot = ignoredPlayerRoot;
        navigating = true;
        return Rebuild(playerPosition, Time.unscaledTime);
    }

    public void Stop()
    {
        navigating = false;
        ClearRoute();
        Instruction = "Choose a Quad landmark";
    }

    private void ClearRoute()
    {
        route.Clear();
        cumulative.Clear();
        progress = 0f;
        segment = 0;
        Arrived = false;
        RemainingDistance = 0f;
    }

    private bool Fail(string message)
    {
        ClearRoute();
        Instruction = message;
        return false;
    }

    private bool Rebuild(Vector3 playerPosition, float now)
    {
        ClearRoute();
        lastAttemptTime = now;
        lastUpdateTime = now;
        lastAttemptPosition = lastPlayerPosition = playerPosition;
        if (data == null) return Fail("No waypoint map is configured");
        if (!string.Equals(data.scenePath, scenePath, StringComparison.Ordinal))
            return Fail("This map belongs to a different scene");
        if (!data.TryValidate(out string validationError)) return Fail(validationError);
        int destination = data.FindDestination(destinationName);
        if (destination < 0) return Fail("No pathway is configured for this landmark");
        if (!TryGroundBelowPlayer(playerPosition, out Vector3 foot))
            return Fail("Return to ground level to find a path");

        BuildTree(data, destination, out float[] costs, out int[] nextNodes, out int[] nextEdges);
        candidates.Clear();
        for (int i = 0; i < data.nodes.Count; i++)
        {
            Vector3 node = data.nodes[i].position;
            float distance = HorizontalDistance(foot, node);
            if (!float.IsInfinity(costs[i]) && distance <= ConnectorRadius && Mathf.Abs(node.y - foot.y) <= 3f)
                candidates.Add(new Candidate { node = i, cost = costs[i] + distance });
        }
        candidates.Sort((a, b) => a.cost.CompareTo(b.cost));
        int start = -1;
        for (int i = 0; i < Mathf.Min(candidates.Count, MaximumConnectorAttempts); i++)
        {
            int candidate = candidates[i].node;
            if (!TryConnector(foot, data.nodes[candidate].position, connector)) continue;
            start = candidate;
            foreach (var point in connector) AppendUnique(route, point);
            break;
        }
        if (start < 0) return Fail("No clear connection to a path. Move closer to a walkway");
        if (!AppendTreeRoute(data, start, destination, nextNodes, nextEdges, route))
            return Fail("No connected pathway reaches this landmark");
        // A zero-length route still has a visible destination and can arrive.
        if (route.Count == 1) route.Add(route[0]);
        cumulative.Add(0f);
        for (int i = 1; i < route.Count; i++)
            cumulative.Add(cumulative[i - 1] + Vector3.Distance(route[i - 1], route[i]));
        RemainingDistance = cumulative[cumulative.Count - 1];
        Instruction = "Follow the highlighted path";
        UpdateProgress(foot);
        return true;
    }

    public void Update(Vector3 playerPosition, float now)
    {
        if (!navigating || now - lastUpdateTime < .1f) return;
        lastUpdateTime = now;
        if (!QuadWaypointMap.Finite(playerPosition)) return;
        if (!HasRoute)
        {
            if (now - lastAttemptTime >= RecalculateInterval && Vector3.Distance(playerPosition, lastAttemptPosition) > 3f)
                Rebuild(playerPosition, now);
            return;
        }
        if (!TryGroundBelowPlayer(playerPosition, out Vector3 foot))
        {
            Instruction = "Return to ground level to continue";
            return;
        }
        float jumped = Vector3.Distance(playerPosition, lastPlayerPosition);
        lastPlayerPosition = playerPosition;
        // Consume adjacent waypoints before checking deviation. Otherwise a
        // low-frame-rate step can miss a short connector waypoint and appear
        // off route despite still walking forward along the same corridor.
        if (jumped <= 12f) AdvanceSegments(foot);
        Vector3 nearest = ProjectToSegment(foot, route[segment], route[segment + 1], out float t);
        float rawProgress = cumulative[segment] + t * (cumulative[segment + 1] - cumulative[segment]);
        bool deviated = jumped > 12f || HorizontalDistance(foot, nearest) > DeviationDistance ||
            Mathf.Abs(foot.y - nearest.y) > VerticalTolerance || rawProgress < progress - DeviationDistance;
        if (deviated)
        {
            Arrived = false;
            Instruction = "Off route — recalculating";
            if (now - lastAttemptTime >= RecalculateInterval) Rebuild(playerPosition, now);
            return;
        }
        UpdateProgress(foot);
    }

    private void UpdateProgress(Vector3 foot)
    {
        AdvanceSegments(foot);
        ProjectToSegment(foot, route[segment], route[segment + 1], out float t);
        progress = Mathf.Max(progress, cumulative[segment] + t * (cumulative[segment + 1] - cumulative[segment]));
        RemainingDistance = Mathf.Max(0f, cumulative[cumulative.Count - 1] - progress);
        Vector3 goal = route[route.Count - 1];
        Arrived = RemainingDistance <= 3f && HorizontalDistance(foot, goal) <= ArrivalRadius &&
            Mathf.Abs(foot.y - goal.y) <= VerticalTolerance;
        if (Arrived)
        {
            RemainingDistance = 0f;
            Instruction = "You have arrived";
        }
        else Instruction = "Follow the highlighted path";
    }

    private void AdvanceSegments(Vector3 foot)
    {
        // Advance sequentially through endpoints, never by choosing a globally
        // nearby later path segment (which can skip loops or parallel paths).
        while (segment < route.Count - 2)
        {
            Vector3 from = route[segment], to = route[segment + 1];
            if (Mathf.Abs(foot.y - to.y) > VerticalTolerance) break;
            Vector3 flatDelta = new Vector3(to.x - from.x, 0f, to.z - from.z);
            Vector3 flatOffset = new Vector3(foot.x - from.x, 0f, foot.z - from.z);
            float squaredLength = flatDelta.sqrMagnitude;
            float along = squaredLength > .00001f ? Vector3.Dot(flatOffset, flatDelta) / squaredLength : 1f;
            bool passed = along >= 1f && (flatOffset - flatDelta * along).magnitude <= 1.5f;
            Vector3 currentNearest = ProjectToSegment(foot, from, to, out _);
            Vector3 nextNearest = ProjectToSegment(foot, to, route[segment + 2], out _);
            bool roundedCorner = HorizontalDistance(foot, to) <= 1.5f &&
                HorizontalDistance(foot, nextNearest) + .01f < HorizontalDistance(foot, currentNearest);
            if (!passed && !roundedCorner) break;
            progress = Mathf.Max(progress, cumulative[segment + 1]);
            segment++;
        }
    }

    private bool TryGroundBelowPlayer(Vector3 playerPosition, out Vector3 ground)
    {
        ground = default;
        if (!QuadWaypointMap.Finite(playerPosition)) return false;
        return TryGround(playerPosition + Vector3.up * .4f, 4.4f, out ground);
    }

    private bool TryGround(Vector3 origin, float distance, out Vector3 ground)
    {
        ground = default;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, distance, ~0, QueryTriggerInteraction.Ignore);
        if (count == groundHits.Length) return false; // Do not trust a truncated hit set.
        float closest = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (Ignore(hit.collider) || hit.normal.y < .7f || hit.distance >= closest) continue;
            closest = hit.distance;
            ground = hit.point + Vector3.up * GroundOffset;
            found = true;
        }
        return found;
    }

    private bool TryConnector(Vector3 start, Vector3 end, List<Vector3> points)
    {
        points.Clear();
        if (CrossesBlockedRegion(data, start, end)) return false;
        float distance = HorizontalDistance(start, end);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / .75f));
        Vector3 previous = start;
        for (int i = 0; i <= steps; i++)
        {
            Vector3 expected = Vector3.Lerp(start, end, i / (float)steps);
            if (!TryGround(expected + Vector3.up * .8f, 1.6f, out Vector3 grounded) ||
                Mathf.Abs(grounded.y - expected.y) > .55f ||
                (i > 0 && Mathf.Abs(grounded.y - previous.y) > .35f) || !BodyClear(grounded)) return false;
            if (i > 0 && !SweepClear(previous, grounded)) return false;
            AppendUnique(points, grounded);
            previous = grounded;
        }
        // Avoid a final hidden snap from a different surface into the graph.
        if (Vector3.Distance(previous, end) > .2f) return false;
        AppendUnique(points, end);
        return true;
    }

    public static bool CrossesBlockedRegion(QuadWaypointMap map, Vector3 start, Vector3 end)
    {
        if (map == null || map.blockedRegions == null) return false;
        foreach (Rect footprint in map.blockedRegions)
        {
            float entry = 0f, exit = 1f;
            if (ClipAxis(start.x, end.x - start.x, footprint.xMin - .35f, footprint.xMax + .35f, ref entry, ref exit) &&
                ClipAxis(start.z, end.z - start.z, footprint.yMin - .35f, footprint.yMax + .35f, ref entry, ref exit)) return true;
        }
        return false;
    }

    private static bool ClipAxis(float origin, float direction, float min, float max, ref float entry, ref float exit)
    {
        if (Mathf.Abs(direction) < .00001f) return origin >= min && origin <= max;
        float first = (min - origin) / direction, last = (max - origin) / direction;
        if (first > last) { float swap = first; first = last; last = swap; }
        entry = Mathf.Max(entry, first);
        exit = Mathf.Min(exit, last);
        return entry <= exit;
    }

    private bool BodyClear(Vector3 ground)
    {
        int count = Physics.OverlapCapsuleNonAlloc(ground + Vector3.up * (CapsuleRadius + .08f),
            ground + Vector3.up * (CapsuleHeight - CapsuleRadius), CapsuleRadius,
            overlaps, ~0, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) return false;
        for (int i = 0; i < count; i++) if (!Ignore(overlaps[i])) return false;
        return true;
    }

    private bool SweepClear(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float length = delta.magnitude;
        if (length < .001f) return true;
        int count = Physics.CapsuleCastNonAlloc(from + Vector3.up * (CapsuleRadius + .08f),
            from + Vector3.up * (CapsuleHeight - CapsuleRadius), CapsuleRadius, delta / length,
            groundHits, length, ~0, QueryTriggerInteraction.Ignore);
        if (count == groundHits.Length) return false;
        for (int i = 0; i < count; i++) if (!Ignore(groundHits[i].collider)) return false;
        return true;
    }

    private bool Ignore(Collider collider) => collider == null ||
        (playerRoot != null && (collider.transform == playerRoot || collider.transform.IsChildOf(playerRoot)));

    /// <summary>Pure graph search for validation and reuse; no physics or player movement.</summary>
    public static bool TryFindGraphPath(QuadWaypointMap map, int start, int destination, List<Vector3> points, out float length)
    {
        points.Clear();
        length = 0f;
        if (map == null || !map.TryValidate(out _) || start < 0 || destination < 0 ||
            start >= map.nodes.Count || destination >= map.nodes.Count) return false;
        BuildTree(map, destination, out float[] costs, out int[] nextNodes, out int[] nextEdges);
        if (float.IsInfinity(costs[start]) || !AppendTreeRoute(map, start, destination, nextNodes, nextEdges, points)) return false;
        length = costs[start];
        return true;
    }

    private static void BuildTree(QuadWaypointMap map, int destination, out float[] costs, out int[] nextNodes, out int[] nextEdges)
    {
        int count = map.nodes.Count;
        costs = new float[count];
        nextNodes = new int[count];
        nextEdges = new int[count];
        var visited = new bool[count];
        var adjacency = new List<int>[count];
        var weights = new float[map.edges.Count];
        for (int i = 0; i < count; i++) { costs[i] = float.PositiveInfinity; nextNodes[i] = nextEdges[i] = -1; adjacency[i] = new List<int>(); }
        for (int i = 0; i < map.edges.Count; i++)
        {
            var edge = map.edges[i];
            adjacency[edge.from].Add(i);
            adjacency[edge.to].Add(i);
            for (int j = 1; j < edge.corners.Length; j++) weights[i] += Vector3.Distance(edge.corners[j - 1], edge.corners[j]);
        }
        costs[destination] = 0f;
        for (int iteration = 0; iteration < count; iteration++)
        {
            int current = -1;
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++) if (!visited[i] && costs[i] < best) { best = costs[i]; current = i; }
            if (current < 0) break;
            visited[current] = true;
            foreach (int edgeIndex in adjacency[current])
            {
                var edge = map.edges[edgeIndex];
                int other = edge.from == current ? edge.to : edge.from;
                float cost = best + weights[edgeIndex];
                if (cost >= costs[other]) continue;
                costs[other] = cost;
                nextNodes[other] = current;
                nextEdges[other] = edgeIndex;
            }
        }
    }

    private static bool AppendTreeRoute(QuadWaypointMap map, int start, int destination,
        int[] nextNodes, int[] nextEdges, List<Vector3> output)
    {
        AppendUnique(output, map.nodes[start].position);
        int current = start;
        for (int hops = 0; current != destination && hops < map.nodes.Count; hops++)
        {
            int edgeIndex = nextEdges[current];
            if (edgeIndex < 0 || nextNodes[current] < 0) return false;
            var edge = map.edges[edgeIndex];
            if (edge.from == current)
                for (int i = 0; i < edge.corners.Length; i++) AppendUnique(output, edge.corners[i]);
            else
                for (int i = edge.corners.Length - 1; i >= 0; i--) AppendUnique(output, edge.corners[i]);
            current = nextNodes[current];
        }
        return current == destination;
    }

    private static void AppendUnique(List<Vector3> output, Vector3 point)
    {
        if (output.Count == 0 || (output[output.Count - 1] - point).sqrMagnitude > .0001f) output.Add(point);
    }

    private static Vector3 ProjectToSegment(Vector3 point, Vector3 from, Vector3 to, out float t)
    {
        Vector3 delta = to - from;
        float lengthSquared = delta.sqrMagnitude;
        t = lengthSquared > .00001f ? Mathf.Clamp01(Vector3.Dot(point - from, delta) / lengthSquared) : 0f;
        return from + t * delta;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
}
