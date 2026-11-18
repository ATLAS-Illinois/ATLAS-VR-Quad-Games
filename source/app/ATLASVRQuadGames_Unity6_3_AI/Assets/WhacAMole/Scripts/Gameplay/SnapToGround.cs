using UnityEngine;

namespace Whacamole.Gameplay
{
    /// <summary>
    /// Drops this object (e.g. the whac-a-mole Arena) straight down onto whatever ground is below it
    /// when the scene starts, so the board stands on the quad's real ground instead of floating or
    /// sinking. Nuts and hammers store their positions relative to the Arena, so moving it is safe.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class SnapToGround : MonoBehaviour
    {
        [Tooltip("How far above this object to start looking for ground (m).")]
        [SerializeField, Min(0f)] private float searchAbove = 20f;
        [Tooltip("How far below this object to look for ground (m).")]
        [SerializeField, Min(0f)] private float searchBelow = 50f;
        [Tooltip("Which layers count as ground.")]
        [SerializeField] private LayerMask groundLayers = ~0;

        // Start (not Awake) so every other collider in the scene is loaded before we look for the ground.
        private void Start()
        {
            Vector3 origin = transform.position + Vector3.up * searchAbove;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, searchAbove + searchBelow,
                                                   groundLayers, QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            Vector3 bestPoint = Vector3.zero;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;          // our own board/stands
                if (hit.collider.GetComponentInParent<CharacterController>() != null) continue; // the player
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestPoint = hit.point;
                }
            }

            if (bestDistance == float.MaxValue)
            {
                Debug.LogWarning($"[SnapToGround] No ground found under {name}; leaving it where it is.", this);
                return;
            }

            transform.position = new Vector3(transform.position.x, bestPoint.y, transform.position.z);
        }
    }
}
