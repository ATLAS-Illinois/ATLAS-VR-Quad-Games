using UnityEngine;

namespace Whacamole.Events
{
    /// <summary>Raised by the GameManager when the run ends, with the final stats.</summary>
    [CreateAssetMenu(fileName = "GameOver", menuName = "Whacamole/Events/Game Over Event Channel")]
    public class GameOverEventChannelSO : EventChannelSO<GameResult> { }
}
