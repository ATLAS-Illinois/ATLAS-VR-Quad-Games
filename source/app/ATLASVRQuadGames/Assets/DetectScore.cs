using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectScore : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider collider) {
        if (collider.gameObject.name == "AlmaPointDetector") {
            Debug.Log("Trigger works");
            Score.currentScore +=1;
            GetComponent<Collider>().enabled = false;
        }
    }
}
