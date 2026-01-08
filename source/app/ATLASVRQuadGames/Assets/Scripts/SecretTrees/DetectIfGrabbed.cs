using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;

public class DetectIfGrabbed : MonoBehaviour
{
    // Detects whether an object is currently being grabbed using the PointerEvent system.
    // Requires the Grabbable script to function properly.

    [SerializeField] private Grabbable _grabbable;

    public bool IsGrabbed { get; private set; }
    public bool IsGrabbedLeft { get; private set; }
    public bool IsGrabbedRight { get; private set; }

    [SerializeField] private GrabInteractor leftHandGrabInteractor;
    [SerializeField] private GrabInteractor rightHandGrabInteractor;

    private int leftId;
    private int rightId;

    // Start is called before the first frame update
    void Start()
    {
        leftId = leftHandGrabInteractor.Identifier;
        rightId = rightHandGrabInteractor.Identifier;

        if (_grabbable != null)
        {
            _grabbable.WhenPointerEventRaised += HandlePointerEvent;
        }
    }

    void OnDestroy()
    {
        if (_grabbable != null)
        {
            _grabbable.WhenPointerEventRaised -= HandlePointerEvent;
        }
    }

    private void HandlePointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Select:
                IsGrabbed = true;
                if (evt.Identifier == leftId)
                    IsGrabbedLeft = true;
                else if (evt.Identifier == rightId)
                    IsGrabbedRight = true;
                //Debug.Log("GRABBED: " + gameObject.name);
                break;
            case PointerEventType.Unselect:
            case PointerEventType.Cancel:
                if (evt.Identifier == leftId)
                    IsGrabbedLeft = false;
                else if (evt.Identifier == rightId)
                    IsGrabbedRight = false;
                if (!IsGrabbedLeft && !IsGrabbedRight) // Failsafe, really
                    IsGrabbed = false;

                //Debug.Log("RELEASED: " + gameObject.name);
                break;
        }
    }
}
