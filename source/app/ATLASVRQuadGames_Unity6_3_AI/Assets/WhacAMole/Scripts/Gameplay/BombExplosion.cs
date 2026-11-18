using System.Collections.Generic;
using UnityEngine;

namespace Whacamole.Gameplay
{
    /// <summary>
    /// A short explosion effect for bomb nuts: a fireball that swells and fades, plus a burst of
    /// debris that flies out and falls. Built from simple shapes at runtime (no prefab or particle
    /// assets needed) and destroys itself after about a second. Purely visual: no colliders.
    /// </summary>
    public class BombExplosion : MonoBehaviour
    {
        private const float Duration = 0.9f;
        private const float FlashTime = 0.35f;

        private static Material sharedMaterial;

        private Transform flash;
        private Renderer flashRenderer;
        private float maxFlashSize;
        private float age;
        private MaterialPropertyBlock block;

        private readonly List<Transform> debris = new List<Transform>();
        private readonly List<Renderer> debrisRenderers = new List<Renderer>();
        private readonly List<Vector3> debrisVelocity = new List<Vector3>();
        private readonly List<Vector3> debrisSpin = new List<Vector3>();
        private readonly List<Color> debrisColor = new List<Color>();

        /// <summary>Creates an explosion at a world position. size = fireball diameter at its biggest (m).</summary>
        public static BombExplosion Spawn(Vector3 position, float size = 0.45f, int debrisCount = 12)
        {
            var root = new GameObject("BombExplosion");
            root.transform.position = position;
            var fx = root.AddComponent<BombExplosion>();
            fx.Build(size, debrisCount);
            WhacSounds.Play(WhacSound.Explosion, position);
            return fx;
        }

        private static Material GetMaterial()
        {
            if (sharedMaterial == null)
            {
                // Sprites/Default: unlit, supports per-object color with transparency, and is always in builds.
                sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "BombExplosion (runtime)" };
            }
            return sharedMaterial;
        }

        private void Build(float size, int debrisCount)
        {
            block = new MaterialPropertyBlock();
            maxFlashSize = size;

            flash = CreatePiece(PrimitiveType.Sphere, transform, out flashRenderer);
            flash.localScale = Vector3.one * 0.05f;

            for (int i = 0; i < debrisCount; i++)
            {
                Transform piece = CreatePiece(PrimitiveType.Cube, transform, out Renderer r);
                piece.localScale = Vector3.one * Random.Range(0.02f, 0.045f);
                piece.localRotation = Random.rotation;

                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f; // mostly up and out
                debris.Add(piece);
                debrisRenderers.Add(r);
                debrisVelocity.Add(dir.normalized * Random.Range(1.2f, 2.6f));
                debrisSpin.Add(Random.insideUnitSphere * 720f);
                debrisColor.Add(Random.value < 0.5f ? new Color(1f, 0.55f, 0.1f) : new Color(0.15f, 0.15f, 0.15f));
            }

            Update(); // set first-frame colors
        }

        private static Transform CreatePiece(PrimitiveType type, Transform parent, out Renderer renderer)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>()); // visual only
            go.transform.SetParent(parent, false);
            renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = GetMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;

            // Fireball: swells fast, white-yellow -> orange -> red, then fades out.
            float f = Mathf.Clamp01(age / FlashTime);
            float grow = 1f - (1f - f) * (1f - f);
            flash.localScale = Vector3.one * Mathf.Lerp(0.05f, maxFlashSize, grow);
            Color hot = Color.Lerp(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.45f, 0.05f), f);
            hot = Color.Lerp(hot, new Color(0.8f, 0.1f, 0.02f), Mathf.Clamp01((f - 0.6f) / 0.4f));
            hot.a = 1f - f * f;
            SetColor(flashRenderer, hot);
            flashRenderer.enabled = f < 1f;

            // Debris: flies out, falls, spins and fades.
            float life = Mathf.Clamp01(age / Duration);
            for (int i = 0; i < debris.Count; i++)
            {
                Vector3 v = debrisVelocity[i];
                v.y -= 9.81f * dt;
                debrisVelocity[i] = v;
                debris[i].position += v * dt;
                debris[i].Rotate(debrisSpin[i] * dt, Space.Self);

                Color c = debrisColor[i];
                c.a = 1f - life;
                SetColor(debrisRenderers[i], c);
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
