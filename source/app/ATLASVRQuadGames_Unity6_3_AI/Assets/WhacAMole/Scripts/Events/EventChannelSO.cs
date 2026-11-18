using System;
using UnityEngine;

namespace Whacamole.Events
{
    /// <summary>
    /// Base class for ScriptableObject event channels.
    /// A channel is an asset that any component can reference: senders call Raise(),
    /// listeners subscribe to OnEventRaised. Senders and listeners never reference each other.
    /// </summary>
    public abstract class EventChannelSO<T> : ScriptableObject
    {
        [TextArea, SerializeField] private string description;
        public string Description => description;

        public event Action<T> OnEventRaised;

        public void Raise(T value)
        {
            OnEventRaised?.Invoke(value);
        }
    }
}
