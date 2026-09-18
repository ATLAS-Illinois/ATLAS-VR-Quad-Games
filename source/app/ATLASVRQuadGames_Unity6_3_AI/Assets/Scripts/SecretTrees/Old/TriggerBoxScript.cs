using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start() { }

    // Update is called once per frame
    void Update() { }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (gameObject.CompareTag("EasterEggTrigger1"))
            {
                Debug.Log("Player has entered the first tree.");
                Narration nscript = GameObject.FindWithTag("EasterEggNarration1").GetComponent<Narration>();
                nscript.EnableText();

                EnableCollision ec = GameObject.FindWithTag("EasterEggTree1").GetComponent<EnableCollision>();
                ec.DisableCollision();
            }
            else if (gameObject.CompareTag("EasterEggTrigger2"))
            {
                Debug.Log("Player has entered the second tree.");
                Narration nscript = GameObject.FindWithTag("EasterEggNarration1").GetComponent<Narration>();
                nscript.EnableText();

                EnableCollision ec = GameObject.FindWithTag("EasterEggBuilding").GetComponent<EnableCollision>();
                ec.DisableCollision();
            }    
        }
            
    }
}
