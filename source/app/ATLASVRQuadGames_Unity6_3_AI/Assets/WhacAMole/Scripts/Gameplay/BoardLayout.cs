using System.Collections.Generic;
using UnityEngine;

namespace Whacamole.Gameplay
{
    /// <summary>
    /// Lays out the whac-a-mole board for a level: how many rows and columns of holes, how big the
    /// table is, and how big the nuts are. New holes and nuts are copies of the ones already in the
    /// scene (Hole_0 / Nut_0), so no prefab needs assigning.
    ///
    /// Lives on the WhackBoard object (the parent of Base, Top and Holes). NutSpawner adds it
    /// automatically if it's missing; add it yourself if you want to tweak the spacing in the Inspector.
    /// The front edge of the table stays where it is; extra rows grow away from the player, so keep
    /// rows at 4 or less to stay within arm's reach (the back row is ~0.65 m from the front edge at 4 rows).
    /// Only call Apply while every nut is hidden (NutSpawner does this between levels).
    /// </summary>
    public class BoardLayout : MonoBehaviour
    {
        [Tooltip("Distance between hole centers, left to right (m).")]
        [SerializeField, Min(0.1f)] private float spacingX = 0.24f;
        [Tooltip("Distance between hole centers, front to back (m).")]
        [SerializeField, Min(0.1f)] private float spacingZ = 0.16f;
        [Tooltip("Extra table width beyond the outer hole centers (m, total for both sides).")]
        [SerializeField, Min(0f)] private float widthMargin = 0.36f;
        [Tooltip("Extra table depth beyond the front and back hole centers (m, total).")]
        [SerializeField, Min(0f)] private float depthMargin = 0.32f;
        [Tooltip("Hole diameter at full nut size (m). Holes shrink along with the nuts.")]
        [SerializeField, Min(0.02f)] private float holeDiameter = 0.14f;

        [Header("Player Blocking")]
        [Tooltip("Stop the player walking through the table. Uses an invisible box (PlayerBlocker) the size of the table " +
                 "pulled in by the player's body radius, so you can stand right at the edge and still reach the back row.")]
        [SerializeField] private bool blockPlayer = true;
        [Tooltip("How far (m) the invisible box is pulled in from each table edge. -1 = the player's CharacterController radius " +
                 "(the body then stops exactly at the table edge). Smaller = you stop further back.")]
        [SerializeField] private float blockerInset = -1f;

        public int Rows { get; private set; }
        public int Columns { get; private set; }
        public float NutScale { get; private set; } = 1f;

        private Transform baseBlock, top, holesParent;
        private Nut nutTemplate;
        private Transform holeTemplate;
        private Vector3 baseNutScale = Vector3.one;
        private float topHeight;   // height of the table top surface (local)
        private float frontZ;      // local z of the table's front edge (closest to the player)
        private float centerX;
        private bool initialized;

        private BoxCollider blocker;
        private float playerRadius = -1f;

        private readonly List<Nut> nuts = new List<Nut>();
        private readonly List<Transform> holes = new List<Transform>();

        private void Start()
        {
            // Block the player from the start, before any game (and any level resize) has happened.
            if (Initialize()) UpdatePlayerBlocker();
        }

        /// <summary>Rebuilds the board. Returns false if the board parts couldn't be found.</summary>
        public bool Apply(int rows, int columns, float nutScale)
        {
            if (!Initialize()) return false;

            rows = Mathf.Max(1, rows);
            columns = Mathf.Max(1, columns);
            int count = rows * columns;

            // Make sure there are enough nuts and holes, copying the originals when needed.
            while (nuts.Count < count)
            {
                Nut copy = Instantiate(nutTemplate, holesParent);
                copy.name = $"Nut_{nuts.Count}";
                nuts.Add(copy);
            }
            while (holes.Count < count)
            {
                Transform copy = Instantiate(holeTemplate, holesParent);
                copy.name = $"Hole_{holes.Count}";
                holes.Add(copy);
            }

            float width = (columns - 1) * spacingX + widthMargin;
            float depth = (rows - 1) * spacingZ + depthMargin;
            float firstRowZ = frontZ + depthMargin / 2f;
            float centerZ = frontZ + depth / 2f;

            for (int i = 0; i < Mathf.Max(nuts.Count, holes.Count); i++)
            {
                bool used = i < count;
                int row = i / columns, col = i % columns;
                var pos = new Vector3(centerX + (col - (columns - 1) / 2f) * spacingX, topHeight, firstRowZ + row * spacingZ);

                if (i < nuts.Count)
                {
                    Nut nut = nuts[i];
                    if (used)
                    {
                        if (!nut.gameObject.activeSelf) nut.gameObject.SetActive(true); // re-registers with the GameManager
                        nut.transform.localScale = baseNutScale * nutScale;
                        nut.SetHome(i, pos);
                    }
                    else
                    {
                        nut.gameObject.SetActive(false); // unregisters from the GameManager
                    }
                }

                if (i < holes.Count)
                {
                    Transform hole = holes[i];
                    hole.gameObject.SetActive(used);
                    if (used)
                    {
                        hole.localPosition = pos + new Vector3(0f, 0.001f, 0f);
                        float d = holeDiameter * Mathf.Max(nutScale, 0.6f);
                        hole.localScale = new Vector3(d, hole.localScale.y, d);
                    }
                }
            }

            // Resize the table around the holes, keeping the front edge in place.
            if (baseBlock != null)
            {
                baseBlock.localPosition = new Vector3(centerX, baseBlock.localPosition.y, centerZ);
                baseBlock.localScale = new Vector3(width, baseBlock.localScale.y, depth);
            }
            if (top != null)
            {
                top.localPosition = new Vector3(centerX, top.localPosition.y, centerZ);
                top.localScale = new Vector3(width + 0.04f, top.localScale.y, depth + 0.04f);
            }

            UpdatePlayerBlocker();

            Rows = rows;
            Columns = columns;
            NutScale = nutScale;
            return true;
        }

