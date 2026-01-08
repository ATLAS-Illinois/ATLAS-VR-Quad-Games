using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class almascore : MonoBehaviour
{
    public bool scoredstate = false;
    void OnCollisionEnter(Collision col) {
        if (col.gameObject.name == "TorusDisc") {
            if (scoredstate == false) {
                scoredstate = true;
                Score.currentScore +=1;
            }
        Debug.Log("Collided. Score is " + Score.currentScore);
        col.rigidbody.velocity = new Vector3(0, 0, 0);
        col.transform.position = new Vector3(-33, 1, -63);
        scoredstate = false;

        }
    }

    // void ResetHoop(Collision col) {

    //     col.transform.position = new Vector3(-33, 1, -63);
    //     scoredstate = false;
    // }
}
