using System.Collections.Generic;
using UnityEngine;

namespace Whacamole.Gameplay
{
    /// <summary>
    /// Sparkle burst when an acorn power-up is collected: a soft glowing pop plus sparkles that spiral up
    /// and outward in the power-up's color, with a chime. Built from simple shapes at runtime (no assets
    /// needed) and destroys itself after about a second. Purely visual: no colliders.
    /// </summary>
    public class PowerUpEffect : MonoBehaviour
    {
        private const float Duration = 1.0f;
        private const float GlowTime = 0.3f;

        private static Material sharedMaterial;

        private Color color;
        private Transform glow;
        private Renderer glowRenderer;
        private float glowSize;
        private float age;
        private MaterialPropertyBlock block;

        private readonly List<Transform> sparks = new List<Transform>();
        private readonly List<Renderer> sparkRenderers = new List<Renderer>();
        private readonly List<float> sparkAngle = new List<float>();
        private readonly List<float> sparkSpeed = new List<float>();
        private readonly List<float> sparkRise = new List<float>();

        /// <summary>Creates the effect (and plays the chime) at a world position.</summary>
        public static PowerUpEffect Spawn(Vector3 position, Color color, float size = 0.3f, int sparkCount = 14)
        {
            var root = new GameObject("PowerUpEffect");
            root.transform.position = position;
            var fx = root.AddComponent<PowerUpEffect>();
            fx.Build(color, size, sparkCount);
            WhacSounds.Play(WhacSound.PowerUp, position);
            return fx;
        }

        private static Material GetMaterial()
        {
            if (sharedMaterial == null)
                sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "PowerUpEffect (runtime)" };
            return sharedMaterial;
        }

        private void Build(Color c, float size, int sparkCount)
        {
            block = new MaterialPropertyBlock();
            color = c;
            glowSize = size;

            glow = CreatePiece(PrimitiveType.Sphere, out glowRenderer);
            glow.localScale = Vector3.one * 0.05f;

            for (int i = 0; i < sparkCount; i++)
            {
                Transform s = CreatePiece(PrimitiveType.Cube, out Renderer r);
                s.localScale = Vector3.one * Random.Range(0.015f, 0.03f);
                s.localRotation = Quaternion.Euler(45f, 45f, 0f); // diamond shape
                sparks.Add(s);
                sparkRenderers.Add(r);
                sparkAngle.Add(i * Mathf.PI * 2f / sparkCount + Random.Range(-0.2f, 0.2f));
                sparkSpeed.Add(Random.Range(0.25f, 0.45f));
                sparkRise.Add(Random.Range(0.35f, 0.7f));
            }

            Update();
        }

        private Transform CreatePiece(PrimitiveType type, out Renderer renderer)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = GetMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float life = Mathf.Clamp01(age / Duration);

            // Glow: quick soft pop that fades.
            float g = Mathf.Clamp01(age / GlowTime);
            glow.localScale = Vector3.one * Mathf.Lerp(0.05f, glowSize, 1f - (1f - g) * (1f - g));
            Color gc = Color.Lerp(Color.white, color, g);
            gc.a = (1f - g) * 0.8f;
            SetColor(glowRenderer, gc);
            glowRenderer.enabled = g < 1f;

            // Sparks: spiral outward and upward, twinkle, fade.
            for (int i = 0; i < sparks.Count; i++)
            {
                float a = sparkAngle[i] + age * 4f;
                float r = sparkSpeed[i] * Mathf.Sqrt(age);
                sparks[i].localPosition = new Vector3(Mathf.Cos(a) * r, sparkRise[i] * age, Mathf.Sin(a) * r);
                sparks[i].Rotate(0f, 360f * Time.deltaTime, 0f, Space.World);

                Color sc = Color.Lerp(Color.white, color, Mathf.PingPong(age * 6f + i, 1f));
                sc.a = 1f - life;
                SetColor(sparkRenderers[i], sc);
            }

            if (age >= Duration) Destroy(gameObject);
        }

        private void SetColor(Renderer r, Color c)
        {
            r.GetPropertyBlock(block);
            block.SetColor("_Color", c);
            r.SetPropertyBlock(block);
        }
    }
}
