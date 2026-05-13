using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MammothEatDonut : MonoBehaviour
{
    [SerializeField] private GameObject goldenDonut;
    [SerializeField] private GameObject donutOnMammothTusk;
    private NavMeshMovement mammothAI;


    // Start is called before the first frame update
    void Start()
    {
        goldenDonut.SetActive(true);
        donutOnMammothTusk.SetActive(false);
        mammothAI = GetComponent<NavMeshMovement>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name == "Golden Donut")
        {
            MammothGameOver();      
        } 
        else
        {
            Debug.Log(collision.gameObject.name);
        }
    }

    // Contrary to its name, this script can trigger multiple times
    // depending on whether you start Mammoth Tag over again.
    public void MammothGameOver()
    {
        mammothAI.enraged = false;
        mammothAI.isReturningDonut = true;
        Debug.Log("NOOOOO THE MAMMOTH ATE THE DONUT!!!");
        goldenDonut.SetActive(false);
        donutOnMammothTusk.SetActive(true);
    }
}
