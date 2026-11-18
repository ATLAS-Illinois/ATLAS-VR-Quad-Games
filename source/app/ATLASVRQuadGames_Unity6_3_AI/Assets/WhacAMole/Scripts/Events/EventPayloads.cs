using UnityEngine;

namespace Whacamole.Events
{
    /// <summary>Data sent when a nut is hit.</summary>
    public readonly struct NutHitData
    {
        public readonly Vector3 Position;     // where the nut was hit (world space)
        public readonly Vector3 HitVelocity;  // controller velocity at impact (m/s)
        public readonly Hand Hand;            // which controller hit it

        public NutHitData(Vector3 position, Vector3 hitVelocity, Hand hand)
        {
            Position = position;
            HitVelocity = hitVelocity;
            Hand = hand;
        }

        public float HitSpeed => HitVelocity.magnitude;
    }

    /// <summary>Data sent when a nut escapes without being hit.</summary>
    public readonly struct NutMissedData
    {
        public readonly Vector3 Position;

        public NutMissedData(Vector3 position)
        {
            Position = position;
        }
    }

    /// <summary>Final stats sent with the game over event.</summary>
    public readonly struct GameResult
    {
        public readonly int Score;
        public readonly int MaxCombo;
        public readonly int Hits;
        public readonly int Misses;
        public readonly float Duration; // seconds

        public GameResult(int score, int maxCombo, int hits, int misses, float duration)
        {
            Score = score;
            MaxCombo = maxCombo;
            Hits = hits;
            Misses = misses;
            Duration = duration;
        }

        public float Accuracy => Hits + Misses == 0 ? 0f : (float)Hits / (Hits + Misses);
    }
}
