using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


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

        // The donut gets deactivated while held on game over / reset. Once we unsubscribe
        // we never hear the "released" event, so clear the flag here; otherwise the mammoth
        // touching the player later would count as another game over.
        donutIsGrabbed = false;
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