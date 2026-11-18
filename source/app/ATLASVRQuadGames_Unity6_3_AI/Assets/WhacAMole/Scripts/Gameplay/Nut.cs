using System;
using UnityEngine;
using Whacamole.Controls;
using Whacamole.Core;

namespace Whacamole.Gameplay
{
    /// <summary>
    /// A nut that lives in one hole of the board. It hides below the table until the NutSpawner
    /// launches it: it shoots up through the hole with a little random drift, slows to a stop
    /// clearly above the table, and hovers there. When a hammer touches it, it falls back into
    /// the hole under gravity. If nobody hits it before its lifetime runs out, it drops back
    /// down on its own and reports a miss.
    ///
    /// The Rigidbody is kinematic, so motion is simulated here and applied with MovePosition.
    /// All colliders on the nut are triggers; the hammer head is a trigger too.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Nut : MonoBehaviour
    {
        /// <summary>What a pop-up is: a normal nut, a bomb (don't hit!), or a power-up acorn.</summary>
        public enum NutKind
        {
            Normal,
            Bomb,
            PowerUp
        }

        public enum NutState
        {
            Hidden,  // below the table, waiting to be launched
            Rising,  // flying up out of the hole
            Up,      // hovering above the table, can be hit
            Falling  // dropping back into the hole (hit or timed out)
        }

        [Tooltip("Which hole this nut sits in (0-8, row by row).")]
        [SerializeField] private int holeIndex;

        [Header("Motion")]
        [Tooltip("How far below the table top the nut hides (m). Must be deep enough that the whole nut is out of sight.")]
        [SerializeField, Min(0.05f)] private float hiddenDepth = 0.18f;
        [Tooltip("Gravity used for the launch and the fall (m/s²).")]
        [SerializeField, Min(0.1f)] private float gravity = 9.81f;
        [Tooltip("Extra downward speed (m/s) given to the nut when it's hit, so it reacts instantly.")]
        [SerializeField, Min(0f)] private float hitKnockSpeed = 0.8f;
        [Tooltip("Gentle up/down bob while hovering (m). 0 = perfectly still.")]
        [SerializeField, Min(0f)] private float bobAmplitude = 0.008f;
        [SerializeField, Min(0f)] private float bobSpeed = 4f;

        [Header("Bomb Look")]
        [Tooltip("Colors used when this pop-up is a bomb (set per launch by the NutSpawner). Matched to the nut's Body, Cap and Stem parts.")]
        [SerializeField] private Color bombBodyColor = new Color(0.07f, 0.07f, 0.08f);
        [SerializeField] private Color bombCapColor = new Color(0.9f, 0.05f, 0.05f);
        [SerializeField] private Color bombCapFlashColor = new Color(1f, 0.6f, 0.1f);
        [SerializeField] private Color bombFuseColor = new Color(1f, 0.75f, 0.2f);
        [Tooltip("How fast the bomb's cap flashes (flashes per second).")]
        [SerializeField, Min(0f)] private float bombFlashRate = 4f;

        [Header("Power-up Acorn Look")]
        [Tooltip("Cap and stem color of power-up acorns (the body takes the power-up's own color).")]
        [SerializeField] private Color acornCapColor = new Color(1f, 0.8f, 0.25f);
        [Tooltip("How fast power-up acorns pulse brighter (pulses per second).")]
        [SerializeField, Min(0f)] private float acornPulseRate = 2.5f;
        [Tooltip("How fast power-up acorns spin while out (degrees per second).")]
        [SerializeField] private float acornSpinSpeed = 180f;
        [Tooltip("Size of the sparkle burst (m) when an acorn is collected.")]
        [SerializeField, Min(0.05f)] private float collectEffectSize = 0.3f;
        [Tooltip("Size of the explosion fireball (m) at full nut size; scales with the nut.")]
        [SerializeField, Min(0.05f)] private float explosionSize = 0.45f;

        public int HoleIndex => holeIndex;
        public NutState State { get; private set; } = NutState.Hidden;
        public bool IsHittable => State == NutState.Rising || State == NutState.Up;
        /// <summary>What this pop-up is for its current launch.</summary>
        public NutKind Kind { get; private set; } = NutKind.Normal;
        /// <summary>Which power-up this acorn gives (only meaningful when Kind is PowerUp).</summary>
        public PowerUpType PowerUp { get; private set; }
        /// <summary>True if this pop-up is a bomb: hitting it explodes and costs a life; letting it sink back is free.</summary>
        public bool IsBomb => Kind == NutKind.Bomb;
        /// <summary>True if this pop-up is a power-up acorn: hitting it collects the power-up; letting it sink back is free.</summary>
        public bool IsPowerUp => Kind == NutKind.PowerUp;
        /// <summary>Where this pop-up was when it was last hit (bombs and acorns vanish straight away, so effects use this).</summary>
        public Vector3 HitPosition { get; private set; }

        /// <summary>Raised when a hammer hits this nut: (nut, hammer, hammer velocity in m/s). Check Kind.</summary>
        public event Action<Nut, Hammer, Vector3> Hit;
        /// <summary>Raised when this nut's lifetime runs out without being hit.</summary>
        public event Action<Nut> Missed;

        private Rigidbody rb;
        private Collider[] colliders;
        private Renderer[] renderers;
        private MaterialPropertyBlock lookBlock;
        private Quaternion homeRotation = Quaternion.identity;
        private float spinAngle;

        private Vector3 home;        // local position: hole center, flush with the table top
        private Vector3 localPos;    // simulated local position
        private Vector3 velocity;    // simulated local velocity
        private Vector3 hoverPos;
        private float aliveTime;
        private float lifetime;
        private float hoverTime;

        public void Setup(int index) => holeIndex = index;

        /// <summary>
        /// Moves this nut to a new hole (used when the board grows between levels). The nut is hidden
        /// below the new hole straight away; only call this while it isn't in play.
        /// </summary>
        public void SetHome(int index, Vector3 localHolePosition)
        {
            holeIndex = index;
            home = localHolePosition;
            HideImmediate();
        }

        // ---------- Lifecycle ----------

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            colliders = GetComponentsInChildren<Collider>(true);
            foreach (Collider c in colliders) c.isTrigger = true;
            renderers = GetComponentsInChildren<Renderer>(true);
            homeRotation = transform.localRotation;

            home = transform.localPosition;
            HideImmediate();
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null) GameManager.Instance.RegisterNut(this);
        }

        private void Start()
        {
            // Covers the case where this nut enabled before the GameManager's Awake ran.
            if (GameManager.Instance != null) GameManager.Instance.RegisterNut(this);
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null) GameManager.Instance.UnregisterNut(this);
        }

        // ---------- Public API ----------

        /// <summary>
        /// Shoots the nut up out of its hole. Returns false if it's already out.
        /// </summary>
        /// <param name="apexHeight">Height of the nut's base above the table at the top of its flight (m).</param>
        /// <param name="drift">Max sideways speed (m/s) for the random drift.</param>
        /// <param name="lifetimeSeconds">Seconds before an unhit nut drops back and counts as missed.</param>
        /// <param name="kind">Normal nut, bomb (explodes when hit) or power-up acorn (collected when hit).</param>
        /// <param name="powerUp">Which power-up, when kind is PowerUp.</param>
        public bool Launch(float apexHeight, float drift, float lifetimeSeconds,
                           NutKind kind = NutKind.Normal, PowerUpType powerUp = PowerUpType.ExtraLife)
        {
            if (State != NutState.Hidden) return false;

            Kind = kind;
            PowerUp = powerUp;
            spinAngle = 0f;
            ApplyLook();

            localPos = home + Vector3.down * hiddenDepth;
            float riseDistance = apexHeight + hiddenDepth;
            Vector2 side = UnityEngine.Random.insideUnitCircle * drift;
            velocity = new Vector3(side.x, Mathf.Sqrt(2f * gravity * riseDistance), side.y);

            aliveTime = 0f;
            lifetime = lifetimeSeconds;
            State = NutState.Rising;
            TeleportTo(localPos);
            SetVisible(true);
            return true;
        }

        /// <summary>Sends the nut back into its hole without counting a hit or a miss (e.g. on game over).</summary>
        public void Retract()
        {
            if (State == NutState.Rising || State == NutState.Up) StartFall(hitKnockSpeed);
        }

        // ---------- Bomb / acorn look ----------

        private void Update()
        {
            if (State == NutState.Hidden) return;

            // Hit squash: flattens and widens for a moment, then springs back.
            if (squashTime >= 0f)
            {
                squashTime += Time.deltaTime;
                float k = squashTime / SquashDuration;
                if (k >= 1f)
                {
                    transform.localScale = squashBase;
                    squashTime = -1f;
                }
                else
                {
                    float s = Mathf.Sin(k * Mathf.PI);
                    transform.localScale = Vector3.Scale(squashBase, new Vector3(1f + 0.35f * s, 1f - 0.45f * s, 1f + 0.35f * s));
                }
            }

            if (IsBomb)
            {
                // Flash the bomb's cap while it's out.
                float t = 0.5f + 0.5f * Mathf.Sin(Time.time * bombFlashRate * Mathf.PI * 2f);
                Color cap = Color.Lerp(bombCapColor, bombCapFlashColor, t);
                foreach (Renderer r in renderers)
                    if (r.name == "Cap") SetRendererColor(r, cap);
            }
            else if (IsPowerUp)
            {
                // Pulse the acorn's body brighter so it reads as "special".
                float t = 0.5f + 0.5f * Mathf.Sin(Time.time * acornPulseRate * Mathf.PI * 2f);
                Color body = Color.Lerp(PowerUpInfo.GetColor(PowerUp), Color.white, t * 0.45f);
                foreach (Renderer r in renderers)
                    if (r.name == "Body") SetRendererColor(r, body);
            }
        }

        private void ApplyLook()
        {
            foreach (Renderer r in renderers)
            {
                switch (Kind)
                {
                    case NutKind.Bomb:
                        switch (r.name)
                        {
                            case "Body": SetRendererColor(r, bombBodyColor); break;
                            case "Cap": SetRendererColor(r, bombCapColor); break;
                            case "Stem": SetRendererColor(r, bombFuseColor); break;
                            default: SetRendererColor(r, bombBodyColor); break;
                        }
                        break;

                    case NutKind.PowerUp:
                        SetRendererColor(r, r.name == "Body" ? PowerUpInfo.GetColor(PowerUp) : acornCapColor);
                        break;

                    default:
                        r.SetPropertyBlock(null); // back to the normal nut materials
                        break;
                }
            }
        }

        /// <summary>Collected: sparkle burst and chime in the power-up's color, and the acorn vanishes into its hole.</summary>
        private void Collect()
        {
            float scale = transform.lossyScale.x;
            PowerUpEffect.Spawn(transform.position, PowerUpInfo.GetColor(PowerUp), collectEffectSize * Mathf.Max(scale, 0.5f));
            HideImmediate();
        }

        private void SetRendererColor(Renderer r, Color c)
        {
            if (lookBlock == null) lookBlock = new MaterialPropertyBlock();
            r.GetPropertyBlock(lookBlock);
            lookBlock.SetColor("_Color", c);
            r.SetPropertyBlock(lookBlock);
        }

        /// <summary>Blows up: explosion effect at the nut, and the nut vanishes back into its hole.</summary>
        private void Explode()
        {
            float scale = transform.lossyScale.x;
            BombExplosion.Spawn(transform.position, explosionSize * Mathf.Max(scale, 0.5f));
            HideImmediate();
        }

        // ---------- Simulation ----------

        private void FixedUpdate()
        {
            if (State == NutState.Hidden) return;
            // Slow-mo power-up: the nut's whole motion and lifetime run at the game speed.
            float speed = GameManager.Instance != null ? GameManager.Instance.GameSpeed : 1f;
            float dt = Time.fixedDeltaTime * speed;

            if (IsPowerUp && rb != null)
            {
                spinAngle += acornSpinSpeed * dt;
                Quaternion local = homeRotation * Quaternion.Euler(0f, spinAngle, 0f);
                rb.MoveRotation(transform.parent != null ? transform.parent.rotation * local : local);
            }

            switch (State)
            {
                case NutState.Rising:
                    velocity.y -= gravity * dt;
                    localPos += velocity * dt;
                    if (velocity.y <= 0f)
                    {
                        // Reached the top: stop and hover until hit or timed out.
                        State = NutState.Up;
                        velocity = Vector3.zero;
                        hoverPos = localPos;
                        hoverTime = 0f;
                    }
                    break;

                case NutState.Up:
                    hoverTime += dt;
                    localPos = hoverPos + Vector3.up * (Mathf.Sin(hoverTime * bobSpeed) * bobAmplitude);
                    break;

                case NutState.Falling:
                    velocity.y -= gravity * dt;
                    localPos += velocity * dt;
                    if (localPos.y <= home.y - hiddenDepth)
                    {
                        HideImmediate();
                        return;
                    }
                    break;
            }

            if (IsHittable)
            {
                aliveTime += dt;
                if (aliveTime >= lifetime)
                {
                    StartFall(0f);
                    Missed?.Invoke(this);
                }
            }

            rb.MovePosition(ToWorld(localPos));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsHittable) return;

            Hammer hammer = other.GetComponentInParent<Hammer>();
            if (hammer == null || !hammer.IsHeld) return; // hammers resting on their stands don't count

            Vector3 hammerVelocity = InputManager.Instance != null ? InputManager.Instance.GetVelocity(hammer.Hand) : Vector3.zero;
            HitPosition = transform.position;

            if (IsBomb)
            {
                Explode();
                Hit?.Invoke(this, hammer, hammerVelocity); // the spawner sees IsBomb and costs a life
                return;
            }
            if (IsPowerUp)
            {
                Collect();
                Hit?.Invoke(this, hammer, hammerVelocity); // the spawner sees IsPowerUp and applies it
                return;
            }

            // A downward swing knocks it down faster.
            float downSpeed = hitKnockSpeed + Mathf.Max(0f, -hammerVelocity.y) * 0.3f;
            StartFall(downSpeed);
            Squash();
            Hit?.Invoke(this, hammer, hammerVelocity);
        }

        /// <summary>
        /// Starts falling under gravity, with sideways speed chosen so the nut lands back in
        /// the center of its hole (undoing the drift) instead of clipping through the table.
        /// </summary>
        private void StartFall(float initialDownSpeed)
        {
            float h = Mathf.Max(0f, localPos.y - home.y);
            float v = initialDownSpeed;
            float timeToTable = (-v + Mathf.Sqrt(v * v + 2f * gravity * h)) / gravity;
            timeToTable = Mathf.Max(timeToTable, 0.05f);

            Vector3 toHole = home - localPos;
            velocity = new Vector3(toHole.x / timeToTable, -v, toHole.z / timeToTable);

            State = NutState.Falling;
            SetCollidersEnabled(false);
        }

        // ---------- Helpers ----------

        private const float SquashDuration = 0.18f;
        private float squashTime = -1f;
        private Vector3 squashBase;

        private void Squash()
        {
            if (squashTime < 0f) squashBase = transform.localScale;
            squashTime = 0f;
        }

        private void HideImmediate()
        {
            if (squashTime >= 0f)
            {
                transform.localScale = squashBase;
                squashTime = -1f;
            }
            State = NutState.Hidden;
            velocity = Vector3.zero;
            localPos = home + Vector3.down * hiddenDepth;
            transform.localRotation = homeRotation; // undo any acorn spin
            TeleportTo(localPos);
            SetVisible(false);
        }

        private void TeleportTo(Vector3 local)
        {
            transform.localPosition = local;
            if (rb != null) rb.position = transform.position;
        }

        private Vector3 ToWorld(Vector3 local) =>
            transform.parent != null ? transform.parent.TransformPoint(local) : local;

        private void SetVisible(bool visible)
        {
            foreach (Renderer r in renderers) r.enabled = visible;
            SetCollidersEnabled(visible);
        }

        private void SetCollidersEnabled(bool on)
        {
            foreach (Collider c in colliders) c.enabled = on;
        }
    }
}
