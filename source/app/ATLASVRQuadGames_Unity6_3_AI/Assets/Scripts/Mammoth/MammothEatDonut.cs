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

    // Can trigger once per round. While the game-over cutscene is playing
    // (mammoth walking the donut back), further calls are ignored; the round
    // re-arms when NavMeshMovement finishes its reset.
    public void MammothGameOver()
    {
        if (mammothAI != null && mammothAI.IsResetting) return;

        Debug.Log("NOOOOO THE MAMMOTH ATE THE DONUT!!!");
        goldenDonut.SetActive(false);
        donutOnMammothTusk.SetActive(true);

        if (mammothAI != null)
        {
            mammothAI.BeginGameOverSequence();
        }
    }
}
