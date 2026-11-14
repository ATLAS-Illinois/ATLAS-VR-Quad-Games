using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class QuadMapNode
{
    public string name;
    public Vector3 position;
}

[Serializable]
public sealed class QuadMapEdge
{
    public int from;
    public int to;
    // Stored in from-to order, including both endpoint positions.
    public Vector3[] corners;
}

[Serializable]
public sealed class QuadMapDestination
{
    public string landmarkName;
    public int nodeIndex;
}

/// <summary>Offline, scene-authored map. Coordinates are Unity world X/Z, not GPS.</summary>
[CreateAssetMenu(menuName = "ATLAS VR/Quad Waypoint Map")]
public sealed class QuadWaypointMap : ScriptableObject
{
    public string scenePath;
    public Texture2D overheadMap;
    public Rect worldRect;
    // Conservative building footprints: proximity scripts may disable their
    // colliders, but a connector must never cut across the visible building.
    public Rect[] blockedRegions = new Rect[0];
    public List<QuadMapNode> nodes = new List<QuadMapNode>();
    public List<QuadMapEdge> edges = new List<QuadMapEdge>();
    public List<QuadMapDestination> destinations = new List<QuadMapDestination>();

    public int FindDestination(string landmarkName)
    {
        if (destinations == null) return -1;
        foreach (var destination in destinations)
            if (destination != null && string.Equals(destination.landmarkName, landmarkName, StringComparison.Ordinal))
                return destination.nodeIndex;
        return -1;
    }

    public bool TryGetDestination(string landmarkName, out Vector3 position)
    {
        int index = FindDestination(landmarkName);
        if (nodes != null && index >= 0 && index < nodes.Count && nodes[index] != null)
        {
            position = nodes[index].position;
            return true;
        }
        position = default;
        return false;
    }

    public Vector2 WorldToUV(Vector3 position) => new Vector2(
        worldRect.width > 0f ? (position.x - worldRect.xMin) / worldRect.width : .5f,
        worldRect.height > 0f ? (position.z - worldRect.yMin) / worldRect.height : .5f);

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(scenePath) || nodes == null || nodes.Count == 0 || edges == null || destinations == null)
            return Invalid("The waypoint map is incomplete", out error);
        if (!Finite(worldRect.x) || !Finite(worldRect.y) || !Finite(worldRect.width) ||
            !Finite(worldRect.height) || worldRect.width <= 0f || worldRect.height <= 0f)
            return Invalid("The map bounds are invalid", out error);
        if (blockedRegions != null)
            foreach (var region in blockedRegions)
                if (!Finite(region.x) || !Finite(region.y) || !Finite(region.width) || !Finite(region.height) ||
                    region.width <= 0f || region.height <= 0f)
                    return Invalid("A building footprint is invalid", out error);
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i] == null || !Finite(nodes[i].position))
                return Invalid("A waypoint position is invalid", out error);
        foreach (var edge in edges)
        {
            if (edge == null || edge.from < 0 || edge.to < 0 || edge.from >= nodes.Count ||
                edge.to >= nodes.Count || edge.from == edge.to || edge.corners == null || edge.corners.Length < 2)
                return Invalid("A waypoint connection is invalid", out error);
            foreach (var corner in edge.corners)
                if (!Finite(corner)) return Invalid("A path coordinate is invalid", out error);
            if (Vector3.Distance(edge.corners[0], nodes[edge.from].position) > .15f ||
                Vector3.Distance(edge.corners[edge.corners.Length - 1], nodes[edge.to].position) > .15f)
                return Invalid("A path endpoint does not match its waypoint", out error);
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var destination in destinations)
            if (destination == null || string.IsNullOrWhiteSpace(destination.landmarkName) ||
                !names.Add(destination.landmarkName) || destination.nodeIndex < 0 || destination.nodeIndex >= nodes.Count)
                return Invalid("A landmark connection is invalid", out error);
        error = "";
        return true;
    }

    public static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Invalid(string message, out string error) { error = message; return false; }
}
