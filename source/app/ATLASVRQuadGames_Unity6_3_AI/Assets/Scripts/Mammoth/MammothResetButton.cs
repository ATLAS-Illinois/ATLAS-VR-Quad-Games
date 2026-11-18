using UnityEngine;

/// <summary>
/// Resets Mammoth Tag (mammoth home, golden donut back on the pedestal, sign calmed down).
///
/// Ways to press it:
///  - Point either controller at it and pull that controller's index trigger.
///  - Touch it with a controller (tag "GameController", same as the archery reset button) and pull the trigger.
///  - In the Unity Editor only: press the R key (handy for testing without a headset).
///
/// Setup: put this on any object with a Collider (e.g. a Cube near the donut signpost).
/// The mammoth and the controller pointers are found automatically if left empty.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MammothResetButton : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    [Tooltip("The NavMeshMovement on the mammoth.")]
    [SerializeField] private NavMeshMovement mammoth;

    [Tooltip("Left controller transform to point with. Defaults to OVRCameraRig.leftControllerAnchor.")]
    [SerializeField] private Transform leftPointer;

    [Tooltip("Right controller transform to point with. Defaults to OVRCameraRig.rightControllerAnchor.")]
    [SerializeField] private Transform rightPointer;

    [Header("Pointing")]
    [Tooltip("How far away you can point at the button from.")]
    [SerializeField] private float maxPointDistance = 15f;

    [Tooltip("Draw a laser from the controller to the button while pointing at it.")]
    [SerializeField] private bool showPointerLine = true;

    [SerializeField] private float pointerLineWidth = 0.01f;

    [Header("Look")]
    [SerializeField] private Color idleColor = new Color(0.85f, 0.15f, 0.15f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.85f, 0.2f);

    [Header("Testing")]
    [Tooltip("Editor only: press this key to reset without a headset.")]
    [SerializeField] private KeyCode editorResetKey = KeyCode.R;

    [Tooltip("Seconds before the button can be pressed again.")]
    [SerializeField] private float pressCooldown = 0.5f;

    private Collider buttonCollider;
    private Renderer buttonRenderer;
    private MaterialPropertyBlock propertyBlock;
    private LineRenderer pointerLine;
    private bool controllerInside;
    private float lastPressTime = -999f;
    private bool? lastHovered;

    private void Awake()
    {
        buttonCollider = GetComponent<Collider>();
        // Trigger so the player can't bump into it, and so controllers can "touch" it.
        buttonCollider.isTrigger = true;

        buttonRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();

        if (showPointerLine)
        {
            GameObject lineObject = new GameObject("Reset Pointer Line");
            lineObject.transform.SetParent(transform, false);
            pointerLine = lineObject.AddComponent<LineRenderer>();
            pointerLine.positionCount = 2;
            pointerLine.useWorldSpace = true;
            pointerLine.startWidth = pointerLineWidth;
            pointerLine.endWidth = pointerLineWidth * 0.5f;
            pointerLine.material = new Material(Shader.Find("Sprites/Default"));
            pointerLine.startColor = hoverColor;
            pointerLine.endColor = hoverColor;
            pointerLine.enabled = false;
        }
    }

    private void Start()
    {
        if (mammoth == null)
        {
            mammoth = FindFirstObjectByType<NavMeshMovement>();
            if (mammoth == null)
            {
                Debug.LogError("MammothResetButton: no NavMeshMovement (mammoth) found in the scene.", this);
            }
        }

        if (leftPointer == null || rightPointer == null)
        {
            OVRCameraRig rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null)
            {
                if (leftPointer == null) leftPointer = rig.leftControllerAnchor;
                if (rightPointer == null) rightPointer = rig.rightControllerAnchor;
            }
            else
            {
                Debug.LogWarning("MammothResetButton: no OVRCameraRig found; pointing won't work (touch + editor key still will).", this);
            }
        }

        SetHovered(false);
    }

    private void Update()
    {
        bool rightPointing = IsPointingAtButton(rightPointer, out Vector3 rightHit);
        bool leftPointing = IsPointingAtButton(leftPointer, out Vector3 leftHit);

        SetHovered(rightPointing || leftPointing || controllerInside);
        UpdatePointerLine(rightPointing ? rightPointer : leftPointing ? leftPointer : null,
                          rightPointing ? rightHit : leftPointing ? leftHit : Vector3.zero);

        bool pressed =
            ((rightPointing || controllerInside) && OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger)) ||
            ((leftPointing || controllerInside) && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger));

#if UNITY_EDITOR
        if (Input.GetKeyDown(editorResetKey)) pressed = true;
#endif

        if (pressed) Press();
    }

    /// <summary>Can also be hooked up to any UnityEvent (e.g. an Interaction SDK poke button).</summary>
    public void Press()
    {
        if (mammoth == null || Time.time - lastPressTime < pressCooldown) return;
        lastPressTime = Time.time;

        Debug.Log("MammothResetButton: resetting Mammoth Tag.");
        mammoth.ResetGameInstantly();
    }

    private bool IsPointingAtButton(Transform pointer, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        if (pointer == null || !pointer.gameObject.activeInHierarchy) return false;

        // Only tests this button's collider, so the player's own hand/body colliders can't block it.
        Ray ray = new Ray(pointer.position, pointer.forward);
        if (buttonCollider.Raycast(ray, out RaycastHit hit, maxPointDistance))
        {
            hitPoint = hit.point;
            return true;
        }
        return false;
    }

    private void UpdatePointerLine(Transform pointer, Vector3 hitPoint)
    {
        if (pointerLine == null) return;

        pointerLine.enabled = pointer != null;
        if (pointer != null)
        {
            pointerLine.SetPosition(0, pointer.position);
            pointerLine.SetPosition(1, hitPoint);
        }
    }

    private void SetHovered(bool hovered)
    {
        if (buttonRenderer == null || lastHovered == hovered) return;
        lastHovered = hovered;

        buttonRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_Color", hovered ? hoverColor : idleColor);
        buttonRenderer.SetPropertyBlock(propertyBlock);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("GameController")) controllerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("GameController")) controllerInside = false;
    }
}
