using UnityEngine;

namespace Whacamole.Events
{
    /// <summary>Event channel carrying an int (score, lives, combo changes).</summary>
    [CreateAssetMenu(fileName = "IntEvent", menuName = "Whacamole/Events/Int Event Channel")]
    public class IntEventChannelSO : EventChannelSO<int> { }
}
