using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnchorFollow : MonoBehaviour
{
    public Transform camera;
    public Transform rigRoot;
    public Vector3 localOffset = new Vector3(0.25f, -0.45f, 0f);
    // Update is called once per frame
    void LateUpdate()
    {
        if (camera == null || rigRoot == null) return;
        {
        float yaw = camera.eulerAngles.y;
        Quaternion yawRotation = Quaternion.Euler(0, yaw, 0);

        transform.position = rigRoot.position + yawRotation * localOffset;
        transform.rotation = yawRotation;
        }
    }
}