        /// <summary>
        /// Sizes the invisible PlayerBlocker box to the current table. The table's own colliders are off
        /// (they kept the player's body a capsule-radius away from the edge); this box is the table pulled in
        /// by that radius on every side, so the body stops right at the edge but can't pass through.
        /// It's solid but only matters to the player: nuts only react to hammers.
        /// </summary>
        public void UpdatePlayerBlocker()
        {
            if (!blockPlayer || baseBlock == null)
            {
                if (blocker != null) blocker.enabled = false;
                return;
            }

            if (blocker == null)
            {
                Transform existing = transform.Find("PlayerBlocker");
                GameObject go = existing != null ? existing.gameObject : new GameObject("PlayerBlocker");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                blocker = go.GetComponent<BoxCollider>();
                if (blocker == null) blocker = go.AddComponent<BoxCollider>();
            }

            float inset = blockerInset >= 0f ? blockerInset : GetPlayerRadius();
            float width = baseBlock.localScale.x;
            float depth = baseBlock.localScale.z;

            blocker.enabled = true;
            blocker.isTrigger = false;
            blocker.center = new Vector3(baseBlock.localPosition.x, topHeight / 2f, baseBlock.localPosition.z);
            blocker.size = new Vector3(Mathf.Max(0.02f, width - 2f * inset),
                                       Mathf.Max(0.02f, topHeight),
                                       Mathf.Max(0.02f, depth - 2f * inset));
        }

        // Radius of the player's body capsule (OVRPlayerController's CharacterController), in world meters.
        private float GetPlayerRadius()
        {
            if (playerRadius >= 0f) return playerRadius;
            CharacterController body = FindFirstObjectByType<CharacterController>();
            if (body == null) return 0.3f; // no walking body in this scene (e.g. the standalone test scene)
            Vector3 s = body.transform.lossyScale;
            playerRadius = body.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
            return playerRadius;
        }

        private bool Initialize()
        {
            if (initialized) return true;

            baseBlock = transform.Find("Base");
            top = transform.Find("Top");
            holesParent = transform.Find("Holes");
            if (holesParent == null)
            {
                Debug.LogError("[BoardLayout] No 'Holes' child under " + name + ".", this);
                return false;
            }

            foreach (Transform child in holesParent)
            {
                Nut nut = child.GetComponent<Nut>();
                if (nut != null) nuts.Add(nut);
                else if (child.name.StartsWith("Hole_")) holes.Add(child);
            }
            nuts.Sort((a, b) => a.HoleIndex.CompareTo(b.HoleIndex));
            holes.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            if (nuts.Count == 0 || holes.Count == 0)
            {
                Debug.LogError("[BoardLayout] Need at least one nut and one Hole_ object under Holes to copy from.", this);
                return false;
            }

            nutTemplate = nuts[0];
            holeTemplate = holes[0];
            baseNutScale = nutTemplate.transform.localScale;

            // Read the table's current shape so the layout matches whatever Build Scene made.
            if (top != null) topHeight = top.localPosition.y + top.localScale.y / 2f;
            else topHeight = holeTemplate.localPosition.y - 0.001f;

            if (baseBlock != null)
            {
                frontZ = baseBlock.localPosition.z - baseBlock.localScale.z / 2f;
                centerX = baseBlock.localPosition.x;
            }
            else
            {
                frontZ = holeTemplate.localPosition.z - depthMargin / 2f;
                centerX = 0f;
            }

            initialized = true;
            return true;
        }
    }
}
