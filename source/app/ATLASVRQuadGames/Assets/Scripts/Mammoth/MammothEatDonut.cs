using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MammothEatDonut : MonoBehaviour
{
    [SerializeField] private GameObject mammoth;
    private NavMeshMovement mammothAI;


    // Start is called before the first frame update
    void Start()
    {
        gameObject.SetActive(true);
        mammothAI = mammoth.GetComponent<NavMeshMovement>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name == "Mammoth Model")
        {
            mammothAI.enraged = false;
            Debug.Log("NOOOOO THE MAMMOTH ATE THE DONUT!!!");
            gameObject.SetActive(false);
            
            
        }
    }
}
