using UnityEngine;

namespace Whacamole.Events
{
    /// <summary>Raised by a nut when it hides again without being hit.</summary>
    [CreateAssetMenu(fileName = "NutMissed", menuName = "Whacamole/Events/Nut Missed Event Channel")]
    public class NutMissedEventChannelSO : EventChannelSO<NutMissedData> { }
}
