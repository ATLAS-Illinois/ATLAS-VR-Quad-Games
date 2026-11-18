using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Whacamole.Controls;
using Whacamole.Core;
using Whacamole.Events;

namespace Whacamole.Gameplay
{
    /// <summary>
    /// Pops nuts out of random empty holes in waves while a game is running. Each nut only stays out
    /// for a short time; if it isn't hit in time it drops back and counts as a miss.
    /// The timing, board size and nut size come from the current level (Levels list on the GameSettings
    /// asset). On a level up it pulls every nut down, rebuilds the board, pauses briefly, then carries on.
    /// Starts on the GameStarted event, stops (and pulls all nuts back down) on GameOver.
    /// Turns nut hits/misses into NutHit / NutMissed events for the rest of the game.
    /// </summary>
    public class NutSpawner : MonoBehaviour
    {
        // Field names changed from minSpawnInterval / maxSpawnInterval / nutLifetime (testing values 1-10 s and 5 s)
        // so the old values saved in the scene are dropped and these harder defaults apply.
        // Wave interval halved from 2-4 s to 1-2 s (2x spawning speed).
        // These are only used if there's no GameManager; normally each level's settings replace them.
        [Header("Timing (overridden by the current level)")]
        [Tooltip("Shortest wait between pop-up waves (s).")]
        [SerializeField, Min(0.1f)] private float minWaveInterval = 1f;
        [Tooltip("Longest wait between pop-up waves (s).")]
        [SerializeField, Min(0.1f)] private float maxWaveInterval = 2f;
        [Tooltip("Fewest nuts that pop up in one wave.")]
        [SerializeField, Min(1)] private int minNutsPerWave = 1;
        [Tooltip("Most nuts that pop up in one wave.")]
        [SerializeField, Min(1)] private int maxNutsPerWave = 2;
        [Tooltip("Shortest time (s) a nut stays out, counted from when it pops up. Unhit nuts then drop back and count as a miss.")]
        [SerializeField, Min(0.1f)] private float minNutUpTime = 1f;
        [Tooltip("Longest time (s) a nut stays out, counted from when it pops up.")]
        [SerializeField, Min(0.1f)] private float maxNutUpTime = 2f;

        [Header("Launch")]
        [Tooltip("Lowest point the bottom of a nut reaches above the table (m).")]
        [SerializeField, Min(0.05f)] private float minApexHeight = 0.22f;
        [Tooltip("Highest point the bottom of a nut reaches above the table (m).")]
        [SerializeField, Min(0.05f)] private float maxApexHeight = 0.30f;
        [Tooltip("Max random sideways speed on launch (m/s). Small values give a little drift.")]
        [SerializeField, Min(0f)] private float drift = 0.08f;

        [Header("Testing")]
        [Tooltip("Start spawning as soon as Play mode starts, without waiting for GameStarted. Turn off once the full game flow is wired up.")]
        [SerializeField] private bool spawnOnPlay = true;

        [Header("Haptics")]
        [SerializeField, Range(0f, 1f)] private float hitHapticAmplitude = 0.6f;
        [SerializeField, Min(0f)] private float hitHapticDuration = 0.08f;

        [Header("Bombs (overridden by the current level)")]
        [Tooltip("Chance (0-1) that a pop-up is a bomb. At most one bomb per wave.")]
        [SerializeField, Range(0f, 1f)] private float bombChance = 0.1f;
        [Tooltip("Controller rumble when a bomb is hit.")]
        [SerializeField, Range(0f, 1f)] private float bombHapticAmplitude = 1f;
        [SerializeField, Min(0f)] private float bombHapticDuration = 0.35f;

        [Header("Power-up acorns (overridden by the current level)")]
        [Tooltip("Chance (0-1) that a wave includes a power-up acorn. At most one acorn per wave; which kinds can appear depends on the level (see GameSettings).")]
        [SerializeField, Range(0f, 1f)] private float powerUpChance = 0f;
        [Tooltip("Controller rumble when an acorn is collected.")]
        [SerializeField, Range(0f, 1f)] private float powerUpHapticAmplitude = 0.4f;
        [SerializeField, Min(0f)] private float powerUpHapticDuration = 0.15f;

