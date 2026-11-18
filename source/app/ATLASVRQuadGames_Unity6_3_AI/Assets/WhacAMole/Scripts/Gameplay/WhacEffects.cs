using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Whacamole.Core;

namespace Whacamole.Gameplay
{
    /// <summary>
    /// Visual feedback for whac-a-mole, driven by the GameManager's events:
    /// - Hits: shell chips, a shock ring and a "+20" popup (gold, with "x2" when a multiplier is on).
    /// - Misses: a dust puff and a red "-1 life" popup.
    /// - Combo: "COMBO x3!" over the board with a spark ring when the multiplier goes up.
    /// - Acorns: sparkles around acorns that are out; on collect a popup, and a green orb that flies
    ///   to the scoreboard for +1 life.
    /// - Big hammer: a pulsing purple glow on each hammer head plus sparks when you swing.
    /// - Slow-mo: an icy blue dome over the table with drifting snow.
    ///   Both blink in their last 2 seconds so you know they're about to run out.
    /// - Level up: fireworks, confetti over the table and a big "LEVEL N!".
    /// Created automatically by the GameManager. To tune it, add this component to any object in the
    /// scene yourself (that one is used instead) and change the settings in the Inspector.
    /// </summary>
    public class WhacEffects : MonoBehaviour
    {
        public static WhacEffects Instance { get; private set; }

        [Header("Hits")]
        [SerializeField] private bool hitEffects = true;
        [SerializeField] private Color nutShellColor = new Color(0.55f, 0.34f, 0.15f);
        [Tooltip("Swing speed (m/s) that counts as a SMASH (more chips, bigger ring).")]
        [SerializeField, Min(0f)] private float smashSpeed = 2.5f;
        [SerializeField] private bool scorePopups = true;

        [Header("Misses")]
        [SerializeField] private bool missEffects = true;

        [Header("Combo")]
        [SerializeField] private bool comboEffects = true;

        [Header("Power-ups")]
        [SerializeField] private bool acornSparkles = true;
        [SerializeField] private bool bigHammerGlow = true;
        [SerializeField] private bool slowMoDome = true;
        [Tooltip("Seconds before a timed power-up ends that its effect starts blinking.")]
        [SerializeField, Min(0f)] private float warnTime = 2f;

        [Header("Level Up")]
        [SerializeField] private bool levelUpShow = true;
        [SerializeField, Range(1, 10)] private int fireworks = 4;
        [SerializeField, Range(0, 200)] private int confettiPieces = 70;

        private GameManager gm;
        private Transform boardTop;
        private readonly List<GameObject> hammerGlows = new List<GameObject>();
        private SlowMoDome dome;
        private float sparkleTimer;

        // ---------- Setup ----------

        /// <summary>Makes sure there's one WhacEffects in the scene (uses the scene's own if it has one).</summary>
        public static WhacEffects Ensure()
        {
            if (Instance != null) return Instance;
            WhacEffects existing = FindFirstObjectByType<WhacEffects>();
            if (existing != null) return existing;
            return new GameObject("WhacEffects").AddComponent<WhacEffects>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (gm == GameManager.Instance) return;
            Unsubscribe();
            gm = GameManager.Instance;
            if (gm == null) return;
            gm.NutScored += OnNutScored;
            gm.NutLost += OnNutLost;
            gm.MultiplierUp += OnMultiplierUp;
            gm.PowerUpCollected += OnPowerUpCollected;
            gm.PowerUpEnded += OnPowerUpEnded;
            gm.LevelChanged += OnLevelChanged;
        }

        private void Unsubscribe()
        {
            if (gm == null) return;
            gm.NutScored -= OnNutScored;
            gm.NutLost -= OnNutLost;
            gm.MultiplierUp -= OnMultiplierUp;
            gm.PowerUpCollected -= OnPowerUpCollected;
            gm.PowerUpEnded -= OnPowerUpEnded;
            gm.LevelChanged -= OnLevelChanged;
            gm = null;
        }

        private void Update()
        {
            Subscribe(); // cheap when already subscribed; picks the GameManager up whenever it appears
            if (gm == null) return;

            if (acornSparkles) SparkleAcorns();
            UpdateWarnings();
        }

        // ---------- Board info ----------

        private Transform BoardTop
        {
            get
            {
                if (boardTop == null)
                {
                    BoardLayout board = FindFirstObjectByType<BoardLayout>();
                    if (board != null) boardTop = board.transform.Find("Top");
                }
                return boardTop;
            }
        }

