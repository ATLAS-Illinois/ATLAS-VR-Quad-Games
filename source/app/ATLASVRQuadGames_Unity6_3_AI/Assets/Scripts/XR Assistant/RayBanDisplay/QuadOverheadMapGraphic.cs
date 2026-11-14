using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// North-up X/Z map overlay over a static, scene-authored texture. It is decorative:
/// the existing gesture focus remains on the four Map controls and Back.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class QuadOverheadMapGraphic : MaskableGraphic
{
    [SerializeField] private RawImage background;
    private readonly List<Vector2> route = new List<Vector2>();
    private QuadWaypointMap map;
    private Rect viewport;
    private Vector2 player, destination, forward;
    private bool hasPlayer, hasDestination, hasRoute, viewportReady;
    private int selection = -1;
    private float nextRefresh;

    public Rect WorldViewport => viewport;

    public void SetBackground(RawImage image)
    {
        background = image;
        if (background != null) background.raycastTarget = false;
        raycastTarget = false;
    }

    public void ResetViewport()
    {
        viewportReady = false;
        nextRefresh = 0;
    }

#if UNITY_EDITOR
    // Unity's Canvas clone retains serialized labels/images but not this runtime
    // geometry. Preview tooling copies presentation state without leasing services.
    public void ClonePresentationStateFrom(QuadOverheadMapGraphic source)
    {
        if (source == null) return;
        map = source.map;
        viewport = source.viewport;
        player = source.player;
        destination = source.destination;
        forward = source.forward;
        hasPlayer = source.hasPlayer;
        hasDestination = source.hasDestination;
        hasRoute = source.hasRoute;
        viewportReady = source.viewportReady;
        selection = source.selection;
        route.Clear();
        route.AddRange(source.route);
        raycastTarget = false;
        UpdateBackground();
        SetVerticesDirty();
    }
#endif

    public void SetState(QuadWaypointMap data, IReadOnlyList<Vector3> points,
        Vector3 playerPosition, bool showPlayer, Vector3 playerForward,
        Vector3 selectedPosition, bool showDestination, bool showRoute, int selectedIndex)
    {
        bool changed = data != map || selection != selectedIndex || hasRoute != showRoute ||
            hasPlayer != showPlayer || hasDestination != showDestination;
        if (!changed && viewportReady && Application.isPlaying && Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .1f;
        if (changed) viewportReady = false;
        map = data;
        selection = selectedIndex;
        player = XZ(playerPosition);
        destination = XZ(selectedPosition);
        forward = XZ(playerForward);
        if (!Finite(forward) || forward.sqrMagnitude < .0001f) forward = Vector2.up;
        else forward.Normalize();
        hasPlayer = showPlayer && Finite(player);
        hasDestination = showDestination && Finite(destination);
        hasRoute = showRoute;
        route.Clear();
        if (showRoute && points != null)
            for (int i = 0; i < points.Count && i < 4096; i++)
            {
                Vector2 point = XZ(points[i]);
                if (Finite(point)) route.Add(point);
            }
        FitViewport();
        UpdateBackground();
        SetVerticesDirty();
    }

    private void FitViewport()
    {
        bool any = false;
        Vector2 low = Vector2.zero, high = Vector2.zero;
        if (hasPlayer) Include(player, ref low, ref high, ref any);
        if (hasDestination) Include(destination, ref low, ref high, ref any);
        for (int i = 0; i < route.Count; i++) Include(route[i], ref low, ref high, ref any);
        if (!any)
        {
            Rect world = map != null && ValidRect(map.worldRect) ? map.worldRect : new Rect(-100, -100, 200, 200);
            low = world.min;
            high = world.max;
        }

        // Refit only when a destination/route changes or geometry leaves the safe
        // interior. Progress along a route does not constantly zoom the map in.
        Rect safe = new Rect(viewport.xMin + viewport.width * .08f, viewport.yMin + viewport.height * .08f,
            viewport.width * .84f, viewport.height * .84f);
        if (viewportReady && safe.Contains(low) && safe.Contains(high)) return;
        float aspect = Mathf.Max(.1f, rectTransform.rect.width / Mathf.Max(1f, rectTransform.rect.height));
        Vector2 extent = high - low + Vector2.one * 28f;
        // Reserve screen-space room for the 18px destination pin even when a
        // long route makes a fixed world-space margin only a handful of pixels.
        float usableWidth = Mathf.Max(.25f, 1f - 48f / Mathf.Max(1f, rectTransform.rect.width));
        float usableHeight = Mathf.Max(.25f, 1f - 48f / Mathf.Max(1f, rectTransform.rect.height));
        float height = Mathf.Max(80f, extent.y / usableHeight, extent.x / (aspect * usableWidth));
        if (viewportReady) height = Mathf.Max(height, viewport.height);
        Vector2 size = new Vector2(height * aspect, height);
        viewport = new Rect((low + high - size) * .5f, size);
        viewportReady = true;
    }

    private void UpdateBackground()
    {
        if (background == null) return;
        bool available = map != null && map.overheadMap != null && ValidRect(map.worldRect) && viewportReady;
        background.enabled = available;
        if (!available) return;
        background.texture = map.overheadMap;
        Rect world = map.worldRect;
        Rect visible = Rect.MinMaxRect(Mathf.Max(world.xMin, viewport.xMin), Mathf.Max(world.yMin, viewport.yMin),
            Mathf.Min(world.xMax, viewport.xMax), Mathf.Min(world.yMax, viewport.yMax));
        if (visible.width <= 0 || visible.height <= 0) { background.enabled = false; return; }
        background.uvRect = new Rect((visible.xMin - world.xMin) / world.width,
            (visible.yMin - world.yMin) / world.height, visible.width / world.width, visible.height / world.height);

        // Keep outside-map space as the backing color instead of stretching/clamping
        // the border texels. Map, route and both markers share this exact transform.
        Rect local = rectTransform.rect;
        Vector2 topLeft = WorldToLocal(new Vector2(visible.xMin, visible.yMax));
        var imageRect = background.rectTransform;
        imageRect.anchorMin = imageRect.anchorMax = new Vector2(0, 1);
        imageRect.pivot = new Vector2(0, 1);
        imageRect.anchoredPosition = new Vector2(topLeft.x - local.xMin, topLeft.y - local.yMax);
        imageRect.sizeDelta = new Vector2(visible.width / viewport.width * local.width,
            visible.height / viewport.height * local.height);
    }

    public Vector2 WorldToLocal(Vector2 worldXZ)
    {
        Rect local = rectTransform.rect;
        if (!viewportReady || !ValidRect(viewport)) return local.center;
        return new Vector2(local.xMin + (worldXZ.x - viewport.xMin) / viewport.width * local.width,
            local.yMin + (worldXZ.y - viewport.yMin) / viewport.height * local.height);
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (!viewportReady) return;
        Rect clip = rectTransform.rect;
        if (hasRoute)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                float width = pass == 0 ? 8f : 4f;
                Color32 tint = pass == 0 ? new Color32(13, 39, 48, 245) : new Color32(67, 237, 219, 255);
                for (int i = 1; i < route.Count; i++)
                {
                    Vector2 a = WorldToLocal(route[i - 1]), b = WorldToLocal(route[i]);
                    if (ClipSegment(ref a, ref b, clip)) Line(mesh, a, b, width, tint);
                }
                for (int i = 1; i < route.Count - 1; i++)
                {
                    Vector2 point = WorldToLocal(route[i]);
                    if (clip.Contains(point)) Join(mesh, WorldToLocal(route[i - 1]), point,
                        WorldToLocal(route[i + 1]), width, tint);
                }
            }
        }
        if (hasDestination)
        {
            Vector2 target = WorldToLocal(destination);
            if (clip.Contains(target))
            {
                Color32 outline = new Color32(28, 41, 44, 255), gold = new Color32(255, 208, 87, 255);
                Triangle(mesh, target + new Vector2(-8, 8), target + new Vector2(8, 8), target + new Vector2(0, -3), outline);
                Triangle(mesh, target + new Vector2(-5, 8), target + new Vector2(5, 8), target, gold);
                Circle(mesh, target + Vector2.up * 8, 10, outline);
                Circle(mesh, target + Vector2.up * 8, 7, gold);
                Circle(mesh, target + Vector2.up * 8, 2.5f, outline);
            }
        }
        if (hasPlayer)
        {
            Vector2 position = WorldToLocal(player);
            if (clip.Contains(position))
            {
                Circle(mesh, position, 12, new Color32(15, 34, 54, 255));
                Circle(mesh, position, 9, new Color32(37, 145, 250, 255));
                Vector2 right = new Vector2(forward.y, -forward.x);
                Triangle(mesh, position + forward * 16, position - forward * 6 + right * 7,
                    position - forward * 6 - right * 7, new Color32(255, 255, 255, 255));
            }
        }
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        ResetViewport();
    }

    private static Vector2 XZ(Vector3 point) => new Vector2(point.x, point.z);
    private static bool Finite(Vector2 point) => !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
        !float.IsNaN(point.y) && !float.IsInfinity(point.y);
    private static bool ValidRect(Rect rect) => rect.width > .001f && rect.height > .001f && Finite(rect.min) && Finite(rect.max);
    private static void Include(Vector2 point, ref Vector2 low, ref Vector2 high, ref bool any)
    {
        if (!any) { low = high = point; any = true; }
        else { low = Vector2.Min(low, point); high = Vector2.Max(high, point); }
    }

    private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color32 tint)
    {
        Vector2 direction = b - a;
        if (direction.sqrMagnitude < .001f) return;
        Vector2 offset = new Vector2(-direction.y, direction.x).normalized * width * .5f;
        int first = mesh.currentVertCount;
        Vertex(mesh, a - offset, tint); Vertex(mesh, a + offset, tint);
        Vertex(mesh, b + offset, tint); Vertex(mesh, b - offset, tint);
        mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
    }

    private static void Circle(VertexHelper mesh, Vector2 centre, float radius, Color32 tint)
    {
        int first = mesh.currentVertCount;
        Vertex(mesh, centre, tint);
        const int sides = 16;
        for (int i = 0; i < sides; i++)
        {
            float angle = i * (Mathf.PI * 2 / sides);
            Vertex(mesh, centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint);
        }
        for (int i = 0; i < sides; i++) mesh.AddTriangle(first, first + 1 + i, first + 1 + ((i + 1) % sides));
    }

    private static void Join(VertexHelper mesh, Vector2 a, Vector2 point, Vector2 b, float width, Color32 tint)
    {
        Vector2 incoming = point - a, outgoing = b - point;
        if (incoming.sqrMagnitude < .001f || outgoing.sqrMagnitude < .001f) return;
        incoming.Normalize();
        outgoing.Normalize();
        float cross = incoming.x * outgoing.y - incoming.y * outgoing.x;
        if (Mathf.Abs(cross) < .0001f) return;
        // Cover only the outer wedge; inner stroke quads already overlap. A
        // three-vertex bevel keeps all 4096 supported points under uGUI's 65K cap.
        float side = cross > 0 ? -1f : 1f;
        Vector2 before = new Vector2(-incoming.y, incoming.x) * (width * .5f * side);
        Vector2 after = new Vector2(-outgoing.y, outgoing.x) * (width * .5f * side);
        Triangle(mesh, point, point + before, point + after, tint);
    }

    private static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color32 tint)
    {
        int first = mesh.currentVertCount;
        Vertex(mesh, a, tint); Vertex(mesh, b, tint); Vertex(mesh, c, tint);
        mesh.AddTriangle(first, first + 1, first + 2);
    }

    private static void Vertex(VertexHelper mesh, Vector2 position, Color32 tint)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = tint;
        vertex.uv0 = Vector2.zero;
        mesh.AddVert(vertex);
    }

    // Clip first, avoiding huge off-map mesh geometry for invalid/remote points.
    public static bool ClipSegment(ref Vector2 a, ref Vector2 b, Rect rect)
    {
        Vector2 delta = b - a;
        float low = 0, high = 1;
        if (!Clip(-delta.x, a.x - rect.xMin, ref low, ref high) ||
            !Clip(delta.x, rect.xMax - a.x, ref low, ref high) ||
            !Clip(-delta.y, a.y - rect.yMin, ref low, ref high) ||
            !Clip(delta.y, rect.yMax - a.y, ref low, ref high)) return false;
        b = a + high * delta;
        a += low * delta;
        return true;
    }

    private static bool Clip(float p, float q, ref float low, ref float high)
    {
        if (Mathf.Abs(p) < .000001f) return q >= 0;
        float t = q / p;
        if (p < 0) { if (t > high) return false; if (t > low) low = t; }
        else { if (t < low) return false; if (t < high) high = t; }
        return true;
    }
}