        [Header("Sound")]
        [Tooltip("Volume of the knock when a normal nut is hit (multiplied by the master volume in GameSettings).")]
        [SerializeField, Range(0f, 1f)] private float hitSoundVolume = 0.7f;

        [Header("Levels")]
        [Tooltip("The board to resize between levels. Found (or added to WhackBoard) automatically if empty.")]
        [SerializeField] private BoardLayout board;
        [Tooltip("Pause (s) after a level up before nuts start popping up again.")]
        [SerializeField, Min(0f)] private float levelUpPause = 1.5f;

        [Header("Listening To")]
        [SerializeField] private VoidEventChannelSO gameStartedChannel;
        [SerializeField] private GameOverEventChannelSO gameOverChannel;

        [Header("Broadcasting On")]
        [SerializeField] private NutHitEventChannelSO nutHitChannel;
        [SerializeField] private NutMissedEventChannelSO nutMissedChannel;

        public bool IsSpawning { get; private set; }

        private readonly List<Nut> nuts = new List<Nut>();
        private readonly List<Nut> hiddenBuffer = new List<Nut>();
        private float spawnTimer; // counts down to the next wave; runs slower during slow-mo
        private readonly List<PowerUpType> powerUpBuffer = new List<PowerUpType>();
        private bool transitioning;
        private Coroutine transition;
        private GameManager levelSource;

        // ---------- Lifecycle ----------

        private void OnEnable()
        {
            if (gameStartedChannel != null) gameStartedChannel.OnEventRaised += StartSpawning;
            if (gameOverChannel != null) gameOverChannel.OnEventRaised += HandleGameOver;
            SubscribeToLevels();
        }

        private void OnDisable()
        {
            if (gameStartedChannel != null) gameStartedChannel.OnEventRaised -= StartSpawning;
            if (gameOverChannel != null) gameOverChannel.OnEventRaised -= HandleGameOver;
            if (levelSource != null) levelSource.LevelChanged -= HandleLevelChanged;
            levelSource = null;
            UnhookNuts();
        }

        private void Start()
        {
            // Make sure the board (and its PlayerBlocker) exists from the start, not only once a game begins.
            RefreshNuts();
            GetBoard();

            SubscribeToLevels(); // in case the GameManager wasn't ready during OnEnable
            if (spawnOnPlay) StartSpawning();
        }

        private void Update()
        {
            if (!IsSpawning || transitioning) return;
            float speed = GameManager.Instance != null ? GameManager.Instance.GameSpeed : 1f;
            spawnTimer -= Time.deltaTime * speed;
            if (spawnTimer > 0f) return;
            SpawnWave();
            ScheduleNext();
        }

        // ---------- Public API ----------

        [ContextMenu("Start Spawning")]
        public void StartSpawning()
        {
            if (IsSpawning) return;
            RefreshNuts();
            SubscribeToLevels();
            // If no level change is already being applied (e.g. the game started before we subscribed), set up the current level now.
            if (!transitioning && GameManager.Instance != null) ApplyLevel(GameManager.Instance.CurrentLevel);
            IsSpawning = true;
            spawnTimer = 0f; // first nut right away
        }

        [ContextMenu("Stop Spawning")]
        public void StopSpawning()
        {
            IsSpawning = false;
            foreach (Nut nut in nuts)
                if (nut != null) nut.Retract();
        }

        // ---------- Internals ----------

