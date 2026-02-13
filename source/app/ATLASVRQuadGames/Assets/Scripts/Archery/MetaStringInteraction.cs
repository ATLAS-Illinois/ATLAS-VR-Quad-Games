using UnityEngine;
using Oculus.Interaction;
using System;

[RequireComponent(typeof(LineRenderer), typeof(Grabbable))]
public class MetaStringInteraction : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The stationary point on the Top Limb")]
    [SerializeField] private Transform topLimbPos;

    [Tooltip("The stationary point on the Bottom Limb")]
    [SerializeField] private Transform bottomLimbPos;

    [Tooltip("OPTIONAL: Drag an object here that points the Z-Axis in the correct fire direction. If empty, it uses the Parent.")]
    [SerializeField] private Transform directionReference;

    [Header("Settings")]
    [SerializeField] private float maxPullDistance = 0.5f;

    // --- EVENTS ---
    public event Action<float> PullAmountChanged;
    public event Action<float> OnStringReleased;

    private LineRenderer lineRenderer;
    private Grabbable grabbable;
    private float currentPullValue = 0f;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        grabbable = GetComponent<Grabbable>();

        lineRenderer.positionCount = 3;
        lineRenderer.useWorldSpace = true;
    }

    private void OnEnable()
    {
        if (grabbable != null) grabbable.WhenPointerEventRaised += HandlePointerEvent;
    }

    private void OnDisable()
    {
        if (grabbable != null) grabbable.WhenPointerEventRaised -= HandlePointerEvent;
    }

    private void HandlePointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Unselect)
        {
            OnStringReleased?.Invoke(currentPullValue);
        }
    }

    private void LateUpdate()
    {
        if (topLimbPos == null || bottomLimbPos == null) return;

        // 1. Calculate the World Rest Position (Center of Limbs)
        Vector3 worldRestPos = (topLimbPos.position + bottomLimbPos.position) / 2f;
        Vector3 finalStringPos = worldRestPos; // Default position is the rest position

        // Check if the string is actively being pulled by a hand
        if (grabbable.GrabPoints.Count > 0)
        {
            // --- A. String is actively grabbed, calculate the constrained position ---

            Transform referenceFrame = directionReference != null ? directionReference : transform.parent;
            if (referenceFrame == null) return;

            // Convert Hand position to the Reference Frame's local space
            Vector3 localHandPos = referenceFrame.InverseTransformPoint(transform.position);
            Vector3 localRestPos = referenceFrame.InverseTransformPoint(worldRestPos);

            // Constrain movement to the Z-Axis rail
            Vector3 constrainedLocalPos = new Vector3(localRestPos.x, localRestPos.y, localHandPos.z);

            // Clamp: Stop string from being pushed through the bow handle
            if (constrainedLocalPos.z > localRestPos.z)
            {
                constrainedLocalPos.z = localRestPos.z;
            }

            // Set final position to the constrained position
            finalStringPos = referenceFrame.TransformPoint(constrainedLocalPos);

            // Calculate Pull Amount 
            float distance = Vector3.Distance(worldRestPos, finalStringPos);
            currentPullValue = Mathf.Clamp01(distance / maxPullDistance);
            PullAmountChanged?.Invoke(currentPullValue);
        }
        else
        {
            // --- B. String is released, set current position and pull amount to 0 ---
            // finalStringPos remains at worldRestPos (set above)
            currentPullValue = 0f;
            PullAmountChanged?.Invoke(0f);
        }

        // --- 2. FORCE THE OBJECT TO ITS FINAL POSITION (Rest or Constrained) ---
        transform.position = finalStringPos;

        // --- 3. Update Visuals ---
        lineRenderer.SetPosition(0, topLimbPos.position);
        lineRenderer.SetPosition(1, finalStringPos);
        lineRenderer.SetPosition(2, bottomLimbPos.position);
    }
}