using UnityEngine;

namespace Whacamole.Events
{
    /// <summary>Raised by a nut when a controller hits it hard enough.</summary>
    [CreateAssetMenu(fileName = "NutHit", menuName = "Whacamole/Events/Nut Hit Event Channel")]
    public class NutHitEventChannelSO : EventChannelSO<NutHitData> { }
}
