using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Native uGUI glass-style tile. Tint is the focused/app-badge color, Focus is 0..1,
/// Radius is in canvas units, and Circle makes a colored circular badge. Flat disables
/// the glass treatment for dots, using Graphic.color directly. Otherwise Graphic.color
/// is an overall tint/opacity. No camera texture or external material is required.
/// </summary>
[AddComponentMenu("UI/Ray-Ban Display/Surface")]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RayBanSurface : MaskableGraphic
{
    [SerializeField] private Color tint = new Color(0.12f, 0.65f, 0.95f, 1f);
    [SerializeField, Range(0f, 1f)] private float focus;
    [SerializeField, Min(0f)] private float radius = 32f;
    [SerializeField] private bool circle;
    [SerializeField] private bool flat;
    [SerializeField] private bool strongContrast;

    public Color Tint { get => tint; set { if (tint == value) return; tint = value; SetVerticesDirty(); } }
    public float Focus { get => focus; set { value = Mathf.Clamp01(value); if (Mathf.Approximately(focus, value)) return; focus = value; SetVerticesDirty(); } }
    public float Radius { get => radius; set { value = Mathf.Max(0f, value); if (Mathf.Approximately(radius, value)) return; radius = value; SetVerticesDirty(); } }
    public bool Circle { get => circle; set { if (circle == value) return; circle = value; SetVerticesDirty(); } }
    public bool Flat { get => flat; set { if (flat == value) return; flat = value; SetVerticesDirty(); } }
    public bool StrongContrast { get => strongContrast; set { if (strongContrast == value) return; strongContrast = value; SetVerticesDirty(); } }

    // This original mesh treatment is informed by the public toolkit's visual states,
    // not copied SVGs, fonts, shaders, or material source. The optical display itself
    // cannot be reproduced by a transparent uGUI mesh over Quest passthrough.
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect bounds = rectTransform.rect;
        if (bounds.width <= 0f || bounds.height <= 0f) return;
        if (circle)
        {
            float side = Mathf.Min(bounds.width, bounds.height);
            bounds = new Rect(bounds.center - Vector2.one * side * 0.5f, Vector2.one * side);
        }
        float corner = circle ? bounds.width * 0.5f : Mathf.Min(radius, Mathf.Min(bounds.width, bounds.height) * 0.5f);
        DrawFill(mesh, bounds, corner);
        if (!flat) DrawRim(mesh, bounds, corner);
    }

    private void DrawFill(VertexHelper mesh, Rect bounds, float corner)
    {
        const int gradientRows = 28, columns = 16, arcSegments = 12;
        var levels = new List<float>(gradientRows + 2 * arcSegments + 3);
        for (int row = 0; row <= gradientRows; row++)
            levels.Add(Mathf.Lerp(bounds.yMin, bounds.yMax, row / (float)gradientRows));
        // Match the rim's curved edge samples even on a tall detail card. Uniform
        // gradient rows alone leave visible wedges between the fill and its rim.
        for (int i = 1; i <= arcSegments; i++)
        {
            float inset = corner * (1f - Mathf.Cos(i * Mathf.PI * .5f / arcSegments));
            levels.Add(bounds.yMin + inset);
            levels.Add(bounds.yMax - inset);
        }
        levels.Sort();
        for (int i = levels.Count - 1; i > 0; i--)
            if (Mathf.Abs(levels[i] - levels[i - 1]) < .0001f) levels.RemoveAt(i);
        int rows = levels.Count - 1;
        for (int row = 0; row <= rows; row++)
        {
            float y = levels[row];
            float edge = 0f;
            if (corner > 0.001f)
            {
                float vertical = Mathf.Max(bounds.yMin + corner - y, y - (bounds.yMax - corner));
                if (vertical > 0f)
                    edge = corner - Mathf.Sqrt(Mathf.Max(0f, corner * corner - vertical * vertical));
            }
            for (int column = 0; column <= columns; column++)
            {
                Vector2 point = new Vector2(Mathf.Lerp(bounds.xMin + edge, bounds.xMax - edge, column / (float)columns), y);
                AddVertex(mesh, point, FillColor(bounds, point));
            }
        }
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int first = row * (columns + 1) + column;
                mesh.AddTriangle(first, first + columns + 1, first + 1);
                mesh.AddTriangle(first + 1, first + columns + 1, first + columns + 2);
            }
        }
    }

    private Color FillColor(Rect bounds, Vector2 point)
    {
        if (flat) return color;
        float u = (point.x - bounds.xMin) / bounds.width;
        float v = (point.y - bounds.yMin) / bounds.height;
        float active = circle ? 1f : focus;
        if (strongContrast && !circle)
        {
            Color restingContrast = new Color(.045f, .065f, .095f, 1f);
            Color focusedContrast = Color.Lerp(tint, Color.black, .62f);
            var resultContrast = Color.Lerp(restingContrast, focusedContrast, active) * color;
            resultContrast.a = Mathf.Max(.96f, color.a);
            return resultContrast;
        }
        Color resting = new Color(0.42f, 0.49f, 0.57f, 0.54f);
        float distance = new Vector2((u - 0.18f) * 0.80f, (v - 0.88f) * 1.12f).magnitude;
        Color glow = Color.Lerp(tint, Color.white, 0.56f);
        Color middle = Color.Lerp(tint, new Color(0.02f, 0.15f, 0.24f), 0.18f);
        Color dark = Color.Lerp(tint, new Color(0.02f, 0.08f, 0.17f), 0.67f);
        Color selected = distance < 0.32f
            ? Color.Lerp(glow, middle, distance / 0.32f)
            : Color.Lerp(middle, dark, Mathf.Clamp01((distance - 0.32f) / 0.90f));
        selected.a = circle ? 0.96f : 0.92f;
        Color result = Color.Lerp(resting, selected, active);
        float sheen = Mathf.Exp(-((u - 0.2f) * (u - 0.2f) * 8f + (v - 0.94f) * (v - 0.94f) * 50f));
        result = Color.Lerp(result, new Color(0.93f, 0.98f, 1f, result.a), sheen * Mathf.Lerp(0.06f, 0.18f, active));
        return result * color;
    }

    private void DrawRim(VertexHelper mesh, Rect bounds, float corner)
    {
        const int arcSegments = 12;
        const int count = 4 * (arcSegments + 1);
        float active = circle ? 1f : focus;
        float thickness = Mathf.Min(strongContrast ? Mathf.Lerp(1.5f, 3f, active) : Mathf.Lerp(0.65f, 1.8f, active), Mathf.Min(bounds.width, bounds.height) * 0.08f);
        Rect inside = new Rect(bounds.xMin + thickness, bounds.yMin + thickness,
            Mathf.Max(0f, bounds.width - thickness * 2f), Mathf.Max(0f, bounds.height - thickness * 2f));
        int start = mesh.currentVertCount;
        for (int i = 0; i < count; i++)
        {
            Vector2 outer = RoundedPoint(bounds, corner, i, arcSegments);
            Vector2 inner = RoundedPoint(inside, Mathf.Max(0f, corner - thickness), i, arcSegments);
            float u = (outer.x - bounds.xMin) / bounds.width;
            float v = (outer.y - bounds.yMin) / bounds.height;
            float highlight = Mathf.Exp(-((u - 0.13f) * (u - 0.13f) * 3.8f + (v - 0.92f) * (v - 0.92f) * 3.0f));
            Color rim = new Color(0.92f, 0.97f, 1f, Mathf.Lerp(0.045f, 0.14f, active) + highlight * Mathf.Lerp(0.08f, 0.68f, active));
            if (strongContrast) rim.a = Mathf.Lerp(.45f, 1f, active);
            AddVertex(mesh, outer, rim * color);
            AddVertex(mesh, inner, rim * color);
        }
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            mesh.AddTriangle(start + i * 2, start + next * 2, start + i * 2 + 1);
            mesh.AddTriangle(start + i * 2 + 1, start + next * 2, start + next * 2 + 1);
        }
    }

    private static Vector2 RoundedPoint(Rect bounds, float corner, int index, int segments)
    {
        int quadrant = index / (segments + 1);
        float angle = (quadrant * 90f + index % (segments + 1) * 90f / segments) * Mathf.Deg2Rad;
        Vector2 centre = new Vector2(quadrant == 0 || quadrant == 3 ? bounds.xMax - corner : bounds.xMin + corner,
            quadrant < 2 ? bounds.yMax - corner : bounds.yMin + corner);
        return centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * corner;
    }

    private static void AddVertex(VertexHelper mesh, Vector2 point, Color tintColor)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = point;
        vertex.color = tintColor;
        vertex.uv0 = Vector2.zero;
        mesh.AddVert(vertex);
    }
}