        private void SpawnWave()
        {
            int count = Random.Range(minNutsPerWave, Mathf.Max(minNutsPerWave, maxNutsPerWave) + 1);
            bool bombUsed = false;

            // At most one acorn per wave, and only kinds unlocked at this level (no +1 life at max lives).
            int acornSlot = -1;
            PowerUpType acornType = PowerUpType.ExtraLife;
            if (powerUpChance > 0f && Random.value < powerUpChance && PickPowerUp(out acornType))
                acornSlot = Random.Range(0, count);

            for (int i = 0; i < count; i++)
            {
                Nut.NutKind kind = Nut.NutKind.Normal;
                if (i == acornSlot) kind = Nut.NutKind.PowerUp;
                else if (!bombUsed && Random.value < bombChance) kind = Nut.NutKind.Bomb; // at most one bomb per wave

                if (!SpawnOne(kind, acornType)) break; // every hole is busy
                bombUsed |= kind == Nut.NutKind.Bomb;
            }
        }

        private bool PickPowerUp(out PowerUpType type)
        {
            type = PowerUpType.ExtraLife;
            GameManager gm = GameManager.Instance;
            if (gm == null) return false;

            powerUpBuffer.Clear();
            foreach (PowerUpType t in PowerUpInfo.All)
                if (gm.CanSpawnPowerUp(t)) powerUpBuffer.Add(t);
            if (powerUpBuffer.Count == 0) return false;

            type = powerUpBuffer[Random.Range(0, powerUpBuffer.Count)];
            return true;
        }

        private bool SpawnOne(Nut.NutKind kind = Nut.NutKind.Normal, PowerUpType powerUp = PowerUpType.ExtraLife)
        {
            hiddenBuffer.Clear();
            foreach (Nut nut in nuts)
                if (nut != null && nut.State == Nut.NutState.Hidden) hiddenBuffer.Add(nut);

            if (hiddenBuffer.Count == 0) return false; // every hole is busy

            Nut pick = hiddenBuffer[Random.Range(0, hiddenBuffer.Count)];
            float apex = Random.Range(minApexHeight, Mathf.Max(minApexHeight, maxApexHeight));
            float upTime = Random.Range(minNutUpTime, Mathf.Max(minNutUpTime, maxNutUpTime));
            return pick.Launch(apex, drift, upTime, kind, powerUp);
        }

        private void ScheduleNext()
        {
            spawnTimer = Random.Range(minWaveInterval, Mathf.Max(minWaveInterval, maxWaveInterval));
        }

        private void HandleGameOver(GameResult result) => StopSpawning();

        // ---------- Levels ----------

