using System;
using System.Collections.Generic;
using UnityEngine;
using Whacamole.Events;
using Whacamole.Gameplay;

namespace Whacamole.Core
{
    /// <summary>
    /// Owns the game state: score, lives and combo, plus the nuts and hammers in play.
    /// Nuts and hammers register themselves when enabled.
    /// Listens for nut hit / missed events and broadcasts game start, game over and stat changes.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private GameSettingsSO settings;
        [Tooltip("Start a run automatically when entering Play mode. Turn off once there's a start menu.")]
        [SerializeField] private bool startOnPlay = true;

        [Header("Start / Restart")]
        [Tooltip("Press A (right controller) to start, and to play again after game over. Editor: Space also works.")]
        [SerializeField] private bool allowRestart = true;
        [Tooltip("Optional. If set, A only starts/restarts the game when the player is within Play Area Range of this point " +
                 "(used in the quad scene so A doesn't start the game from across the map, and isn't stolen from other games).")]
        [SerializeField] private Transform playArea;
        [Tooltip("How close (m, measured flat on the ground) the player's head must be to Play Area for A to start the game.")]
        [SerializeField, Min(0.5f)] private float playAreaRange = 4f;
        [Tooltip("Seconds after game over before A can restart, so a button press mid-swing doesn't skip the game over screen.")]
        [SerializeField, Min(0f)] private float restartDelay = 1f;
        [Tooltip("After a game over, restart from the level you lost on (score and lives reset). Off = always restart from level 1.")]
        [SerializeField] private bool restartAtLevelReached = true;

        [Header("Listening To")]
        [SerializeField] private NutHitEventChannelSO nutHitChannel;
        [SerializeField] private NutMissedEventChannelSO nutMissedChannel;

        [Header("Broadcasting On")]
        [SerializeField] private VoidEventChannelSO gameStartedChannel;
        [SerializeField] private GameOverEventChannelSO gameOverChannel;
        [SerializeField] private IntEventChannelSO scoreChangedChannel;
        [SerializeField] private IntEventChannelSO livesChangedChannel;
        [SerializeField] private IntEventChannelSO comboChangedChannel;

        public GameState State { get; private set; } = GameState.Idle;
        public int Score { get; private set; }
        public int Lives { get; private set; }
        public int Combo { get; private set; }

        /// <summary>Current level, starting at 1. See the Levels list on the GameSettings asset.</summary>
        public int Level { get; private set; } = 1;
        /// <summary>Hits made since the current level started.</summary>
        public int HitsThisLevel { get; private set; }
        /// <summary>Difficulty settings for the current level.</summary>
        public LevelSettings CurrentLevel => settings != null ? settings.GetLevel(Level) : LevelSettings.Defaults()[0];
        /// <summary>Raised with the new level number when a game starts (level 1) and on every level up.</summary>
        public event Action<int> LevelChanged;

        /// <summary>The level the next game starts on: 1 for a fresh session, or the level you lost on (see Restart At Level Reached).</summary>
        public int NextStartLevel { get; private set; } = 1;
        public int MaxCombo { get; private set; }
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        /// <summary>Bombs hit this game.</summary>
        public int BombsHit { get; private set; }
        /// <summary>Raised when a bomb is hit (after the life is taken).</summary>
        public event Action BombExploded;

        // ---------- Power-ups ----------

        /// <summary>Raised when an acorn power-up is collected (after its effect is applied).</summary>
        public event Action<PowerUpType> PowerUpCollected;
        /// <summary>Raised when a timed power-up (big hammer, slow-mo) wears off.</summary>
        public event Action<PowerUpType> PowerUpEnded;
        /// <summary>A nut was whacked: (where, points scored, multiplier, swing speed m/s). For the hit effects.</summary>
        public event Action<Vector3, int, int, float> NutScored;
        /// <summary>A nut got away: (where, points lost). For the miss effects.</summary>
        public event Action<Vector3, int> NutLost;
        /// <summary>The score multiplier went up (new multiplier).</summary>
        public event Action<int> MultiplierUp;

