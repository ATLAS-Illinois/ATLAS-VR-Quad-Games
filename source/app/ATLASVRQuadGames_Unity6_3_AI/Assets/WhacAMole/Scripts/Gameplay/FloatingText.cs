using TMPro;
using UnityEngine;

namespace Whacamole.Gameplay
{
    /// <summary>
    /// A popup like "+20" or "LEVEL 3!" that pops in, floats up, always turns to face the player, and fades.
    /// Uses TextMeshPro (falls back to the old TextMesh if TMP has no default font). Destroys itself.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        private TextMeshPro tmp;
        private TextMesh legacy;
        private Color color;
        private float age, life, rise, baseScale;
        private bool bounce;

        private static Transform head;
        private static Material outlined; // one shared outlined font material for every popup

        private static Material OutlinedMaterial()
        {
            if (outlined == null && TMP_Settings.defaultFontAsset != null)
            {
                outlined = new Material(TMP_Settings.defaultFontAsset.material) { name = "PopupOutlined (runtime)" };
                outlined.EnableKeyword(ShaderUtilities.Keyword_Outline);
                outlined.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.18f);  // dark edge so it reads against sky and grass
                outlined.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(30, 15, 5, 255));
            }
            return outlined;
        }

        /// <param name="height">Letter height in meters (roughly).</param>
        /// <param name="rise">How far it floats up over its life (m).</param>
        /// <param name="bounce">Pop in with an overshoot (for big announcements).</param>
        public static FloatingText Spawn(string text, Vector3 position, Color color, float height = 0.06f,
                                         float life = 0.9f, float rise = 0.25f, bool bounce = false)
        {
            var go = new GameObject("FloatingText");
            go.transform.position = position;
            var ft = go.AddComponent<FloatingText>();
            ft.color = color;
            ft.life = Mathf.Max(0.1f, life);
            ft.rise = rise;
            ft.bounce = bounce;
            ft.Build(text, height);
            return ft;
        }

        /// <summary>The player's head (OVR center eye, else the main camera).</summary>
        public static Transform Head
        {
            get
            {
                if (head != null && head.gameObject.activeInHierarchy) return head;
                OVRCameraRig rig = FindFirstObjectByType<OVRCameraRig>();
                if (rig != null && rig.centerEyeAnchor != null) head = rig.centerEyeAnchor;
                else if (Camera.main != null) head = Camera.main.transform;
                return head;
            }
        }

        private void Build(string text, float height)
        {
            if (TMP_Settings.defaultFontAsset != null)
            {
                tmp = gameObject.AddComponent<TextMeshPro>();
                tmp.richText = true;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableAutoSizing = false;
                tmp.fontSize = 10f;               // about 1 m tall letters at scale 1...
                tmp.fontStyle = FontStyles.Bold;
                tmp.rectTransform.sizeDelta = new Vector2(30f, 6f);
                Material m = OutlinedMaterial();
                if (m != null) tmp.fontSharedMaterial = m;
                tmp.text = text;
                baseScale = height;               // ...so scaling by the height gives the right size
            }
            else
            {
                legacy = gameObject.AddComponent<TextMesh>();
                legacy.text = text;
                legacy.anchor = TextAnchor.MiddleCenter;
                legacy.alignment = TextAlignment.Center;
                legacy.fontSize = 64;
                legacy.characterSize = 0.1f;
                legacy.richText = true;
                legacy.fontStyle = FontStyle.Bold;
                baseScale = height / 0.75f;
            }
            Update();
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / life);

            // Pop in (with an overshoot if bouncing), then hold.
            float pop = Mathf.Clamp01(age / 0.15f);
            float s = bounce ? 1f + Mathf.Sin(pop * Mathf.PI) * 0.35f * (1f - pop * 0.5f) : Mathf.SmoothStep(0.3f, 1f, pop);
            if (bounce && pop >= 1f) s = 1f;
            transform.localScale = Vector3.one * (baseScale * s);

            // Float up (fast at first, then easing), and face the player.
            float risen = rise * (1f - (1f - t) * (1f - t));
            transform.position += Vector3.up * (risen - lastRisen);
            lastRisen = risen;
            Transform h = Head;
            if (h != null)
            {
                Vector3 away = transform.position - h.position;
                away.y = 0f; // stay upright
                if (away.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
            }

            // Fade out over the last 40%.
            Color c = color;
            c.a *= t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            if (tmp != null) tmp.color = c;
            if (legacy != null) legacy.color = c;

            if (age >= life) Destroy(gameObject);
        }

        private float lastRisen;
    }
}
