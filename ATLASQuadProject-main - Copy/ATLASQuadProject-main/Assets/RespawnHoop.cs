using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnHoop : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public GameObject torus_prefab;
    public bool hasCollided = false;
    private void OnCollisionStay(Collision collision) {
        //Another object touches torus
        //collision is the collider that has collided with us
        if (collision.gameObject.name != "Cube" && collision.gameObject.name.Contains("torus") == false) {
            if (hasCollided) {
                Debug.Log("Already collided, do nothing");
                return;
            }

            if (hasCollided == false) {
                Debug.Log("Has not collided yet, respawning torus");
                hasCollided = true;
            }

            Instantiate (torus_prefab, new Vector3 (-33.79f,1.85f,-69.7f), transform.rotation);
        }
        // if (collision.gameObject.name != "Cube" && hasCollided == false) {
        //     Instantiate (torus_prefab, new Vector3 (-33.36f,1.75f,-65.6f), transform.rotation);
        //     Debug.Log("Respawned");
        // }
    }
}
