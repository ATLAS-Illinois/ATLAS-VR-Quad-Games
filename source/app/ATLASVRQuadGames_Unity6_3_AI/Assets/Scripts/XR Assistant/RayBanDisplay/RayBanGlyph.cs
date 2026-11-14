using UnityEngine;
using UnityEngine.UI;

/// <summary>Original geometric uGUI symbols. Symbol selects a name; Graphic.color supplies tint.</summary>
[AddComponentMenu("UI/Ray-Ban Display/Glyph")]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RayBanGlyph : MaskableGraphic
{
    [SerializeField] private string symbol = "Assistant";
    public string Symbol { get => symbol; set { if (symbol == value) return; symbol = value; SetVerticesDirty(); } }
    private Rect bounds;
    private float scale;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        bounds = rectTransform.rect;
        scale = Mathf.Min(bounds.width, bounds.height) / 100f;
        if (scale <= 0f) return;
        string name = (symbol ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", string.Empty);
        switch (name)
        {
            case "whatsapp":
                Arc(mesh, 50, 53, 34, 225, 540, 5.5f);
                Line(mesh, 16, 53, 13, 17, 5.5f);
                Line(mesh, 13, 17, 26, 29, 5.5f);
                Receiver(mesh, 0.67f);
                break;
            case "instagram":
                RoundBox(mesh, 14, 14, 72, 72, 21, 6);
                Ring(mesh, 50, 50, 17, 6);
                Disc(mesh, 72, 73, 4.5f);
                break;
            case "messenger":
                Arc(mesh, 50, 54, 35, -135, 180, 6);
                Line(mesh, 15, 54, 16, 19, 6);
                Line(mesh, 16, 19, 25, 29, 6);
                Polygon(mesh, new Vector2(25, 43), new Vector2(45, 65), new Vector2(56, 51),
                    new Vector2(76, 64), new Vector2(55, 39), new Vector2(44, 52));
                break;
            case "messages":
                RoundBox(mesh, 11, 38, 65, 47, 17, 5);
                Line(mesh, 27, 38, 23, 28, 5);
                RoundBox(mesh, 29, 19, 61, 47, 17, 6);
                Line(mesh, 73, 19, 82, 10, 6);
                break;
            case "calls": case "phone":
                Receiver(mesh, 1f);
                break;
            case "camera":
                RoundBox(mesh, 11, 19, 78, 59, 12, 6);
                Line(mesh, 29, 78, 36, 88, 6);
                Line(mesh, 36, 88, 61, 88, 6);
                Line(mesh, 61, 88, 69, 78, 6);
                Ring(mesh, 50, 49, 17, 6);
                Disc(mesh, 77, 66, 3.5f);
                break;
            case "music":
                Line(mesh, 39, 29, 39, 76, 7);
                Line(mesh, 74, 36, 74, 84, 7);
                Polygon(mesh, new Vector2(36, 75), new Vector2(77, 86), new Vector2(77, 72), new Vector2(36, 61));
                Oval(mesh, 28, 25, 14, 10);
                Oval(mesh, 63, 32, 14, 10);
                break;
            case "photos": case "gallery":
                RoundBox(mesh, 16, 18, 70, 69, 11, 6);
                Line(mesh, 10, 70, 10, 11, 4);
                Line(mesh, 10, 11, 70, 11, 4);
                Disc(mesh, 67, 68, 7);
                Polygon(mesh, new Vector2(23, 27), new Vector2(44, 56), new Vector2(60, 36),
                    new Vector2(69, 46), new Vector2(80, 27));
                break;
            case "captions": case "cc":
                RoundBox(mesh, 9, 22, 82, 56, 13, 5);
                Arc(mesh, 35, 50, 13, 45, 315, 6);
                Arc(mesh, 66, 50, 13, 45, 315, 6);
                break;
            case "maps": case "map":
                Arc(mesh, 50, 62, 25, 0, 180, 7);
                Line(mesh, 25, 62, 28, 48, 7);
                Line(mesh, 28, 48, 50, 13, 7);
                Line(mesh, 50, 13, 72, 48, 7);
                Line(mesh, 72, 48, 75, 62, 7);
                Ring(mesh, 50, 62, 9, 5);
                break;
            case "tutorials": case "book":
                Polygon(mesh, new Vector2(11, 20), new Vector2(11, 82), new Vector2(41, 78), new Vector2(46, 72), new Vector2(46, 15), new Vector2(35, 20));
                Polygon(mesh, new Vector2(54, 15), new Vector2(54, 72), new Vector2(59, 78), new Vector2(89, 82), new Vector2(89, 20), new Vector2(65, 20));
                break;
            case "settings":
                Ring(mesh, 50, 50, 25, 10);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 0.25f;
                    Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Line(mesh, 50 + d.x * 26, 50 + d.y * 26, 50 + d.x * 36, 50 + d.y * 36, 10);
                }
                Ring(mesh, 50, 50, 8, 5);
                break;
            case "volume": case "speaker":
                Polygon(mesh, new Vector2(13, 37), new Vector2(28, 37), new Vector2(47, 20), new Vector2(47, 80), new Vector2(28, 63), new Vector2(13, 63));
                Arc(mesh, 42, 50, 27, -52, 52, 6);
                Arc(mesh, 42, 50, 42, -48, 48, 6);
                break;
            case "brightness": case "sun":
                Ring(mesh, 50, 50, 16, 6);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 0.25f;
                    Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Line(mesh, 50 + d.x * 29, 50 + d.y * 29, 50 + d.x * 39, 50 + d.y * 39, 5);
                }
                break;
            case "headphones": case "audioonly":
                Arc(mesh, 50, 49, 32, 0, 180, 7);
                RoundBox(mesh, 15, 19, 17, 37, 7, 7);
                RoundBox(mesh, 68, 19, 17, 37, 7, 7);
                break;
            case "bell": case "donotdisturb":
                Arc(mesh, 50, 61, 22, 0, 180, 6);
                Line(mesh, 28, 61, 26, 34, 6);
                Line(mesh, 26, 34, 17, 26, 6);
                Line(mesh, 17, 26, 83, 26, 6);
                Line(mesh, 83, 26, 74, 34, 6);
                Line(mesh, 74, 34, 72, 61, 6);
                Arc(mesh, 50, 24, 9, 195, 345, 6);
                Line(mesh, 50, 85, 50, 89, 6);
                break;
            case "battery":
                RoundBox(mesh, 8, 28, 77, 44, 8, 5);
                Line(mesh, 91, 42, 91, 58, 6);
                Polygon(mesh, new Vector2(17, 37), new Vector2(66, 37), new Vector2(66, 63), new Vector2(17, 63));
                break;
            case "glasses":
                RoundBox(mesh, 7, 28, 35, 35, 12, 5);
                RoundBox(mesh, 58, 28, 35, 35, 12, 5);
                Arc(mesh, 50, 48, 9, 0, 180, 5);
                Line(mesh, 7, 61, 1, 70, 5);
                Line(mesh, 93, 61, 99, 70, 5);
                break;
            case "person": case "avatar":
                Disc(mesh, 50, 68, 16);
                Arc(mesh, 50, 19, 29, 0, 180, 14);
                Line(mesh, 21, 19, 79, 19, 10);
                break;
            case "calendar": case "reminder":
                RoundBox(mesh, 15, 13, 70, 68, 10, 6);
                Line(mesh, 17, 61, 83, 61, 6);
                Line(mesh, 33, 76, 33, 89, 7);
                Line(mesh, 67, 76, 67, 89, 7);
                Star(mesh, 50, 37, 15);
                break;
            case "play":
                Polygon(mesh, new Vector2(29, 17), new Vector2(81, 50), new Vector2(29, 83));
                break;
            case "pause":
                Polygon(mesh, new Vector2(24, 18), new Vector2(40, 18), new Vector2(40, 82), new Vector2(24, 82));
                Polygon(mesh, new Vector2(60, 18), new Vector2(76, 18), new Vector2(76, 82), new Vector2(60, 82));
                break;
            case "next":
                Polygon(mesh, new Vector2(21, 21), new Vector2(65, 50), new Vector2(21, 79));
                Line(mesh, 78, 23, 78, 77, 9);
                break;
            case "previous":
                Polygon(mesh, new Vector2(79, 21), new Vector2(35, 50), new Vector2(79, 79));
                Line(mesh, 22, 23, 22, 77, 9);
                break;
            case "plus":
                Line(mesh, 20, 50, 80, 50, 7);
                Line(mesh, 50, 20, 50, 80, 7);
                break;
            case "minus":
                Line(mesh, 20, 50, 80, 50, 7);
                break;
            case "mic": case "microphone":
                RoundBox(mesh, 36, 39, 28, 48, 14, 6);
                Arc(mesh, 50, 44, 26, 180, 360, 6);
                Line(mesh, 24, 44, 24, 56, 6);
                Line(mesh, 76, 44, 76, 56, 6);
                Line(mesh, 50, 18, 50, 8, 6);
                Line(mesh, 34, 8, 66, 8, 6);
                break;
            case "send":
                Polygon(mesh, new Vector2(12, 44), new Vector2(88, 86), new Vector2(57, 10),
                    new Vector2(44, 39));
                break;
            case "heart":
                Polygon(mesh, new Vector2(50, 15), new Vector2(20, 43), new Vector2(13, 56),
                    new Vector2(13, 67), new Vector2(19, 78), new Vector2(29, 84),
                    new Vector2(40, 84), new Vector2(50, 75), new Vector2(60, 84),
                    new Vector2(71, 84), new Vector2(81, 78), new Vector2(87, 67),
                    new Vector2(87, 56), new Vector2(80, 43));
                break;
            case "route":
                Ring(mesh, 24, 19, 8, 5);
                Line(mesh, 24, 28, 24, 49, 6);
                Line(mesh, 24, 49, 74, 49, 6);
                Line(mesh, 74, 49, 74, 78, 6);
                Line(mesh, 61, 68, 74, 81, 6);
                Line(mesh, 74, 81, 87, 68, 6);
                break;
            case "up":
                Line(mesh, 50, 18, 50, 82, 7);
                Line(mesh, 24, 57, 50, 83, 7);
                Line(mesh, 50, 83, 76, 57, 7);
                break;
            case "back":
                Line(mesh, 59, 18, 27, 50, 7);
                Line(mesh, 27, 50, 59, 82, 7);
                break;
            case "check":
                Line(mesh, 17, 46, 39, 23, 8);
                Line(mesh, 39, 23, 84, 77, 8);
                break;
            case "close": case "x":
                Line(mesh, 24, 24, 76, 76, 7);
                Line(mesh, 24, 76, 76, 24, 7);
                break;
            default:
                Star(mesh, 42, 54, 28);
                Star(mesh, 77, 78, 13);
                Star(mesh, 69, 23, 15);
                break;
        }
    }

    private void Receiver(VertexHelper mesh, float size)
    {
        Vector2[] points = { new Vector2(28, 75), new Vector2(23, 67), new Vector2(28, 52),
            new Vector2(40, 37), new Vector2(55, 26), new Vector2(68, 23), new Vector2(77, 29) };
        for (int i = 1; i < points.Length; i++)
        {
            Vector2 a = Vector2.one * 50 + (points[i - 1] - Vector2.one * 50) * size;
            Vector2 b = Vector2.one * 50 + (points[i] - Vector2.one * 50) * size;
            Line(mesh, a.x, a.y, b.x, b.y, 12 * size);
        }
        Line(mesh, 50 + (27 - 50) * size, 50 + (76 - 50) * size,
            50 + (35 - 50) * size, 50 + (65 - 50) * size, 15 * size);
        Line(mesh, 50 + (66 - 50) * size, 50 + (34 - 50) * size,
            50 + (77 - 50) * size, 50 + (27 - 50) * size, 15 * size);
    }

    private void Star(VertexHelper mesh, float x, float y, float radius)
    {
        int first = mesh.currentVertCount;
        Vertex(mesh, x, y);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            float length = i % 2 == 0 ? radius : radius * 0.30f;
            Vertex(mesh, x + Mathf.Cos(angle) * length, y + Mathf.Sin(angle) * length);
        }
        for (int i = 0; i < 8; i++) mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % 8);
    }

    private void RoundBox(VertexHelper mesh, float x, float y, float width, float height, float r, float stroke)
    {
        r = Mathf.Min(r, Mathf.Min(width, height) * 0.5f);
        Line(mesh, x + r, y, x + width - r, y, stroke, false);
        Line(mesh, x + r, y + height, x + width - r, y + height, stroke, false);
        Line(mesh, x, y + r, x, y + height - r, stroke, false);
        Line(mesh, x + width, y + r, x + width, y + height - r, stroke, false);
        Arc(mesh, x + width - r, y + height - r, r, 0, 90, stroke);
        Arc(mesh, x + r, y + height - r, r, 90, 180, stroke);
        Arc(mesh, x + r, y + r, r, 180, 270, stroke);
        Arc(mesh, x + width - r, y + r, r, 270, 360, stroke);
    }

    private void Ring(VertexHelper mesh, float x, float y, float radius, float width) => Arc(mesh, x, y, radius, 0, 360, width);

    private void Arc(VertexHelper mesh, float x, float y, float radius, float start, float end, float width)
    {
        int segments = Mathf.Max(3, Mathf.CeilToInt(Mathf.Abs(end - start) / 10f));
        int first = mesh.currentVertCount;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(start, end, i / (float)segments) * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
            Vertex(mesh, x + cosine * (radius + width * 0.5f), y + sine * (radius + width * 0.5f));
            Vertex(mesh, x + cosine * Mathf.Max(0, radius - width * 0.5f), y + sine * Mathf.Max(0, radius - width * 0.5f));
        }
        for (int i = 0; i < segments; i++)
        {
            int v = first + i * 2;
            mesh.AddTriangle(v, v + 2, v + 1);
            mesh.AddTriangle(v + 1, v + 2, v + 3);
        }
    }

    private void Disc(VertexHelper mesh, float x, float y, float radius) => Oval(mesh, x, y, radius, radius);

    private void Oval(VertexHelper mesh, float x, float y, float rx, float ry)
    {
        const int segments = 24;
        int first = mesh.currentVertCount;
        Vertex(mesh, x, y);
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vertex(mesh, x + Mathf.Cos(a) * rx, y + Mathf.Sin(a) * ry);
        }
        for (int i = 0; i < segments; i++) mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % segments);
    }

    private void Line(VertexHelper mesh, float ax, float ay, float bx, float by, float width, bool caps = true)
    {
        Vector2 a = new Vector2(ax, ay), b = new Vector2(bx, by);
        Vector2 delta = b - a;
        if (delta.sqrMagnitude < 0.001f) return;
        Vector2 normal = new Vector2(-delta.y, delta.x).normalized * width * 0.5f;
        int first = mesh.currentVertCount;
        Vertex(mesh, a.x - normal.x, a.y - normal.y);
        Vertex(mesh, a.x + normal.x, a.y + normal.y);
        Vertex(mesh, b.x + normal.x, b.y + normal.y);
        Vertex(mesh, b.x - normal.x, b.y - normal.y);
        mesh.AddTriangle(first, first + 1, first + 2);
        mesh.AddTriangle(first, first + 2, first + 3);
        if (caps) { Disc(mesh, ax, ay, width * 0.5f); Disc(mesh, bx, by, width * 0.5f); }
    }

    // Ear clipping keeps the original lightning/book/speaker silhouettes concave,
    // without filling the notches as a triangle fan would.
    private void Polygon(VertexHelper mesh, params Vector2[] points)
    {
        if (points.Length < 3) return;
        int[] remaining = new int[points.Length];
        float area = 0f;
        int first = mesh.currentVertCount;
        for (int i = 0; i < points.Length; i++)
        {
            remaining[i] = i;
            Vertex(mesh, points[i].x, points[i].y);
            area += Cross(points[i], points[(i + 1) % points.Length]);
        }
        float winding = area >= 0f ? 1f : -1f;
        int count = points.Length;
        int guard = count * count;
        while (count > 2 && guard-- > 0)
        {
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                int previous = remaining[(i + count - 1) % count];
                int current = remaining[i];
                int next = remaining[(i + 1) % count];
                Vector2 a = points[previous], b = points[current], c = points[next];
                if (Cross(b - a, c - b) * winding <= 0.0001f) continue;
                bool occupied = false;
                for (int j = 0; j < count; j++)
                {
                    int candidate = remaining[j];
                    if (candidate == previous || candidate == current || candidate == next) continue;
                    Vector2 p = points[candidate];
                    if (Cross(b - a, p - a) * winding >= -0.0001f &&
                        Cross(c - b, p - b) * winding >= -0.0001f &&
                        Cross(a - c, p - c) * winding >= -0.0001f)
                    { occupied = true; break; }
                }
                if (occupied) continue;
                mesh.AddTriangle(first + previous, first + current, first + next);
                for (int j = i; j < count - 1; j++) remaining[j] = remaining[j + 1];
                count--;
                found = true;
                break;
            }
            if (!found) break;
        }
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private void Vertex(VertexHelper mesh, float x, float y)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = bounds.center + new Vector2(x - 50, y - 50) * scale;
        vertex.color = color;
        vertex.uv0 = Vector2.zero;
        mesh.AddVert(vertex);
    }
}