        /// <summary>World bounds of the table top (a guess around the GameManager if it can't be found).</summary>
        private Bounds TableBounds()
        {
            Transform top = BoardTop;
            if (top != null && top.TryGetComponent(out Renderer r)) return r.bounds;
            Vector3 c = gm != null ? gm.transform.position + Vector3.up * 0.85f : Vector3.up * 0.85f;
            return new Bounds(c, new Vector3(1f, 0.05f, 0.7f));
        }

        private Vector3 TableTopCenter()
        {
            Bounds b = TableBounds();
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }

        // ---------- Hits and misses ----------

        private void OnNutScored(Vector3 pos, int points, int multiplier, float swingSpeed)
        {
            if (hitEffects)
            {
                bool smash = swingSpeed >= smashSpeed;
                var fx = FxParticles.Create("HitFx", pos);

                // Shell chips flying up and out, then falling.
                int chips = smash ? 14 : 8;
                for (int i = 0; i < chips; i++)
                {
                    Vector3 dir = Random.onUnitSphere;
                    dir.y = Mathf.Abs(dir.y) * 1.5f + 0.4f;
                    Color c = nutShellColor * Random.Range(0.75f, 1.2f);
                    c.a = 1f;
                    fx.Emit(PrimitiveType.Cube, pos, dir.normalized * Random.Range(0.8f, smash ? 2.2f : 1.5f),
                            Vector3.one * Random.Range(0.012f, 0.025f), c, Random.Range(0.45f, 0.7f),
                            gravity: 6f, drag: 0.5f, spinSpeed: 720f, shrink: false);
                }

                // Gold sparks when a multiplier is running (more for higher ones).
                for (int i = 0; i < (multiplier - 1) * 5; i++)
                {
                    fx.Emit(PrimitiveType.Cube, pos, Random.onUnitSphere * Random.Range(0.6f, 1.4f) + Vector3.up * 0.4f,
                            Vector3.one * 0.012f, new Color(1f, 0.85f, 0.3f), Random.Range(0.3f, 0.5f),
                            gravity: 1f, drag: 2f, spinSpeed: 360f, twinkle: true);
                }

                ShockRing.Spawn(pos, smash ? 0.35f : 0.22f, smash ? new Color(1f, 0.9f, 0.6f, 0.9f) : new Color(1f, 1f, 1f, 0.7f));
                if (smash) FloatingText.Spawn("SMASH!", pos + Vector3.up * 0.16f, new Color(1f, 0.6f, 0.2f), 0.045f, 0.7f, 0.18f, true);
            }

            if (scorePopups)
            {
                Color c = multiplier > 1 ? new Color(1f, 0.82f, 0.25f) : Color.white;
                string text = multiplier > 1 ? $"+{points} <size=70%>x{multiplier}</size>" : $"+{points}";
                FloatingText.Spawn(text, pos + Vector3.up * 0.08f, c, 0.05f + 0.008f * (multiplier - 1), 0.85f, 0.25f);
            }
        }

        private void OnNutLost(Vector3 pos, int penalty)
        {
            if (!missEffects) return;
            WhacSounds.Play(WhacSound.Miss, pos, 0.6f);

            // Dust puff around the hole.
            var fx = FxParticles.Create("MissFx", pos);
            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = Random.insideUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.4f;
                fx.Emit(PrimitiveType.Sphere, pos + dir * 0.03f, dir * 0.3f + Vector3.up * 0.15f,
                        Vector3.one * Random.Range(0.04f, 0.07f), new Color(0.75f, 0.7f, 0.62f, 0.55f), Random.Range(0.5f, 0.8f),
                        drag: 2f, shrink: false);
            }

            string text = penalty > 0 ? $"MISS  -1 life <size=70%>(-{penalty})</size>" : "MISS  -1 life";
            FloatingText.Spawn(text, pos + Vector3.up * 0.12f, new Color(1f, 0.35f, 0.3f), 0.04f, 1f, 0.2f);
        }

