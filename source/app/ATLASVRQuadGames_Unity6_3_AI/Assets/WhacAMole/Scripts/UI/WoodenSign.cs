using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Whacamole.UI
{
    /// <summary>
    /// A wooden notice board on two posts (like the quad's signposts by the archery range), built from
    /// simple shapes at runtime: planks with a darker frame, a little roof plank on top, and posts that
    /// reach down to the ground. Text is TextMeshPro on the front of the board; small 3D icons (the acorn
    /// key) can be added too. The front of the board faces local -Z, the same way the old scoreboard text faced.
    /// The wood texture is generated in code, so no art assets are needed; pass a material to use real wood instead.
    /// </summary>
    public class WoodenSign : MonoBehaviour
    {
        public const float BoardDepth = 0.035f;
        private const float FrameWidth = 0.05f;
        private const float PostWidth = 0.07f;

        // Wood tints (multiplied with the grain texture).
        public static readonly Color PlankColor = new Color(0.62f, 0.4f, 0.22f);
        public static readonly Color FrameColor = new Color(0.36f, 0.22f, 0.11f);
        public static readonly Color PostColor = new Color(0.42f, 0.27f, 0.14f);
        /// <summary>Cream "painted" lettering, readable on the wood.</summary>
        public static readonly Color InkColor = new Color(1f, 0.95f, 0.84f);

        private static Material woodAcross, woodUp, paint;
        private static MaterialPropertyBlock block;

        public Vector2 Size { get; private set; }

        private readonly List<Transform> spinners = new List<Transform>();
        private Material woodOverride;

        /// <summary>
        /// Builds a sign as a child of parent. localPos is the center of the board; yaw turns it about the vertical.
        /// size is the board's face (width, height) in meters, not counting the frame.
        /// </summary>
        public static WoodenSign Build(Transform parent, string name, Vector3 localPos, float yaw, Vector2 size,
                                       Material wood = null, bool posts = true)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var sign = root.AddComponent<WoodenSign>();
            sign.woodOverride = wood;
            sign.Size = size;
            sign.BuildBody(posts);
            return sign;
        }

        private void Update()
        {
            // Key icons turn slowly so they read as 3D acorns.
            float step = 60f * Time.deltaTime;
            foreach (Transform t in spinners)
                if (t != null) t.Rotate(0f, step, 0f, Space.Self);
        }

        // ---------- Building ----------

        private void BuildBody(bool withPosts)
        {
            float w = Size.x, h = Size.y;

            // Planks, stacked with tiny gaps and slightly different shades.
            int planks = Mathf.Max(2, Mathf.RoundToInt(h / 0.14f));
            float plankH = h / planks;
            for (int i = 0; i < planks; i++)
            {
                float y = -h / 2f + plankH * (i + 0.5f);
                float shade = 0.9f + 0.2f * Mathf.PerlinNoise(i * 1.7f + transform.position.x, 0.37f);
                Piece("Plank", new Vector3(0f, y, 0f), new Vector3(w, plankH - 0.006f, BoardDepth),
                      PlankColor * shade, false);
            }
            // A solid backing so you can't see through the gaps between planks.
            Piece("Backing", new Vector3(0f, 0f, BoardDepth * 0.5f + 0.004f), new Vector3(w, h, 0.008f), FrameColor * 0.8f, false);

            // Frame.
            float fz = 0.004f;
            Piece("FrameTop", new Vector3(0f, h / 2f + FrameWidth / 2f, fz), new Vector3(w + 2f * FrameWidth, FrameWidth, BoardDepth + 0.02f), FrameColor, false);
            Piece("FrameBottom", new Vector3(0f, -h / 2f - FrameWidth / 2f, fz), new Vector3(w + 2f * FrameWidth, FrameWidth, BoardDepth + 0.02f), FrameColor, false);
            Piece("FrameLeft", new Vector3(-w / 2f - FrameWidth / 2f, 0f, fz), new Vector3(FrameWidth, h, BoardDepth + 0.02f), FrameColor, true);
            Piece("FrameRight", new Vector3(w / 2f + FrameWidth / 2f, 0f, fz), new Vector3(FrameWidth, h, BoardDepth + 0.02f), FrameColor, true);

            // Little roof plank on top.
            Piece("Roof", new Vector3(0f, h / 2f + FrameWidth + 0.02f, 0.01f), new Vector3(w + 0.22f, 0.04f, 0.16f), FrameColor * 0.9f, false);

            if (withPosts) BuildPosts();
        }

        private void BuildPosts()
        {
            float w = Size.x, h = Size.y;
            float topLocal = h / 2f + FrameWidth;

            // Find the ground under the board (fall back to the parent's height, e.g. the Arena on the ground).
            Vector3 bottomWorld = transform.TransformPoint(new Vector3(0f, -h / 2f - FrameWidth - 0.02f, BoardDepth + 0.05f));
            float groundY = transform.parent != null ? transform.parent.position.y : bottomWorld.y - 1.4f;
            if (Physics.Raycast(bottomWorld, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                groundY = hit.point.y;

            float groundLocal = groundY - transform.position.y; // signs are only turned about the vertical
            float length = topLocal - groundLocal;
            if (length <= 0.05f) return;

            float x = w / 2f - 0.1f;
            float z = BoardDepth / 2f + 0.008f + PostWidth / 2f;
            float yCenter = (topLocal + groundLocal) / 2f;
            Piece("PostLeft", new Vector3(-x, yCenter, z), new Vector3(PostWidth, length, PostWidth), PostColor, true);
            Piece("PostRight", new Vector3(x, yCenter, z), new Vector3(PostWidth, length, PostWidth), PostColor, true);
        }

        private Transform Piece(string pieceName, Vector3 pos, Vector3 scale, Color tint, bool vertical)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); // keeps its collider: you can't walk through signs
            go.name = pieceName;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (woodOverride != null)
            {
                r.sharedMaterial = woodOverride;
            }
            else
            {
                r.sharedMaterial = vertical ? GetWoodUp() : GetWoodAcross();
                SetColor(r, tint);
            }
            return go.transform;
        }

        // ---------- Content ----------

        /// <summary>
        /// Adds TextMeshPro text on the front of the board. area is in board coordinates (meters from the
        /// center; x, y = center of the box). The text auto-sizes to fit the box, up to maxFontSize.
        /// Returns null if TextMeshPro has no default font (TMP Essentials not imported).
        /// </summary>
        public TextMeshPro AddText(string textName, Rect area, float maxFontSize, TextAlignmentOptions alignment,
                                   Color color, string text = "")
        {
            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning("[WoodenSign] TextMeshPro has no default font (Window > TextMeshPro > Import TMP Essential Resources).", this);
                return null;
            }

            var go = new GameObject(textName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(area.x, area.y, -BoardDepth / 2f - 0.003f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = new Vector2(area.width, area.height);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f;
            tmp.fontSizeMax = maxFontSize;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.richText = true;
            tmp.text = text;
            return tmp;
        }

        public enum IconKind { Nut, Bomb, Acorn }

        /// <summary>Adds a small 3D nut / bomb / acorn sticking out of the board at (x, y), slowly turning.</summary>
        public Transform AddIcon(IconKind kind, Vector2 pos, float size, Color bodyColor)
        {
            var icon = new GameObject("Icon_" + kind).transform;
            icon.SetParent(transform, false);
            icon.localPosition = new Vector3(pos.x, pos.y, -BoardDepth / 2f - size * 0.55f);

            Color capColor, stemColor;
            switch (kind)
            {
                case IconKind.Bomb: capColor = new Color(0.9f, 0.05f, 0.05f); stemColor = new Color(1f, 0.75f, 0.2f); break;
                case IconKind.Acorn: capColor = new Color(1f, 0.8f, 0.25f); stemColor = capColor * 0.8f; break;
                default: capColor = new Color(0.35f, 0.2f, 0.08f); stemColor = capColor; break;
            }

            IconPart(icon, PrimitiveType.Sphere, new Vector3(0f, -size * 0.08f, 0f), new Vector3(size * 0.8f, size * 0.9f, size * 0.8f), bodyColor);
            IconPart(icon, PrimitiveType.Sphere, new Vector3(0f, size * 0.3f, 0f), new Vector3(size * 0.9f, size * 0.42f, size * 0.9f), capColor);
            IconPart(icon, PrimitiveType.Cylinder, new Vector3(0f, size * 0.55f, 0f), new Vector3(size * 0.12f, size * 0.1f, size * 0.12f), stemColor);

            spinners.Add(icon);
            return icon;
        }

        private static void IconPart(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = GetPaint();
            SetColor(r, color);
        }

        // ---------- Materials ----------

        private static void SetColor(Renderer r, Color c)
        {
            if (block == null) block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_Color", c);
            r.SetPropertyBlock(block);
        }

        private static Shader LitShader()
        {
            Shader s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Legacy Shaders/Diffuse");
            if (s == null) s = Shader.Find("Sprites/Default");
            return s;
        }

        private static Material GetPaint()
        {
            if (paint == null)
            {
                paint = new Material(LitShader()) { name = "SignPaint (runtime)" };
                if (paint.HasProperty("_Glossiness")) paint.SetFloat("_Glossiness", 0.45f);
            }
            return paint;
        }

        private static Material GetWoodAcross()
        {
            if (woodAcross == null) woodAcross = MakeWood(false);
            return woodAcross;
        }

        private static Material GetWoodUp()
        {
            if (woodUp == null) woodUp = MakeWood(true);
            return woodUp;
        }

        private static Material MakeWood(bool vertical)
        {
            var m = new Material(LitShader()) { name = vertical ? "SignWoodUp (runtime)" : "SignWood (runtime)" };
            m.mainTexture = MakeGrainTexture(vertical);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.12f);
            return m;
        }

        /// <summary>Grayscale wood grain (tinted by the material color). Grain runs along U, or along V if vertical.</summary>
        private static Texture2D MakeGrainTexture(bool vertical)
        {
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true)
            {
                name = "WoodGrain (runtime)",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    // along = direction of the grain, across = across it.
                    float along = (vertical ? y : x) / (float)N;
                    float across = (vertical ? x : y) / (float)N;

                    // Wavy growth rings plus fine streaks.
                    float warp = Mathf.PerlinNoise(along * 2.2f, across * 3.1f) * 1.4f;
                    float ring = Mathf.Repeat(across * 9f + warp, 1f);
                    float rings = Mathf.Pow(Mathf.Abs(ring * 2f - 1f), 3f);
                    float streak = Mathf.PerlinNoise(along * 1.5f + 13f, across * 60f);

                    // A soft knot or two.
                    float kx = along - 0.68f, ky = (across - 0.4f) * 3f;
                    float knot = Mathf.Clamp01(1f - Mathf.Sqrt(kx * kx + ky * ky) * 9f);

                    float v = 1f - 0.22f * rings - 0.12f * streak - 0.35f * knot * knot;
                    byte b = (byte)(Mathf.Clamp01(v) * 255f);
                    px[y * N + x] = new Color32(b, b, b, 255);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
