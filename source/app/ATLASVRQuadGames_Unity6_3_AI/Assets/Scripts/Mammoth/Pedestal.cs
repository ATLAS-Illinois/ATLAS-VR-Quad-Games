using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pedestal : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private BewareSignpost signpost;
    [SerializeField] private GameObject mammoth;
    private NavMeshMovement mammothAI;

    void Start()
    {
        mammothAI = mammoth.GetComponent<NavMeshMovement>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Vector3.Distance(player.transform.position, transform.parent.transform.position) < 2f)
        {
            signpost.Anger();
            mammothAI.enraged = true;            
        }
    }
}
