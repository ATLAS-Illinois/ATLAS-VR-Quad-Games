using TMPro;
using UnityEngine;
using Whacamole.Core;
using Whacamole.Events;

namespace Whacamole.UI
{
    /// <summary>
    /// World-space scoreboard that updates from the score, lives, combo and level. On Play it dresses
    /// itself up as a wooden notice board on posts (like the quad's signposts) and puts a second wooden
    /// "How to play" board beside it with the rules and a key for the nuts, bombs and acorns.
    /// </summary>
    public class Scoreboard : MonoBehaviour
    {
        [SerializeField] private TextMesh text;

        [Header("Listening To")]
        [SerializeField] private IntEventChannelSO scoreChangedChannel;
        [SerializeField] private IntEventChannelSO livesChangedChannel;
        [SerializeField] private IntEventChannelSO comboChangedChannel;
        [SerializeField] private VoidEventChannelSO gameStartedChannel;
        [SerializeField] private GameOverEventChannelSO gameOverChannel;

        [Header("Level Up")]
        [Tooltip("How long (s) \"LEVEL N!\" shows after a level up.")]
        [SerializeField, Min(0f)] private float levelUpMessageTime = 1.5f;
        [Tooltip("How long (s) the message shows on a level up that introduces a new acorn (it has an extra line to read).")]
        [SerializeField, Min(0f)] private float newAcornMessageTime = 3f;

        [Header("Wooden Boards")]
        [Tooltip("Build a wooden board (with posts) for the scoreboard when Play starts. Off = the old flat panel.")]
        [SerializeField] private bool woodenBoard = true;
        [Tooltip("Optional: a wood material to use instead of the generated wood (e.g. the one on Assets/Assets/Signpost.prefab).")]
        [SerializeField] private Material woodMaterial;
        [Tooltip("Size (m) of the scoreboard's wooden face.")]
        [SerializeField] private Vector2 boardSize = new Vector2(1.2f, 0.6f);
        [Tooltip("Build the \"How to play\" board with the rules and the acorn key next to the scoreboard.")]
        [SerializeField] private bool rulesBoard = true;
        [Tooltip("Where the rules board's center sits, relative to the scoreboard (m). +X is the player's right.")]
        [SerializeField] private Vector3 rulesBoardOffset = new Vector3(1.45f, -0.05f, -0.15f);
        [Tooltip("Turn (degrees) of the rules board so it faces the player at the table.")]
        [SerializeField] private float rulesBoardYaw = 25f;
        [Tooltip("Size (m) of the rules board's wooden face.")]
        [SerializeField] private Vector2 rulesBoardSize = new Vector2(1.0f, 1.2f);

        private int score;
        private int lives;
        private int combo;
        private int level = 1;
        private bool gameOver;
        private bool started;
        private float levelMessageUntil;
        private string flashMessage;
        private GameManager levelSource;
        private float nextTimerRefresh;
        private TextMeshPro boardText; // the stats on the wooden board (null = use the old TextMesh)
        private WoodenSign rulesSign;
        private bool timersShown;

        private void OnEnable()
        {
            if (scoreChangedChannel != null) scoreChangedChannel.OnEventRaised += OnScore;
            if (livesChangedChannel != null) livesChangedChannel.OnEventRaised += OnLives;
            if (comboChangedChannel != null) comboChangedChannel.OnEventRaised += OnCombo;
            if (gameStartedChannel != null) gameStartedChannel.OnEventRaised += OnGameStarted;
            if (gameOverChannel != null) gameOverChannel.OnEventRaised += OnGameOver;
            SubscribeToLevels();
            Refresh();
        }

        private void OnDisable()
        {
            if (scoreChangedChannel != null) scoreChangedChannel.OnEventRaised -= OnScore;
            if (livesChangedChannel != null) livesChangedChannel.OnEventRaised -= OnLives;
            if (comboChangedChannel != null) comboChangedChannel.OnEventRaised -= OnCombo;
            if (gameStartedChannel != null) gameStartedChannel.OnEventRaised -= OnGameStarted;
            if (gameOverChannel != null) gameOverChannel.OnEventRaised -= OnGameOver;
            if (levelSource != null)
            {
                levelSource.LevelChanged -= OnLevel;
                levelSource.BombExploded -= OnBomb;
                levelSource.PowerUpCollected -= OnPowerUp;
            }
            levelSource = null;
        }

        private void Start()
        {
            SubscribeToLevels(); // in case the GameManager wasn't ready during OnEnable
            if (levelSource != null) level = levelSource.Level;
            if (woodenBoard) BuildWoodenBoard();
            if (rulesBoard) BuildRulesBoard();
            Refresh();
        }

        private void Update()
        {
            // Swap the "LEVEL N!" message back to the normal stats once its time is up.
            if (levelMessageUntil > 0f && Time.time >= levelMessageUntil)
            {
                levelMessageUntil = 0f;
                Refresh();
            }

            // Keep the power-up countdown line ticking (a few times a second is plenty).
            bool anyTimer = TimerLine().Length > 0;
            if ((anyTimer || timersShown) && Time.time >= nextTimerRefresh)
            {
                nextTimerRefresh = Time.time + 0.2f;
                Refresh();
            }
        }

