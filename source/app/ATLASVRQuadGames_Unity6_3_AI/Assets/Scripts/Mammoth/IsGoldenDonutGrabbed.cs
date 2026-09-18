using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.VirtualTexturing;


public class IsGoldenDonutGrabbed : MonoBehaviour
{
    [SerializeField] private GrabInteractable _grabInteractable;
    public bool donutIsGrabbed = false;

    // Apparently this is industry standard for working with
    // the Interaction SDK

    private void OnEnable()
    {
        // Subscribe to state changes
        _grabInteractable.WhenStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        _grabInteractable.WhenStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(InteractableStateChangeArgs args)
    {
        if (args.NewState == InteractableState.Select)
        {
            Debug.Log("Object Grabbed!");
            donutIsGrabbed = true;
        }
        else if (args.PreviousState == InteractableState.Select)
        {
            Debug.Log("Object Released!");
            donutIsGrabbed = false;
        }
    }
}