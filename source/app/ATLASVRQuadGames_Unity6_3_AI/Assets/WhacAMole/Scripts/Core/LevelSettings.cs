using System;
using UnityEngine;

namespace Whacamole.Core
{
    /// <summary>
    /// Difficulty settings for one level. The list of levels lives on the GameSettings asset;
    /// edit it there to rebalance. The last level repeats forever once reached.
    /// </summary>
    [Serializable]
    public class LevelSettings
    {
        [Tooltip("Hits needed during this level to move on to the next one. Ignored on the last level.")]
        [Min(1)] public int hitsToAdvance = 8;

        [Header("Board")]
        [Tooltip("Holes across (left to right). Up to 5; the hammer stands sit in front of the table's corners, out of the way.")]
        [Range(1, 5)] public int columns = 3;
        [Tooltip("Rows of holes (front to back, growing away from the player). Up to 4 so the back row stays within reach.")]
        [Range(1, 4)] public int rows = 3;
        [Tooltip("Nut size compared to the original (1 = full size). Smaller is harder to hit.")]
        [Range(0.4f, 1.2f)] public float nutScale = 1f;

        [Header("Pop-ups")]
        [Tooltip("Shortest / longest wait between pop-up waves (s).")]
        [Min(0.1f)] public float minWaveInterval = 2f;
        [Min(0.1f)] public float maxWaveInterval = 3f;
        [Tooltip("Fewest / most nuts that pop up together in one wave.")]
        [Min(1)] public int minNutsPerWave = 1;
        [Min(1)] public int maxNutsPerWave = 1;
        [Tooltip("Shortest / longest time (s) a nut stays out before it drops back and counts as a miss.")]
        [Min(0.1f)] public float minNutUpTime = 2f;
        [Min(0.1f)] public float maxNutUpTime = 2.5f;

        [Header("Bombs")]
        [Tooltip("Chance (0-1) that a pop-up is a bomb instead of a nut. Hitting a bomb costs a life; letting it sink back is free. " +
                 "At most one bomb per wave.")]
        [Range(0f, 1f)] public float bombChance = 0.1f;

        [Header("Power-ups")]
        [Tooltip("Chance (0-1) that a pop-up is a power-up acorn. Which acorns can appear depends on the unlock levels " +
                 "on GameSettings. At most one acorn per wave; missing one costs nothing.")]
        [Range(0f, 1f)] public float powerUpChance = 0f;

        public LevelSettings() { }

        public LevelSettings(int hitsToAdvance, int columns, int rows, float nutScale,
                             float minWave, float maxWave, int minNuts, int maxNuts, float minUp, float maxUp,
                             float bombChance = 0f, float powerUpChance = 0f)
        {
            this.bombChance = bombChance;
            this.powerUpChance = powerUpChance;
            this.hitsToAdvance = hitsToAdvance;
            this.columns = columns;
            this.rows = rows;
            this.nutScale = nutScale;
            minWaveInterval = minWave;
            maxWaveInterval = maxWave;
            minNutsPerWave = minNuts;
            maxNutsPerWave = maxNuts;
            minNutUpTime = minUp;
            maxNutUpTime = maxUp;
        }

        /// <summary>
        /// Default progression: starts slower than the old fixed tuning (1-2 s up, waves every 1-2 s)
        /// and ends well past it.
        /// </summary>
        public static LevelSettings[] Defaults() => new[]
        {
            //                 hits cols rows scale  wave(s)      nuts   up time(s)  bomb  acorn
            new LevelSettings(  8,   3,   3,  1.00f, 2.0f, 3.0f,  1, 1,  2.0f, 2.5f, 0.10f, 0.00f), // 1: warm-up
            new LevelSettings( 10,   3,   3,  0.95f, 1.6f, 2.4f,  1, 2,  1.6f, 2.2f, 0.15f, 0.08f), // 2: extra-life acorns
            new LevelSettings( 12,   4,   3,  0.90f, 1.3f, 2.0f,  1, 2,  1.4f, 1.9f, 0.20f, 0.10f), // 3: + slow-mo acorns
            new LevelSettings( 14,   4,   4,  0.85f, 1.1f, 1.7f,  1, 2,  1.2f, 1.6f, 0.25f, 0.12f), // 4: + big-hammer acorns
            new LevelSettings( 16,   5,   4,  0.80f, 0.9f, 1.4f,  2, 2,  1.0f, 1.4f, 0.30f, 0.14f), // 5
            new LevelSettings(  1,   5,   4,  0.70f, 0.7f, 1.2f,  2, 3,  0.8f, 1.2f, 0.35f, 0.15f), // 6: final, repeats
        };
    }
}