        private void SubscribeToLevels()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm == levelSource) return;
            if (levelSource != null) levelSource.LevelChanged -= HandleLevelChanged;
            levelSource = gm;
            levelSource.LevelChanged += HandleLevelChanged;
        }

        private void HandleLevelChanged(int level)
        {
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(LevelTransition(level));
        }

        private IEnumerator LevelTransition(int level)
        {
            transitioning = true;

            // Pull everything back into the board (doesn't count as misses), then wait for it to land.
            foreach (Nut nut in nuts)
                if (nut != null) nut.Retract();
            float waited = 0f;
            while (AnyNutOut() && waited < 2f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (GameManager.Instance != null) ApplyLevel(GameManager.Instance.CurrentLevel);
            if (level > 1) WhacSounds.Play(WhacSound.LevelUp, board != null ? board.transform.position + Vector3.up : transform.position);

            yield return new WaitForSeconds(level > 1 ? levelUpPause : 0.5f);

            transitioning = false;
            transition = null;
            spawnTimer = 0f;
        }

        private bool AnyNutOut()
        {
            foreach (Nut nut in nuts)
                if (nut != null && nut.isActiveAndEnabled && nut.State != Nut.NutState.Hidden) return true;
            return false;
        }

        /// <summary>Copies a level's timing into the spawner and resizes the board for it.</summary>
        private void ApplyLevel(LevelSettings level)
        {
            if (level == null) return;

            minWaveInterval = level.minWaveInterval;
            maxWaveInterval = level.maxWaveInterval;
            minNutsPerWave = level.minNutsPerWave;
            maxNutsPerWave = level.maxNutsPerWave;
            minNutUpTime = level.minNutUpTime;
            maxNutUpTime = level.maxNutUpTime;
            bombChance = level.bombChance;
            powerUpChance = level.powerUpChance;

            if (nuts.Count == 0) RefreshNuts();
            BoardLayout layout = GetBoard();
            if (layout != null && layout.Apply(level.rows, level.columns, level.nutScale))
                RefreshNuts(); // the set of nuts in play may have changed
        }

        private BoardLayout GetBoard()
        {
            if (board != null) return board;

            board = FindFirstObjectByType<BoardLayout>();
            if (board != null) return board;

            // Add it to WhackBoard (the parent of the Holes object the nuts sit in).
            foreach (Nut nut in nuts)
            {
                Transform holes = nut != null ? nut.transform.parent : null;
                if (holes != null && holes.parent != null)
                {
                    board = holes.parent.gameObject.AddComponent<BoardLayout>();
                    return board;
                }
            }

            Debug.LogWarning("[NutSpawner] Couldn't find the board to resize between levels.", this);
            return null;
        }

        private void HandleNutHit(Nut nut, Hammer hammer, Vector3 hammerVelocity)
        {
            if (nut.IsBomb)
            {
                // Boom: costs a life (handled by the GameManager), not a hit.
                if (GameManager.Instance != null) GameManager.Instance.BombHit();
                FloatingText.Spawn("BOOM!  -1 life", nut.HitPosition + Vector3.up * 0.2f, new Color(1f, 0.4f, 0.2f), 0.06f, 1.2f, 0.3f, true);
                if (InputManager.Instance != null)
                    InputManager.Instance.SendHaptic(hammer.Hand, bombHapticAmplitude, bombHapticDuration);
                return;
            }

            if (nut.IsPowerUp)
            {
                // Acorn: apply the power-up (the acorn plays its own sparkle and chime). Not a scoring hit, so the combo is untouched.
                if (GameManager.Instance != null) GameManager.Instance.CollectPowerUp(nut.PowerUp);
                if (WhacEffects.Instance != null) WhacEffects.Instance.PowerUpCollectedAt(nut.PowerUp, nut.HitPosition);
                if (InputManager.Instance != null)
                    InputManager.Instance.SendHaptic(hammer.Hand, powerUpHapticAmplitude, powerUpHapticDuration);
                return;
            }

            WhacSounds.Play(WhacSound.Hit, nut.transform.position, hitSoundVolume);

            if (nutHitChannel != null)
                nutHitChannel.Raise(new NutHitData(nut.transform.position, hammerVelocity, hammer.Hand));

            if (InputManager.Instance != null)
                InputManager.Instance.SendHaptic(hammer.Hand, hitHapticAmplitude, hitHapticDuration);
        }

        private void HandleNutMissed(Nut nut)
        {
            if (nut.IsBomb || nut.IsPowerUp) return; // letting a bomb or acorn sink back costs nothing

            if (nutMissedChannel != null)
                nutMissedChannel.Raise(new NutMissedData(nut.transform.position));
        }

        private void RefreshNuts()
        {
            UnhookNuts();
            nuts.Clear();

            if (GameManager.Instance != null && GameManager.Instance.NutCount > 0)
                nuts.AddRange(GameManager.Instance.Nuts);
            else
                nuts.AddRange(FindObjectsByType<Nut>(FindObjectsSortMode.None));

            foreach (Nut nut in nuts)
            {
                nut.Hit += HandleNutHit;
                nut.Missed += HandleNutMissed;
            }

            if (nuts.Count == 0)
                Debug.LogWarning("[NutSpawner] No nuts found in the scene. Run Whacamole > Build Scene.", this);
        }

        private void UnhookNuts()
        {
            foreach (Nut nut in nuts)
            {
                if (nut == null) continue;
                nut.Hit -= HandleNutHit;
                nut.Missed -= HandleNutMissed;
            }
        }
    }
}
