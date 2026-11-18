using System.Collections.Generic;
using UnityEngine;

namespace Whacamole.Gameplay
{
    /// <summary>
    /// A tiny particle system for the whac-a-mole effects, made of simple shapes (no colliders) that fly,
    /// fall, spin, shrink and fade. One FxParticles object holds a batch of particles and destroys itself
    /// once they're all gone (unless KeepAlive is set, e.g. for an effect that keeps emitting).
    /// Uses the unlit "Sprites/Default" shader so colors stay bright, like the other runtime effects.
    /// </summary>
    public class FxParticles : MonoBehaviour
    {
        private class Particle
        {
            public Transform t;
            public Renderer r;
            public Vector3 velocity;
            public Vector3 spin;      // degrees per second
            public float age, life;
            public float gravity;     // m/s^2 (positive pulls down)
            public float drag;        // fraction of speed lost per second
            public float sway;        // side-to-side flutter (m/s), for confetti and snow
            public float swayPhase;
            public Vector3 size;
            public Color color;
            public bool shrink, twinkle;
        }

        /// <summary>Keep this object (and its Update) alive even when no particles are left.</summary>
        public bool KeepAlive { get; set; }
        /// <summary>Multiplies all particle alpha (for fading a whole effect in or out).</summary>
        public float Alpha { get; set; } = 1f;
        public int Count => particles.Count;

        private static Material sharedMaterial;
        private static MaterialPropertyBlock block;
        private readonly List<Particle> particles = new List<Particle>();

        public static Material SharedMaterial
        {
            get
            {
                if (sharedMaterial == null)
                    sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "WhacFx (runtime)" };
                return sharedMaterial;
            }
        }

        /// <summary>Makes an empty particle batch at a world position.</summary>
        public static FxParticles Create(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            return go.AddComponent<FxParticles>();
        }

        /// <summary>Adds one particle at a world position.</summary>
        public void Emit(PrimitiveType shape, Vector3 position, Vector3 velocity, Vector3 size, Color color, float life,
                         float gravity = 0f, float drag = 0f, float spinSpeed = 0f, bool shrink = true,
                         bool twinkle = false, float sway = 0f)
        {
            GameObject go = CreateShape(shape, transform);
            go.transform.position = position;
            go.transform.rotation = Random.rotation;
            go.transform.localScale = size;

            var p = new Particle
            {
                t = go.transform,
                r = go.GetComponent<Renderer>(),
                velocity = velocity,
                spin = Random.onUnitSphere * spinSpeed,
                life = Mathf.Max(0.01f, life),
                gravity = gravity,
                drag = drag,
                sway = sway,
                swayPhase = Random.value * 10f,
                size = size,
                color = color,
                shrink = shrink,
                twinkle = twinkle
            };
            particles.Add(p);
            Apply(p, 0f);
        }

        /// <summary>A shape with no collider and the shared unlit effect material, parented to parent.</summary>
        public static GameObject CreateShape(PrimitiveType shape, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = SharedMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>Sets a renderer's color through a property block (shared material stays untouched).</summary>
        public static void SetColor(Renderer r, Color c)
        {
            if (block == null) block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_Color", c);
            r.SetPropertyBlock(block);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i];
                p.age += dt;
                if (p.age >= p.life)
                {
                    Destroy(p.t.gameObject);
                    particles.RemoveAt(i);
                    continue;
                }

                p.velocity += Vector3.down * (p.gravity * dt);
                if (p.drag > 0f) p.velocity *= Mathf.Max(0f, 1f - p.drag * dt);
                Vector3 move = p.velocity * dt;
                if (p.sway > 0f)
                    move += new Vector3(Mathf.Sin(p.age * 3.1f + p.swayPhase), 0f, Mathf.Cos(p.age * 2.3f + p.swayPhase)) * (p.sway * dt);
                p.t.position += move;
                if (p.spin != Vector3.zero) p.t.Rotate(p.spin * dt, Space.World);

                Apply(p, p.age / p.life);
            }

            if (particles.Count == 0 && !KeepAlive) Destroy(gameObject);
        }

        private void Apply(Particle p, float t)
        {
            if (p.shrink) p.t.localScale = p.size * (1f - t * t);
            Color c = p.color;
            float fade = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            if (p.twinkle) fade *= 0.55f + 0.45f * Mathf.Sin((p.age + p.swayPhase) * 25f);
            c.a *= Mathf.Clamp01(fade) * Alpha;
            SetColor(p.r, c);
        }

        /// <summary>A random bright color for fireworks and confetti.</summary>
        public static Color Festive()
        {
            Color[] palette =
            {
                new Color(1f, 0.3f, 0.3f), new Color(1f, 0.8f, 0.2f), new Color(0.3f, 0.9f, 0.4f),
                new Color(0.3f, 0.75f, 1f), new Color(0.8f, 0.45f, 1f), new Color(1f, 0.55f, 0.85f)
            };
            return palette[Random.Range(0, palette.Length)];
        }
    }
}
