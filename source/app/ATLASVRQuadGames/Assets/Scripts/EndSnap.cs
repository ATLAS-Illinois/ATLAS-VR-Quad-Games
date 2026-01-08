using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EndSnap : MonoBehaviour
{
    [Header("Snapping Settings")]
    [Tooltip("How close to an EndSnapPoint before snapping.")]
    public float snapDistance = 0.02f;

    [Tooltip("Optional override for this block's letter key (e.g. \"i\", \"l\"). " +
             "If empty, it will be parsed from the GameObject name like SnapBlock.")]
    public string letterKeyOverride = "";

    [Header("Completion / Fireworks")]
    [Tooltip("Total number of blocks that must be snapped to trigger the final event.")]
    public int totalBlocksRequired = 6;

    [Tooltip("Invoked once when all blocks are snapped.")]
    public UnityEvent onAllBlocksSnapped;

    [Tooltip("Reference to the 'SF_Rainbow' object that is hidden.")]
    public GameObject rainbowEffectObject;

    private bool hasSnappedToWall = false;

    // Global count of how many blocks have finished snapping
    private static int globalSnappedCount = 0;

    private static HashSet<Transform> occupiedSlots = new HashSet<Transform>();


    // ---------------------------
    // TRIGGER HANDLING
    // ---------------------------
    private void OnTriggerStay(Collider other)
    {
        if (hasSnappedToWall) return;
        if (!other.CompareTag("EndSnapPoint")) return;

        Transform targetSnap = other.transform;

        if (occupiedSlots.Contains(targetSnap)) return;  // slot already used

        // Use the root rigidbody of this block
        Rigidbody myRb = GetComponent<Rigidbody>();
        if (myRb == null)
        {
            myRb = GetComponentInParent<Rigidbody>();
            if (myRb == null)
            {
                Debug.LogWarning($"[EndSnap] {gameObject.name} has no Rigidbody to snap.");
                return;
            }
        }

        float distance = Vector3.Distance(myRb.transform.position, targetSnap.position);
        if (distance > snapDistance) return;

        // ---------------------------
        // FULL ASSEMBLY CHECK
        // ---------------------------
        if (!SnapBlock.groupPieceCounts.TryGetValue(myRb, out int pieceCount) || pieceCount < 2)
        {
            // This block is not fully assembled (middle only or missing pieces)
            Debug.Log($"[EndSnap] {myRb.name} rejected — only {pieceCount}/2 pieces attached.");
            return;
        }

        // ---------------------------
        // LETTER COMPATIBILITY CHECK
        // ---------------------------
        string myLetter = !string.IsNullOrEmpty(letterKeyOverride)
            ? letterKeyOverride.ToLower()
            : ExtractLetterKey(myRb.gameObject.name);

        string targetLetter = ExtractLetterKey(targetSnap.gameObject.name);

        // If the target doesn't encode a letter, we allow any.
        // If it does, then letters must match.
        if (!string.IsNullOrEmpty(targetLetter) &&
            !string.IsNullOrEmpty(myLetter) &&
            myLetter != targetLetter)
        {
            Debug.Log($"[EndSnap] {myRb.name} (letter {myLetter}) cannot snap to {targetSnap.name} (letter {targetLetter}).");
            return;
        }

        // ---------------------------
        // FINAL SNAP: LOCK TO WALL
        // ---------------------------
        DoFinalSnap(myRb, targetSnap);
    }

    // ---------------------------
    // FINAL SNAP LOGIC
    // ---------------------------
    private void DoFinalSnap(Rigidbody rb, Transform targetSnap)
    {
        if (hasSnappedToWall) return;
        hasSnappedToWall = true;

        Debug.Log($"[EndSnap] Snapping {rb.name} to end marker {targetSnap.name}");

        occupiedSlots.Add(targetSnap);

        // Optional: disable the snap collider after use
        Collider snapCol = targetSnap.GetComponent<Collider>();
        if (snapCol != null) snapCol.enabled = false;

        Collider[] allCols = rb.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in allCols)
        {
            col.enabled = false;
        }

        // 1. Disable grabbing scripts so the player can't pull it off
        DisableGrabInteractions(rb.gameObject);

        // 2. Align the entire block to the target snap
        rb.transform.position = targetSnap.position;
        rb.transform.rotation = targetSnap.rotation;

        // 3. Optionally parent it under the wall / marker parent
        // (keeps hierarchy clean and moves with the wall if needed)
        if (targetSnap.parent != null)
        {
            rb.transform.SetParent(targetSnap.parent, true);
        }

        // 4. Freeze physics so it stays perfectly in place
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        // If there are any child rigidbodies left for some reason, freeze them too
        Rigidbody[] childBodies = rb.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody childRb in childBodies)
        {
            if (childRb == rb) continue;

            childRb.velocity = Vector3.zero;
            childRb.angularVelocity = Vector3.zero;
            childRb.useGravity = false;
            childRb.isKinematic = true;
            childRb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }

        // 5. Make colliders triggers so nothing bumps it anymore (optional)
        Collider[] colliders = rb.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.isTrigger = true;
        }

        Physics.SyncTransforms();

        // 6. Update global snapped count & possibly trigger fireworks
        globalSnappedCount++;
        Debug.Log($"[EndSnap] Global snapped count = {globalSnappedCount}/{totalBlocksRequired}");

        if (globalSnappedCount >= totalBlocksRequired)
        {
            Debug.Log("[EndSnap] All blocks snapped! Triggering final event.");

            // Use the direct reference instead of GameObject.Find()
            if (rainbowEffectObject != null)
            {
                rainbowEffectObject.SetActive(true); // This will now work!
            }
            else
            {
                Debug.LogWarning($"[EndSnap] {gameObject.name} is missing the 'Rainbow Effect Object' reference in the Inspector!");
            }

            // This event will now work because the object was just activated
            onAllBlocksSnapped?.Invoke();
        }
    }

    // ---------------------------
    // LETTER KEY PARSING (LIKE SnapBlock)
    // ---------------------------
    private string ExtractLetterKey(string name)
    {
        // Handles: "logo-i-middle[1]" and "end-logo-i-middle[1]"
        string lower = name.ToLower();
        string[] parts = lower.Split('-');

        // "logo-i-middle"
        if (parts.Length >= 3 && parts[0] == "logo")
        {
            return parts[1];  // "i"
        }

        // "end-logo-i-middle"
        if (parts.Length >= 4 && parts[0] == "end" && parts[1] == "logo")
        {
            return parts[2];  // "i"
        }

        // --- NEW, MORE ROBUST RULE ---
        // Handles simpler names like "EndSnapPoint-i" or "Target-l"
        // It looks for a single letter as the *last* part of the name.
        if (parts.Length > 1 && parts[parts.Length - 1].Length == 1)
        {
            // Grabs the last part if it's a single character
            return parts[parts.Length - 1];
        }
        // --- END NEW RULE ---

        return ""; // Return empty if no known pattern matches
    }


    // ---------------------------
    // DISABLE XR GRABBING ON THIS BLOCK
    // ---------------------------
    private void DisableGrabInteractions(GameObject root)
    {
        var grabScripts = root.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var script in grabScripts)
        {
            string n = script.GetType().Name.ToLower();
            if (n.Contains("handgrabinteraction") ||
                n.Contains("grabinteractable") ||
                n.Contains("grabbable"))
            {
                if (script is Behaviour b)
                {
                    b.enabled = false;
                    Debug.Log($"[EndSnap] Disabled grab interaction on {root.name} ({script.GetType().Name})");
                }
            }
        }
    }
}
