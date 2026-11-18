using UnityEngine;

namespace Whacamole.Core
{
    /// <summary>Tunable rules for scoring, lives and combos. Edit the GameSettings asset to rebalance.</summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "Whacamole/Game Settings")]
    public class GameSettingsSO : ScriptableObject
    {
        [Header("Lives")]
        [Min(1)] public int startingLives = 3;

        [Header("Scoring")]
        [Tooltip("Points for a single hit before the combo multiplier.")]
        [Min(0)] public int pointsPerHit = 10;

        [Tooltip("Points lost when a nut drops back down without being hit. Default is half a hit (5 of 10). Score never goes below 0.")]
        [Min(0)] public int missPenalty = 5;

        [Header("Combo")]
        [Tooltip("Every this many consecutive hits adds +1 to the multiplier.")]
        [Min(1)] public int hitsPerMultiplierStep = 5;

        [Tooltip("Highest multiplier a combo can reach.")]
        [Min(1)] public int maxMultiplier = 4;

        [Header("Power-ups (acorns)")]
        [Tooltip("Most lives you can have. Extra-life acorns stop appearing while you're at the max.")]
        [Min(1)] public int maxLives = 5;
        [Tooltip("Points for collecting any acorn (no combo multiplier). Collecting doesn't break your combo.")]
        [Min(0)] public int powerUpPoints = 10;

        [Tooltip("Level where green extra-life acorns start appearing.")]
        [Min(1)] public int extraLifeUnlockLevel = 2;
        [Tooltip("Level where blue slow-mo acorns start appearing.")]
        [Min(1)] public int slowTimeUnlockLevel = 3;
        [Tooltip("Level where purple big-hammer acorns start appearing.")]
        [Min(1)] public int bigHammerUnlockLevel = 4;

        [Tooltip("How much bigger the hammers get (1.6 = 60% bigger).")]
        [Range(1f, 3f)] public float bigHammerScale = 1.6f;
        [Tooltip("How long the big hammer lasts (s). Collecting another adds to the time left.")]
        [Min(0.5f)] public float bigHammerDuration = 8f;
        [Tooltip("Game speed during slow-mo (0.5 = half speed): nuts rise, hover and pop up more slowly.")]
        [Range(0.2f, 1f)] public float slowTimeFactor = 0.5f;
        [Tooltip("How long slow-mo lasts (s, real time). Collecting another adds to the time left.")]
        [Min(0.5f)] public float slowTimeDuration = 6f;

        [Header("Sound")]
        [Tooltip("Volume of the whac-a-mole sound effects (hits, explosions, power-ups, level ups).")]
        [Range(0f, 1f)] public float soundVolume = 0.8f;

        [Header("Levels")]
        [Tooltip("Difficulty per level, in order. You move up after 'Hits To Advance' hits in a level; the last level repeats. " +
                 "Each level sets the board size, nut size, how often nuts pop up and how long they stay out.")]
        public LevelSettings[] levels = LevelSettings.Defaults();

        /// <summary>Settings for a 1-based level number, clamped to the list (falls back to the defaults if the list is empty).</summary>
        public LevelSettings GetLevel(int level)
        {
            LevelSettings[] list = levels != null && levels.Length > 0 ? levels : LevelSettings.Defaults();
            return list[Mathf.Clamp(level - 1, 0, list.Length - 1)];
        }

        public int LevelCount => levels != null && levels.Length > 0 ? levels.Length : LevelSettings.Defaults().Length;

        /// <summary>The level where a power-up starts appearing.</summary>
        public int GetUnlockLevel(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.ExtraLife: return extraLifeUnlockLevel;
                case PowerUpType.BigHammer: return bigHammerUnlockLevel;
                case PowerUpType.SlowTime: return slowTimeUnlockLevel;
                default: return 1;
            }
        }
    }
}
