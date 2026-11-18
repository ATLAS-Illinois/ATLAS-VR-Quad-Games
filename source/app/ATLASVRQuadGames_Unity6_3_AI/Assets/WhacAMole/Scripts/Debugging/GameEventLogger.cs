using UnityEngine;
using Whacamole.Events;

namespace Whacamole.Debugging
{
    /// <summary>
    /// Logs every game event to the Console so you can check the wiring without any gameplay.
    /// Right-click the component header in Play mode to simulate hits and misses.
    /// Safe to disable or delete once real gameplay exists.
    /// </summary>
    public class GameEventLogger : MonoBehaviour
    {
        [SerializeField] private VoidEventChannelSO gameStartedChannel;
        [SerializeField] private GameOverEventChannelSO gameOverChannel;
        [SerializeField] private NutHitEventChannelSO nutHitChannel;
        [SerializeField] private NutMissedEventChannelSO nutMissedChannel;
        [SerializeField] private IntEventChannelSO scoreChangedChannel;
        [SerializeField] private IntEventChannelSO livesChangedChannel;
        [SerializeField] private IntEventChannelSO comboChangedChannel;

        private void OnEnable()
        {
            if (gameStartedChannel != null) gameStartedChannel.OnEventRaised += OnGameStarted;
            if (gameOverChannel != null) gameOverChannel.OnEventRaised += OnGameOver;
            if (nutHitChannel != null) nutHitChannel.OnEventRaised += OnNutHit;
            if (nutMissedChannel != null) nutMissedChannel.OnEventRaised += OnNutMissed;
            if (scoreChangedChannel != null) scoreChangedChannel.OnEventRaised += OnScore;
            if (livesChangedChannel != null) livesChangedChannel.OnEventRaised += OnLives;
            if (comboChangedChannel != null) comboChangedChannel.OnEventRaised += OnCombo;
        }

        private void OnDisable()
        {
            if (gameStartedChannel != null) gameStartedChannel.OnEventRaised -= OnGameStarted;
            if (gameOverChannel != null) gameOverChannel.OnEventRaised -= OnGameOver;
            if (nutHitChannel != null) nutHitChannel.OnEventRaised -= OnNutHit;
            if (nutMissedChannel != null) nutMissedChannel.OnEventRaised -= OnNutMissed;
            if (scoreChangedChannel != null) scoreChangedChannel.OnEventRaised -= OnScore;
            if (livesChangedChannel != null) livesChangedChannel.OnEventRaised -= OnLives;
            if (comboChangedChannel != null) comboChangedChannel.OnEventRaised -= OnCombo;
        }

        private void OnGameStarted() => Debug.Log("[Events] Game started", this);
        private void OnGameOver(GameResult r) =>
            Debug.Log($"[Events] Game over - score {r.Score}, max combo {r.MaxCombo}, hits {r.Hits}, misses {r.Misses}, accuracy {r.Accuracy:P0}, time {r.Duration:F1}s", this);
        private void OnNutHit(NutHitData d) => Debug.Log($"[Events] Nut hit by {d.Hand} hand at {d.HitSpeed:F2} m/s", this);
        private void OnNutMissed(NutMissedData d) => Debug.Log("[Events] Nut missed", this);
        private void OnScore(int v) => Debug.Log($"[Events] Score: {v}", this);
        private void OnLives(int v) => Debug.Log($"[Events] Lives: {v}", this);
        private void OnCombo(int v) => Debug.Log($"[Events] Combo: {v}", this);

        [ContextMenu("Simulate Nut Hit")]
        private void SimulateHit()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Enter Play mode first."); return; }
            if (nutHitChannel != null) nutHitChannel.Raise(new NutHitData(transform.position, Vector3.down * 3f, Hand.Right));
        }

        [ContextMenu("Simulate Nut Missed")]
        private void SimulateMiss()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Enter Play mode first."); return; }
            if (nutMissedChannel != null) nutMissedChannel.Raise(new NutMissedData(transform.position));
        }
    }
}
