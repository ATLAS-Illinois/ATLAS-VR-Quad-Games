using UnityEngine;

[RequireComponent(typeof(Collider))]
public class NockSocket : MonoBehaviour
{
    //[Tooltip("The main BowController script (drag your Bow object here)")]
    //[SerializeField] private BowController bowController;

    [Tooltip("Updated Multi Shot Bow script (drag your Bow Object here)")]
    [SerializeField] private MultiShotBow msb;

    private void Start()
    {
        if (msb == null)
        {
            Debug.LogError("NockSocket: Bow Controller is not set!", this);
        }
        // Ensure it's a trigger
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object that entered is an arrow
        Arrow arrow = other.GetComponent<Arrow>();

        // Check if it's an arrow AND if the player is holding it
        if (arrow != null && arrow.IsHeldByHand)
        {
            // Tell the bow to nock this arrow
            msb.NockArrow(arrow);
        }
    }

    //private void Start()
    //{
    //    if (bowController == null)
    //    {
    //        Debug.LogError("NockSocket: Bow Controller is not set!", this);
    //    }
    //    // Ensure it's a trigger
    //    GetComponent<Collider>().isTrigger = true;
    //}

    //private void OnTriggerEnter(Collider other)
    //{
    //    // Check if the object that entered is an arrow
    //    Arrow arrow = other.GetComponent<Arrow>();

    //    // Check if it's an arrow AND if the player is holding it
    //    if (arrow != null && arrow.IsHeldByHand)
    //    {
    //        // Tell the bow to nock this arrow
    //        bowController.NockArrow(arrow);
    //    }
    //}
}