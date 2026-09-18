using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnableCollision : MonoBehaviour
{
    private Collider c;

    private void Awake()
    {
        c = gameObject.GetComponent<MeshCollider>();
        c.enabled = true;
    }

    public void DisableCollision()
    {
        c.enabled = false;
    }

    public void TurnOnCollision()
    {
        c.enabled = true;
    }
}