        private void SubscribeToLevels()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm == levelSource) return;
            if (levelSource != null)
            {
                levelSource.LevelChanged -= OnLevel;
                levelSource.BombExploded -= OnBomb;
                levelSource.PowerUpCollected -= OnPowerUp;
            }
            levelSource = gm;
            levelSource.LevelChanged += OnLevel;
            levelSource.BombExploded += OnBomb;
            levelSource.PowerUpCollected += OnPowerUp;
        }

        private void OnScore(int v) { score = v; Refresh(); }
        private void OnLives(int v) { lives = v; Refresh(); }
        private void OnCombo(int v) { combo = v; Refresh(); }
        private void OnGameStarted() { started = true; gameOver = false; Refresh(); }
        private void OnGameOver(GameResult r) { gameOver = true; levelMessageUntil = 0f; Refresh(); }

        private void OnLevel(int v)
        {
            level = v;
            if (v > 1)
            {
                // No banner for level 1 at the start. Announce any acorn that first appears at this level.
                string intro = "";
                if (levelSource != null)
                    foreach (PowerUpType t in PowerUpInfo.All)
                        if (levelSource.PowerUpUnlockLevel(t) == v) intro += "\n" + PowerUpInfo.GetIntro(t);
                Flash($"LEVEL {v}!" + intro, intro.Length > 0 ? newAcornMessageTime : levelUpMessageTime);
            }
            else levelMessageUntil = 0f;
            Refresh();
        }

        // Lives arrive first via the lives channel, so this shows the updated count.
        private void OnBomb() => Flash("BOOM!  -1 life");

        // Score and lives arrive first via their channels, so this shows the updated numbers.
        private void OnPowerUp(PowerUpType type) => Flash(PowerUpInfo.GetCollectedMessage(type));

        private void Flash(string message) => Flash(message, levelUpMessageTime);

        private void Flash(string message, float seconds)
        {
            flashMessage = message;
            levelMessageUntil = Time.time + seconds;
            Refresh();
        }

        /// <summary>"Big hammer 5s   Slow-mo 3s" for the timed power-ups running now, or "".</summary>
        private string TimerLine()
        {
            if (levelSource == null || gameOver || !started) return "";
            string line = "";
            foreach (PowerUpType t in PowerUpInfo.All)
            {
                float left = levelSource.PowerUpTimeLeft(t);
                if (left <= 0f) continue;
                if (line.Length > 0) line += "   ";
                line += $"{PowerUpInfo.GetName(t)} {Mathf.CeilToInt(left)}s";
            }
            return line;
        }

        // ---------- Wooden boards ----------

        private void BuildWoodenBoard()
        {
            WoodenSign sign = WoodenSign.Build(transform, "WoodenScoreboard", Vector3.zero, 0f, boardSize, woodMaterial);

            float w = boardSize.x, h = boardSize.y;
            sign.AddText("Title", new Rect(0f, h / 2f - 0.07f, w - 0.1f, 0.1f), 0.8f,
                                             TextAlignmentOptions.Center, new Color(1f, 0.82f, 0.35f), "<b>WHAC-A-MOLE</b>");
            boardText = sign.AddText("Stats", new Rect(0f, -0.05f, w - 0.1f, h - 0.2f), 0.9f,
                                     TextAlignmentOptions.Center, WoodenSign.InkColor);
            if (boardText == null)
            {
                // No TMP font: keep the old text, just in front of the wood.
                if (text != null) text.transform.localPosition += Vector3.back * (WoodenSign.BoardDepth / 2f + 0.005f);
                return;
            }

            // Hide the old flat panel and text (the panel keeps its collider).
            Transform panel = transform.Find("Panel");
            if (panel != null && panel.TryGetComponent(out Renderer pr)) pr.enabled = false;
            if (text != null && text.TryGetComponent(out Renderer tr)) tr.enabled = false;
        }

        private void BuildRulesBoard()
        {
            float w = rulesBoardSize.x, h = rulesBoardSize.y;
            rulesSign = WoodenSign.Build(transform, "RulesBoard", rulesBoardOffset, rulesBoardYaw, rulesBoardSize, woodMaterial);
            Color gold = new Color(1f, 0.82f, 0.35f);

            GameSettingsSO s = levelSource != null ? levelSource.Settings : null;
            int points = s != null ? s.pointsPerHit : 10;
            int missPenalty = s != null ? s.missPenalty : 5;
            int lives = s != null ? s.startingLives : 3;
            int step = s != null ? s.hitsPerMultiplierStep : 5;
            int maxMult = s != null ? s.maxMultiplier : 4;
            int levels = s != null ? s.LevelCount : LevelSettings.Defaults().Length;

            // Top part: title and rules.
            float top = h / 2f;
            rulesSign.AddText("Title", new Rect(0f, top - 0.07f, w - 0.1f, 0.1f), 0.85f,
                              TextAlignmentOptions.Center, gold, "<b>HOW TO PLAY</b>");
            string rules =
                "- Stand at the table and press <b>A</b> to start\n" +
                "- Grab a hammer from a stand (<b>grip</b>)\n" +
                $"- Whack nuts before they sink: <b>+{points}</b>\n" +
                $"- Miss a nut: <b>-1 life</b>, -{missPenalty} pts\n" +
                $"- {step} hits in a row = bigger multiplier (up to x{maxMult})\n" +
                $"- Hit enough nuts to level up ({levels} levels)\n" +
                $"- You start with {lives} lives. Lose them all and press <b>A</b> to retry your level";
            float rulesTop = top - 0.13f, rulesBottom = -0.02f;
            rulesSign.AddText("Rules", new Rect(0f, (rulesTop + rulesBottom) / 2f, w - 0.1f, rulesTop - rulesBottom), 0.45f,
                              TextAlignmentOptions.TopLeft, WoodenSign.InkColor, rules);

            // Bottom part: the key, one row per kind of pop-up, each with a little 3D model.
            rulesSign.AddText("KeyTitle", new Rect(0f, -0.07f, w - 0.1f, 0.07f), 0.6f,
                              TextAlignmentOptions.Center, gold, "<b>KEY</b>");

            var rows = new System.Collections.Generic.List<(WoodenSign.IconKind kind, Color color, string label)>
            {
                (WoodenSign.IconKind.Nut, new Color(0.62f, 0.38f, 0.16f), "<b>Nut</b>: whack it!"),
                (WoodenSign.IconKind.Bomb, new Color(0.07f, 0.07f, 0.08f), "<b><color=#FF5040>Bomb</color></b>: DON'T hit it (-1 life)"),
            };
            foreach (PowerUpType t in PowerUpInfo.All)
            {
                int unlock = levelSource != null ? levelSource.PowerUpUnlockLevel(t) : 1;
                Color c = PowerUpInfo.GetColor(t);
                string hex = ColorUtility.ToHtmlStringRGB(c);
                rows.Add((WoodenSign.IconKind.Acorn, c, $"<b><color=#{hex}>{AcornName(t)}</color></b>: {AcornEffect(t, s)} <size=75%>(level {unlock}+)</size>"));
            }

            float keyTop = -0.12f, keyBottom = -h / 2f + 0.03f;
            float rowH = (keyTop - keyBottom) / rows.Count;
            float iconSize = Mathf.Min(rowH * 0.8f, 0.075f);
            for (int i = 0; i < rows.Count; i++)
            {
                float y = keyTop - rowH * (i + 0.5f);
                rulesSign.AddIcon(rows[i].kind, new Vector2(-w / 2f + 0.08f, y), iconSize, rows[i].color);
                float textLeft = -w / 2f + 0.15f, textRight = w / 2f - 0.04f;
                rulesSign.AddText("Key_" + i, new Rect((textLeft + textRight) / 2f, y, textRight - textLeft, rowH * 0.95f), 0.42f,
                                  TextAlignmentOptions.Left, WoodenSign.InkColor, rows[i].label);
            }
        }

        private static string AcornName(PowerUpType t)
        {
            switch (t)
            {
                case PowerUpType.ExtraLife: return "Green acorn";
                case PowerUpType.BigHammer: return "Purple acorn";
                case PowerUpType.SlowTime: return "Blue acorn";
                default: return PowerUpInfo.GetName(t);
            }
        }

        private static string AcornEffect(PowerUpType t, GameSettingsSO s)
        {
            switch (t)
            {
                case PowerUpType.ExtraLife: return "+1 life";
                case PowerUpType.BigHammer: return $"big hammer for {(s != null ? s.bigHammerDuration : 8f):0}s";
                case PowerUpType.SlowTime: return $"slow-mo for {(s != null ? s.slowTimeDuration : 6f):0}s";
                default: return PowerUpInfo.GetName(t);
            }
        }

        private int RetryLevel() => levelSource != null ? levelSource.NextStartLevel : level;

        private void Refresh()
        {
            if (text == null && boardText == null) return;
            bool wood = boardText != null; // the wooden board already shows the title and can use TMP rich text
            string s;
            if (gameOver)
                s = wood ? $"<color=#FF7050><b>GAME OVER</b></color>  (Level {level})\nScore {score}\n<size=80%>Press A to retry level {RetryLevel()}</size>"
                         : $"GAME OVER  (Level {level})\nScore {score}\nPress A to retry level {RetryLevel()}";
            else if (!started)
                s = wood ? "Press <b>A</b> to start\n<size=70%>(stand at the table)</size>"
                         : "WHAC-A-MOLE\nPress A to start"; // waiting for the player (quad scene)
            else if (levelMessageUntil > 0f)
                s = wood ? $"<color=#FFD15A><b>{flashMessage}</b></color>\nScore {score}   Lives {lives}"
                         : $"{flashMessage}\nScore {score}   Lives {lives}";
            else
                s = $"Score {score}   Level {level}\nLives {lives}   Combo {combo}";

            string timers = TimerLine();
            timersShown = timers.Length > 0;
            if (timersShown) s += wood ? $"\n<size=75%><color=#9FE8FF>{timers}</color></size>" : "\n" + timers;

            if (wood) boardText.text = s;
            else text.text = s;
        }
    }
}
