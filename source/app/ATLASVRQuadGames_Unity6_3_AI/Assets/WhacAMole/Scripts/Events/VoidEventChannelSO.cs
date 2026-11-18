using System;
using UnityEngine;

namespace Whacamole.Events
{
    /// <summary>Event channel with no payload (e.g. "game started").</summary>
    [CreateAssetMenu(fileName = "VoidEvent", menuName = "Whacamole/Events/Void Event Channel")]
    public class VoidEventChannelSO : ScriptableObject
    {
        [TextArea, SerializeField] private string description;
        public string Description => description;

        public event Action OnEventRaised;

        public void Raise()
        {
            OnEventRaised?.Invoke();
        }
    }
}