        /// <summary>
        /// Speed of the nuts and pop-ups: 1 normally, lower during slow-mo. Nut motion and spawn timing
        /// are multiplied by this (Time.timeScale is left alone so the player's movement isn't slowed).
        /// </summary>
        public float GameSpeed => slowTimeLeft > 0f && settings != null ? settings.slowTimeFactor : 1f;

        private float bigHammerLeft;
        private float slowTimeLeft;

        /// <summary>Seconds left on a timed power-up (0 if it isn't active; extra life is instant, always 0).</summary>
        public float PowerUpTimeLeft(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.BigHammer: return bigHammerLeft;
                case PowerUpType.SlowTime: return slowTimeLeft;
                default: return 0f;
            }
        }

        /// <summary>True if this acorn can appear at the current level (and, for extra life, if you're below max lives).</summary>
        /// <summary>The level an acorn kind first appears at (from GameSettings).</summary>
        public int PowerUpUnlockLevel(PowerUpType type) => settings != null ? settings.GetUnlockLevel(type) : int.MaxValue;

        public bool CanSpawnPowerUp(PowerUpType type)
        {
            if (settings == null || Level < settings.GetUnlockLevel(type)) return false;
            if (type == PowerUpType.ExtraLife && Lives >= settings.maxLives) return false;
            return true;
        }

        /// <summary>Called by the NutSpawner when a hammer hits an acorn power-up.</summary>
        public void CollectPowerUp(PowerUpType type)
        {
            if (!IsPlaying || settings == null) return;

            SetScore(Score + settings.powerUpPoints); // combo is left alone

            switch (type)
            {
                case PowerUpType.ExtraLife:
                    SetLives(Mathf.Min(Lives + 1, Mathf.Max(settings.maxLives, Lives)));
                    break;
                case PowerUpType.BigHammer:
                    bigHammerLeft += settings.bigHammerDuration;
                    SetHammerSize(settings.bigHammerScale);
                    break;
                case PowerUpType.SlowTime:
                    slowTimeLeft += settings.slowTimeDuration;
                    break;
            }

            PowerUpCollected?.Invoke(type);
        }

        private void TickPowerUps(float dt)
        {
            if (bigHammerLeft > 0f)
            {
                bigHammerLeft -= dt;
                if (bigHammerLeft <= 0f)
                {
                    bigHammerLeft = 0f;
                    SetHammerSize(1f);
                    PlayPowerDown();
                    PowerUpEnded?.Invoke(PowerUpType.BigHammer);
                }
            }
            if (slowTimeLeft > 0f)
            {
                slowTimeLeft -= dt;
                if (slowTimeLeft <= 0f)
                {
                    slowTimeLeft = 0f;
                    PlayPowerDown();
                    PowerUpEnded?.Invoke(PowerUpType.SlowTime);
                }
            }
        }

        /// <summary>Ends every power-up straight away (new game / game over).</summary>
        private void ClearPowerUps()
        {
            bool hadBigHammer = bigHammerLeft > 0f, hadSlowTime = slowTimeLeft > 0f;
            bigHammerLeft = 0f;
            slowTimeLeft = 0f;
            SetHammerSize(1f);
            // Let the effects (hammer glow, slow-mo dome) switch off too.
            if (hadBigHammer) PowerUpEnded?.Invoke(PowerUpType.BigHammer);
            if (hadSlowTime) PowerUpEnded?.Invoke(PowerUpType.SlowTime);
        }

        private void SetHammerSize(float multiplier)
        {
            foreach (Hammer h in hammers)
                if (h != null) h.SetSizeMultiplier(multiplier);
        }

        private void PlayPowerDown()
        {
            Vector3 at = playArea != null ? playArea.position : transform.position;
            WhacSounds.Play(WhacSound.PowerDown, at + Vector3.up, 0.7f);
        }
        public float ElapsedTime => State == GameState.Playing ? Time.time - startTime : lastRunDuration;

        /// <summary>Current score multiplier from the combo (1 = no bonus).</summary>
        public int Multiplier
        {
            get
            {
                if (settings == null) return 1;
                int m = 1 + Combo / settings.hitsPerMultiplierStep;
                return Mathf.Min(m, settings.maxMultiplier);
            }
        }

        public bool IsPlaying => State == GameState.Playing;
        /// <summary>The settings asset in use (may be null), e.g. for the rules board.</summary>
        public GameSettingsSO Settings => settings;

        // ---------- Nuts & hammers ----------

        private readonly List<Nut> nuts = new List<Nut>();
        private readonly List<Hammer> hammers = new List<Hammer>();

        /// <summary>All nuts currently in the scene, sorted by hole index.</summary>
        public IReadOnlyList<Nut> Nuts => nuts;
        public int NutCount => nuts.Count;

        /// <summary>All hammers in the scene, held or resting.</summary>
        public IReadOnlyList<Hammer> Hammers => hammers;

        /// <summary>The hammer held in the given hand, or null if that hand is empty.</summary>
        public Hammer GetHammer(Hand hand)
        {
            foreach (Hammer h in hammers)
                if (h != null && h.IsHeld && h.Hand == hand) return h;
            return null;
        }

        public void RegisterNut(Nut nut)
        {
            if (nut == null || nuts.Contains(nut)) return;
            nuts.Add(nut);
            nuts.Sort((a, b) => a.HoleIndex.CompareTo(b.HoleIndex));
        }

        public void UnregisterNut(Nut nut) => nuts.Remove(nut);

        public void RegisterHammer(Hammer hammer)
        {
            if (hammer == null || hammers.Contains(hammer)) return;
            hammers.Add(hammer);
        }

        public void UnregisterHammer(Hammer hammer) => hammers.Remove(hammer);

        private float startTime;
        private float lastRunDuration;
        private float gameOverTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] Another GameManager exists in the scene. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (settings == null)
                Debug.LogError("[GameManager] No GameSettings assigned.", this);
            else
                WhacSounds.Volume = settings.soundVolume;

            WhacEffects.Ensure(); // hit / combo / power-up / level-up visuals
        }

        private void OnEnable()
        {
            if (nutHitChannel != null) nutHitChannel.OnEventRaised += HandleNutHit;
            if (nutMissedChannel != null) nutMissedChannel.OnEventRaised += HandleNutMissed;
        }

        private void OnDisable()
        {
            if (nutHitChannel != null) nutHitChannel.OnEventRaised -= HandleNutHit;
            if (nutMissedChannel != null) nutMissedChannel.OnEventRaised -= HandleNutMissed;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            ResetStats();
            if (startOnPlay) StartGame();
        }

        private void Update()
        {
            if (State == GameState.Playing) TickPowerUps(Time.deltaTime);

            // A starts the game from Idle (when Start On Play is off) and restarts it after game over.
            if (!allowRestart || State == GameState.Playing) return;
            if (State == GameState.GameOver && Time.time - gameOverTime < restartDelay) return;

            bool pressed = OVRInput.GetDown(OVRInput.Button.One); // A on the right controller
#if UNITY_EDITOR
            pressed |= Input.GetKeyDown(KeyCode.Space);           // for testing without a headset
#endif
            if (pressed && IsPlayerInPlayArea()) StartGame();
        }

        private Transform playerHead;

        // The player's headset position: the OVR rig's center eye (the quad scenes' "Main Camera" object is disabled),
        // falling back to Camera.main.
        private Transform GetPlayerHead()
        {
            if (playerHead != null && playerHead.gameObject.activeInHierarchy) return playerHead;

            OVRCameraRig rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null && rig.centerEyeAnchor != null) playerHead = rig.centerEyeAnchor;
            else if (Camera.main != null) playerHead = Camera.main.transform;
            return playerHead;
        }

        /// <summary>True if no Play Area is set, or the player's head is close enough to it.</summary>
        public bool IsPlayerInPlayArea()
        {
            if (playArea == null) return true;

            Transform head = GetPlayerHead();
            if (head == null) return true; // can't tell where the player is; don't block the game

            Vector3 offset = head.position - playArea.position;
            offset.y = 0f;
            return offset.magnitude <= playAreaRange;
        }

        // ---------- Public API ----------

        [ContextMenu("Start Game")]
        public void StartGame()
        {
            if (State == GameState.Playing) return;

            ResetStats();
            Level = Mathf.Max(1, NextStartLevel); // pick up from the level you lost on
            State = GameState.Playing;
            startTime = Time.time;

            BroadcastStats();
            LevelChanged?.Invoke(Level); // sets the board up for the starting level (1, or the level you lost on)
            if (gameStartedChannel != null) gameStartedChannel.Raise();
        }

        [ContextMenu("End Game")]
        public void EndGame()
        {
            if (State != GameState.Playing) return;

            State = GameState.GameOver;
            lastRunDuration = Time.time - startTime;
            NextStartLevel = restartAtLevelReached ? Level : 1;
            gameOverTime = Time.time;
            ClearPowerUps();

            if (gameOverChannel != null)
                gameOverChannel.Raise(new GameResult(Score, MaxCombo, Hits, Misses, lastRunDuration));
        }

        // ---------- Event handlers ----------

        private void HandleNutHit(NutHitData data)
        {
            if (!IsPlaying) return;

            Hits++;
            int multiplierBefore = Multiplier;
            SetCombo(Combo + 1);

            int basePoints = settings != null ? settings.pointsPerHit : 0;
            int points = basePoints * Multiplier;
            SetScore(Score + points);
            NutScored?.Invoke(data.Position, points, Multiplier, data.HitSpeed);
            if (Multiplier > multiplierBefore) MultiplierUp?.Invoke(Multiplier);

            // Level up after enough hits in this level (the last level repeats forever).
            HitsThisLevel++;
            int levelCount = settings != null ? settings.LevelCount : LevelSettings.Defaults().Length;
            if (Level < levelCount && HitsThisLevel >= CurrentLevel.hitsToAdvance)
            {
                Level++;
                HitsThisLevel = 0;
                LevelChanged?.Invoke(Level);
            }
        }

        /// <summary>
        /// Called by the NutSpawner when a hammer hits a bomb: costs a life and resets the combo.
        /// Doesn't count as a hit (no points, no progress toward the next level).
        /// </summary>
        public void BombHit()
        {
            if (!IsPlaying) return;

            BombsHit++;
            SetCombo(0);
            SetLives(Lives - 1);
            BombExploded?.Invoke();

            if (Lives <= 0) EndGame();
        }

        private void HandleNutMissed(NutMissedData data)
        {
            if (!IsPlaying) return;

            Misses++;
            SetCombo(0);

            // Letting a nut get away costs points (half a hit by default).
            int penalty = settings != null ? settings.missPenalty : 0;
            SetScore(Score - penalty);
            NutLost?.Invoke(data.Position, penalty);

            SetLives(Lives - 1);

            if (Lives <= 0) EndGame();
        }

        // ---------- Helpers ----------

        private void ResetStats()
        {
            Score = 0;
            Combo = 0;
            MaxCombo = 0;
            Hits = 0;
            Misses = 0;
            Lives = settings != null ? settings.startingLives : 3;
            Level = 1;
            HitsThisLevel = 0;
            BombsHit = 0;
            ClearPowerUps();
        }

        private void BroadcastStats()
        {
            if (scoreChangedChannel != null) scoreChangedChannel.Raise(Score);
            if (livesChangedChannel != null) livesChangedChannel.Raise(Lives);
            if (comboChangedChannel != null) comboChangedChannel.Raise(Combo);
        }

        private void SetScore(int value)
        {
            Score = Mathf.Max(0, value);
            if (scoreChangedChannel != null) scoreChangedChannel.Raise(Score);
        }

        private void SetLives(int value)
        {
            Lives = Mathf.Max(0, value);
            if (livesChangedChannel != null) livesChangedChannel.Raise(Lives);
        }

        private void SetCombo(int value)
        {
            Combo = Mathf.Max(0, value);
            if (Combo > MaxCombo) MaxCombo = Combo;
            if (comboChangedChannel != null) comboChangedChannel.Raise(Combo);
        }
    }
}