        private void OnMultiplierUp(int multiplier)
        {
            if (!comboEffects || multiplier <= 1) return;
            Vector3 c = TableTopCenter();
            WhacSounds.Play(WhacSound.Combo, c + Vector3.up * 0.5f, 0.8f);
            FloatingText.Spawn($"COMBO x{multiplier}!", c + Vector3.up * 0.45f, new Color(1f, 0.82f, 0.25f), 0.09f, 1.3f, 0.2f, true);

            // Ring of gold sparks around the table edge.
            Bounds b = TableBounds();
            var fx = FxParticles.Create("ComboFx", c);
            int n = 24;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * b.extents.x, 0.02f, Mathf.Sin(a) * b.extents.z);
                fx.Emit(PrimitiveType.Cube, p, Vector3.up * Random.Range(0.6f, 1f), Vector3.one * 0.02f,
                        new Color(1f, 0.85f, 0.3f), Random.Range(0.5f, 0.8f), gravity: 0.5f, drag: 1.5f,
                        spinSpeed: 360f, twinkle: true);
            }
        }

        // ---------- Power-ups ----------

        /// <summary>Called by the NutSpawner when an acorn is hit: popup, and the extra-life orb.</summary>
        public void PowerUpCollectedAt(PowerUpType type, Vector3 pos)
        {
            Color c = PowerUpInfo.GetColor(type);
            FloatingText.Spawn(PowerUpInfo.GetCollectedMessage(type), pos + Vector3.up * 0.12f, c, 0.06f, 1.2f, 0.3f, true);

            if (type == PowerUpType.ExtraLife)
            {
                var board = FindFirstObjectByType<UI.Scoreboard>();
                Vector3 target = board != null ? board.transform.position : pos + Vector3.up * 1f;
                LifeOrb.Spawn(pos, target, c);
            }
        }

        private void OnPowerUpCollected(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.BigHammer:
                    if (bigHammerGlow && hammerGlows.Count == 0)
                        foreach (Hammer h in gm.Hammers)
                            if (h != null) hammerGlows.Add(HammerGlow.Attach(h, PowerUpInfo.GetColor(type)).gameObject);
                    break;

                case PowerUpType.SlowTime:
                    WhacSounds.Play(WhacSound.SlowMo, TableTopCenter() + Vector3.up * 0.5f, 0.8f);
                    if (slowMoDome && dome == null) dome = SlowMoDome.Spawn(TableBounds(), PowerUpInfo.GetColor(type));
                    break;
            }
        }

        private void OnPowerUpEnded(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.BigHammer:
                    foreach (GameObject g in hammerGlows)
                        if (g != null && g.TryGetComponent(out HammerGlow glow)) glow.FadeOut();
                    hammerGlows.Clear();
                    break;

                case PowerUpType.SlowTime:
                    if (dome != null) dome.FadeOut();
                    dome = null;
                    break;
            }
        }

        private void UpdateWarnings()
        {
            float bh = gm.PowerUpTimeLeft(PowerUpType.BigHammer);
            bool hammerWarn = bh > 0f && bh < warnTime;
            foreach (GameObject g in hammerGlows)
                if (g != null && g.TryGetComponent(out HammerGlow glow)) glow.Warning = hammerWarn;

            float st = gm.PowerUpTimeLeft(PowerUpType.SlowTime);
            if (dome != null) dome.Warning = st > 0f && st < warnTime;
        }

        // Little twinkles around any acorn that's out of its hole.
        private void SparkleAcorns()
        {
            sparkleTimer -= Time.deltaTime;
            if (sparkleTimer > 0f) return;
            sparkleTimer = 0.07f;

            foreach (Nut nut in gm.Nuts)
            {
                if (nut == null || !nut.IsPowerUp || nut.State == Nut.NutState.Hidden) continue;
                Vector3 p = nut.transform.position + Random.onUnitSphere * 0.07f * Mathf.Max(nut.transform.lossyScale.x, 0.5f);
                var fx = FxParticles.Create("AcornSparkle", p);
                fx.Emit(PrimitiveType.Cube, p, Vector3.up * 0.12f, Vector3.one * 0.012f,
                        Color.Lerp(PowerUpInfo.GetColor(nut.PowerUp), Color.white, 0.4f), 0.5f,
                        spinSpeed: 300f, twinkle: true);
            }
        }

        // ---------- Level up ----------

        private void OnLevelChanged(int level)
        {
            if (!levelUpShow || level <= 1 || gm == null || !gm.IsPlaying) return;
            StartCoroutine(LevelUpShow(level));
        }

        private IEnumerator LevelUpShow(int level)
        {
            Bounds b = TableBounds();
            Vector3 c = TableTopCenter();

            FloatingText.Spawn($"LEVEL {level}!", c + Vector3.up * 0.6f, new Color(1f, 0.85f, 0.3f), 0.14f, 2.2f, 0.25f, true);
            ShockRing.Spawn(c + Vector3.up * 0.01f, Mathf.Max(b.extents.x, b.extents.z) * 2.4f, new Color(1f, 0.85f, 0.3f, 0.8f), 0.7f);
            Confetti(b);

            int count = Mathf.Clamp(fireworks + (level - 2) / 2, 1, 10);
            for (int i = 0; i < count; i++)
            {
                Vector3 from = new Vector3(Random.Range(b.min.x, b.max.x), b.max.y, Random.Range(b.center.z, b.max.z + 0.3f));
                Firework.Launch(from, Random.Range(0.9f, 1.5f), FxParticles.Festive());
                yield return new WaitForSeconds(Random.Range(0.18f, 0.35f));
            }
        }

        private void Confetti(Bounds b)
        {
            if (confettiPieces <= 0) return;
            var fx = FxParticles.Create("Confetti", b.center);
            for (int i = 0; i < confettiPieces; i++)
            {
                Vector3 p = new Vector3(Random.Range(b.min.x - 0.2f, b.max.x + 0.2f), b.max.y + Random.Range(1.0f, 1.6f),
                                        Random.Range(b.min.z - 0.2f, b.max.z + 0.2f));
                fx.Emit(PrimitiveType.Cube, p, Vector3.down * Random.Range(0.25f, 0.45f),
                        new Vector3(0.025f, 0.003f, 0.015f), FxParticles.Festive(), Random.Range(2.5f, 3.5f),
                        spinSpeed: 400f, shrink: false, sway: 0.25f);
            }
        }
    }

    /// <summary>A flat ring that expands and fades (hit shock wave, level-up wave).</summary>
    public class ShockRing : MonoBehaviour
    {
        private Renderer[] pieces;
        private Color color;
        private float age, life, size;

        public static ShockRing Spawn(Vector3 pos, float size, Color color, float life = 0.35f)
        {
            var go = new GameObject("ShockRing");
            go.transform.position = pos;
            var ring = go.AddComponent<ShockRing>();
            ring.size = size;
            ring.color = color;
            ring.life = life;

            // A ring made of short flat segments.
            const int n = 20;
            ring.pieces = new Renderer[n];
            for (int i = 0; i < n; i++)
            {
                GameObject s = FxParticles.CreateShape(PrimitiveType.Cube, go.transform);
                float a = i * 360f / n;
                s.transform.localRotation = Quaternion.Euler(0f, a, 0f);
                ring.pieces[i] = s.GetComponent<Renderer>();
            }
            ring.Update();
            return ring;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / life);
            float radius = Mathf.Lerp(0.02f, size * 0.5f, 1f - (1f - t) * (1f - t));
            float segment = 2f * Mathf.PI * radius / pieces.Length * 1.1f;
            Color c = color;
            c.a *= 1f - t;
            for (int i = 0; i < pieces.Length; i++)
            {
                Transform s = pieces[i].transform;
                s.localPosition = s.localRotation * Vector3.forward * radius;
                s.localScale = new Vector3(segment, 0.004f, Mathf.Lerp(0.02f, 0.006f, t));
                FxParticles.SetColor(pieces[i], c);
            }
            if (age >= life) Destroy(gameObject);
        }
    }

    /// <summary>A level-up firework: a spark rises, then bursts into a ball of falling, twinkling sparks.</summary>
    public class Firework : MonoBehaviour
    {
        private FxParticles trail;
        private Transform rocket;
        private Vector3 start;
        private float height, age;
        private const float RiseTime = 0.45f;
        private Color color;

        public static void Launch(Vector3 from, float height, Color color)
        {
            var go = new GameObject("Firework");
            go.transform.position = from;
            var fw = go.AddComponent<Firework>();
            fw.start = from;
            fw.height = height;
            fw.color = color;
            fw.rocket = FxParticles.CreateShape(PrimitiveType.Sphere, go.transform).transform;
            fw.rocket.localScale = Vector3.one * 0.03f;
            FxParticles.SetColor(fw.rocket.GetComponent<Renderer>(), Color.Lerp(color, Color.white, 0.6f));
            fw.trail = FxParticles.Create("FireworkTrail", from);
            fw.trail.KeepAlive = true; // until the rocket bursts
        }

        private void OnDestroy()
        {
            if (trail != null) trail.KeepAlive = false; // the trail fades out on its own
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / RiseTime);
            Vector3 p = start + Vector3.up * (height * (1f - (1f - t) * (1f - t)));
            rocket.position = p;
            if (trail != null)
                trail.Emit(PrimitiveType.Cube, p, Vector3.down * 0.2f, Vector3.one * 0.012f, new Color(1f, 0.8f, 0.5f), 0.3f);

            if (t >= 1f)
            {
                Burst(p);
                Destroy(gameObject);
            }
        }

        private void Burst(Vector3 p)
        {
            WhacSounds.Play(WhacSound.Firework, p, 0.7f);
            var fx = FxParticles.Create("FireworkBurst", p);
            Color second = Color.Lerp(color, Color.white, 0.5f);
            for (int i = 0; i < 32; i++)
            {
                Vector3 v = Random.onUnitSphere * Random.Range(0.9f, 1.2f);
                fx.Emit(PrimitiveType.Cube, p, v, Vector3.one * 0.02f, i % 3 == 0 ? second : color,
                        Random.Range(0.9f, 1.3f), gravity: 0.8f, drag: 1.8f, spinSpeed: 500f, twinkle: true);
            }
            // Bright flash at the center.
            fx.Emit(PrimitiveType.Sphere, p, Vector3.zero, Vector3.one * 0.25f, new Color(1f, 1f, 0.9f, 0.6f), 0.15f);
        }
    }

    /// <summary>For the +1 life acorn: a glowing green orb that arcs from the acorn to the scoreboard and pops.</summary>
    public class LifeOrb : MonoBehaviour
    {
        private Vector3 from, to;
        private Color color;
        private float age;
        private const float FlyTime = 0.7f;
        private FxParticles trail;
        private Renderer glow;

        public static void Spawn(Vector3 from, Vector3 to, Color color)
        {
            var go = new GameObject("LifeOrb");
            go.transform.position = from;
            var orb = go.AddComponent<LifeOrb>();
            orb.from = from;
            orb.to = to;
            orb.color = color;
            GameObject ball = FxParticles.CreateShape(PrimitiveType.Sphere, go.transform);
            ball.transform.localScale = Vector3.one * 0.06f;
            orb.glow = ball.GetComponent<Renderer>();
            FxParticles.SetColor(orb.glow, Color.Lerp(color, Color.white, 0.3f));
            orb.trail = FxParticles.Create("LifeOrbTrail", from);
            orb.trail.KeepAlive = true;
        }

        private void OnDestroy()
        {
            if (trail != null) trail.KeepAlive = false;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / FlyTime);
            float e = t * t * (3f - 2f * t);
            Vector3 p = Vector3.Lerp(from, to, e) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.35f);
            transform.position = p;
            transform.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(age * 30f));
            if (trail != null)
                trail.Emit(PrimitiveType.Cube, p, Random.insideUnitSphere * 0.1f, Vector3.one * 0.015f, color, 0.4f, twinkle: true);

            if (t >= 1f)
            {
                var fx = FxParticles.Create("LifeOrbPop", p);
                for (int i = 0; i < 16; i++)
                    fx.Emit(PrimitiveType.Cube, p, Random.onUnitSphere * 0.8f, Vector3.one * 0.02f, color, 0.6f,
                            drag: 3f, spinSpeed: 400f, twinkle: true);
                FloatingText.Spawn("+1", p + Vector3.up * 0.1f, color, 0.07f, 0.9f, 0.2f, true);
                Destroy(gameObject);
            }
        }
    }

    /// <summary>Big-hammer glow: a pulsing see-through sphere on the hammer head, plus sparks when you swing.</summary>
    public class HammerGlow : MonoBehaviour
    {
        public bool Warning { get; set; }

        private Renderer glow;
        private Color color;
        private float alpha, targetAlpha = 1f, age;
        private Vector3 lastPos;
        private FxParticles sparks;

        public static HammerGlow Attach(Hammer hammer, Color color)
        {
            // Parented to the hammer itself, so it follows the hand and grows with the hammer.
            var go = new GameObject("BigHammerGlow");
            go.transform.SetParent(hammer.transform, false);
            if (hammer.HeadCollider != null)
                go.transform.position = hammer.HeadCollider.bounds.center;

            var g = go.AddComponent<HammerGlow>();
            g.color = color;
            GameObject ball = FxParticles.CreateShape(PrimitiveType.Sphere, go.transform);
            float headSize = hammer.HeadCollider != null ? hammer.HeadCollider.bounds.size.magnitude : 0.15f;
            float scale = Mathf.Max(hammer.transform.lossyScale.x, 1e-3f);
            ball.transform.localScale = Vector3.one * (headSize * 0.9f / scale);
            g.glow = ball.GetComponent<Renderer>();
            g.lastPos = go.transform.position;
            g.sparks = FxParticles.Create("BigHammerSparks", go.transform.position);
            g.sparks.KeepAlive = true;
            return g;
        }

        public void FadeOut() => targetAlpha = 0f;

        private void Update()
        {
            age += Time.deltaTime;
            alpha = Mathf.MoveTowards(alpha, targetAlpha, Time.deltaTime * 3f);

            float pulse = 0.3f + 0.12f * Mathf.Sin(age * 6f);
            if (Warning) pulse *= Mathf.Sin(age * 18f) > 0f ? 1f : 0.2f; // blink: about to run out
            Color c = Color.Lerp(color, Color.white, 0.2f);
            c.a = pulse * alpha;
            FxParticles.SetColor(glow, c);

            // Sparks fly off when the hammer is swung.
            Vector3 p = transform.position;
            float speed = (p - lastPos).magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
            lastPos = p;
            if (speed > 1.2f && alpha > 0.5f && sparks != null)
                for (int i = 0; i < 2; i++)
                    sparks.Emit(PrimitiveType.Cube, p + Random.insideUnitSphere * 0.05f, Random.insideUnitSphere * 0.3f,
                                Vector3.one * 0.015f, color, 0.4f, gravity: 0.5f, spinSpeed: 400f, twinkle: true);

            if (alpha <= 0f && targetAlpha <= 0f)
            {
                if (sparks != null) sparks.KeepAlive = false;
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (sparks != null) sparks.KeepAlive = false;
        }
    }

    /// <summary>Slow-mo field: a faint icy dome over the table with slowly drifting snow sparkles.</summary>
    public class SlowMoDome : MonoBehaviour
    {
        public bool Warning { get; set; }

        private Renderer shell;
        private Color color;
        private Bounds area;
        private float alpha, targetAlpha = 1f, age, snowTimer;
        private FxParticles snow;

        public static SlowMoDome Spawn(Bounds table, Color color)
        {
            var go = new GameObject("SlowMoDome");
            go.transform.position = new Vector3(table.center.x, table.max.y, table.center.z);
            var d = go.AddComponent<SlowMoDome>();
            d.color = color;
            d.area = table;
            GameObject ball = FxParticles.CreateShape(PrimitiveType.Sphere, go.transform);
            ball.transform.localScale = new Vector3(table.size.x + 0.5f, 1.1f, table.size.z + 0.5f);
            d.shell = ball.GetComponent<Renderer>();
            d.snow = FxParticles.Create("SlowMoSnow", go.transform.position);
            d.snow.KeepAlive = true;
            return d;
        }

        public void FadeOut() => targetAlpha = 0f;

        private void Update()
        {
            age += Time.deltaTime;
            alpha = Mathf.MoveTowards(alpha, targetAlpha, Time.deltaTime * 2.5f);

            float a = 0.07f + 0.03f * Mathf.Sin(age * 2f);
            if (Warning) a *= Mathf.Sin(age * 14f) > 0f ? 1f : 0.25f;
            Color c = color;
            c.a = a * alpha;
            FxParticles.SetColor(shell, c);
            if (snow != null) snow.Alpha = alpha;

            snowTimer -= Time.deltaTime;
            if (snowTimer <= 0f && targetAlpha > 0f && snow != null)
            {
                snowTimer = 0.05f;
                Vector3 p = new Vector3(Random.Range(area.min.x, area.max.x), area.max.y + Random.Range(0.4f, 0.7f),
                                        Random.Range(area.min.z, area.max.z));
                snow.Emit(PrimitiveType.Cube, p, Vector3.down * Random.Range(0.08f, 0.15f), Vector3.one * 0.012f,
                          Color.Lerp(color, Color.white, 0.6f), 3f, spinSpeed: 120f, twinkle: true, sway: 0.08f);
            }

            if (alpha <= 0f && targetAlpha <= 0f)
            {
                if (snow != null) snow.KeepAlive = false;
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (snow != null) snow.KeepAlive = false;
        }
    }
}
