using System.Collections;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.AI;

public class NavMeshMovement : MonoBehaviour
{
    // Global variables used throughout the script. May be changed from the Unity Editor.
    public float wanderWaitTime = 2.0f; // in seconds
    public float wanderTriggerChance = 0.3f; // has to be between 0 and 1
    public float avoidDistance = 4.0f;
    public float cooldownTime = 30f;
    public float agentSpeed = 6.0f;
    public float agentAcceleration = 8.0f;
    public float baitDetectionDistance = 6.0f;
    public float baitStopDistance = 6.0f;
    public float baitCooldownTime = 1f; // in seconds

    private float baseSpeed; // used to remember the speed of the NavMesh agent
    private float baseAcceleration;

    [SerializeField]
    private Transform player;

    [SerializeField]
    private GameObject goldenDonutIdol;

    [SerializeField]
    private GameObject tuskCosmeticGoldenDonut;

    [SerializeField]
    private Transform goldenPedestal;

    [SerializeField]
    private BewareSignpost signpost;

    [SerializeField]
    private Transform mammothStand;

    //[SerializeField]
    //private Transform bait;

    private Animator animator;
    private NavMeshAgent agent;
    private Coroutine currentCoroutine;

    //private bool hasReachedBait = false; // indicates if the object has reached the bait
    //private bool immobileFromBait = false; // indicates if mammoth is slow to react after reaching bait

    //// Variables used for the GameObject's avoiding behavior.
    //private bool isAvoiding = false;
    //private bool recentlyAvoided = false;
    //private int increment = 1;

    // because the player approached the mammoth's golden donut. Who wouldn't be angry?
    public bool enraged = false;
    public bool previouslyEnraged = false;
    public bool isReturningDonut = false;
    public bool isReturningToStart = false;
    private int noFrames = 0;

    /// <summary>
    /// Start is called before the first frame update.
    /// Initiates the GameObject's properties.
    /// </summary>

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        if (animator != null)
        {
            animator.SetBool("Moving", false);
        }

        if (agent == null)
        {
            Debug.LogError("NavMeshAgent component not found!");
            return;
        }

        baseSpeed = agentSpeed;
        baseAcceleration = agentAcceleration;   
        agent.speed = agentSpeed;
        agent.acceleration = agentAcceleration;
        agent.stoppingDistance = baitStopDistance;
        enraged = false;
        previouslyEnraged = false;

        //currentCoroutine = StartCoroutine(Wander());
        //currentCoroutine = StartCoroutine(RunTowardsDonut());
    }

    IEnumerator RunTowardsDestination(Transform destination)
    {
        Vector3 travelDirection = (transform.position - destination.position).normalized * -1;

        if (enraged) agent.speed = baseSpeed;

        Vector3 target = transform.position + travelDirection * (avoidDistance);

        NavMeshHit navHit;
        NavMesh.SamplePosition(target, out navHit, avoidDistance, NavMesh.AllAreas);

        if (animator != null)
        {
            animator.SetBool("Moving", true);
        }

        Debug.Log("Mammoth: Moving to position " + navHit.position + " to track down " + destination.name);
        agent.SetDestination(navHit.position);

        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
        {
            yield return null;
        }

        if (animator != null)
        {
            animator.SetBool("Moving", false);
        }
    }




    /// <summary>
    /// Update is called once per frame. It is used to check the distance between the player, 
    /// bait, and the GameObject.
    /// </summary>
    /// 
    void Update()
    {
        if (enraged)
        {
            if (currentCoroutine == null)
            {
                noFrames = 0;
                currentCoroutine = StartCoroutine(RunTowardsDestination(goldenDonutIdol.transform));
            }
            else if (noFrames == 120)
            {
                noFrames = 0;
                StopCoroutine(currentCoroutine);
                currentCoroutine = StartCoroutine(RunTowardsDestination(goldenDonutIdol.transform));
            }
            else
            {
                noFrames++;
            }
        }
        else
        {
            if (currentCoroutine != null)
            {
                StopCoroutine(currentCoroutine);
            }

            animator.SetBool("Moving", false);

            if (!previouslyEnraged && Vector3.Distance(player.transform.position, goldenDonutIdol.transform.position) < 2f)
            {
                signpost.Anger();
                enraged = true;
                previouslyEnraged = true;
            }
            else
            {
                Vector3 directionTowardsPlayer = (transform.position - player.position).normalized * -1;
                agent.SetDestination(transform.position + 0.001f * directionTowardsPlayer);
            }
        }

        //else if (isReturningDonut)
        //{
        //    currentCoroutine = StartCoroutine(RunTowardsDestination(goldenPedestal));
        //    float distanceToTarget = Vector3.Distance(transform.position, goldenPedestal.position);
        //    if (distanceToTarget > 5f)
        //    {
        //        agent.speed = baseSpeed;
        //        agent.acceleration = baseAcceleration;
        //    }
        //    else
        //    {
        //        agent.speed = baseSpeed * (distanceToTarget / 5f);
        //        agent.acceleration = baseAcceleration * 200;
        //    }

        //    if (Vector3.Distance(transform.position, goldenPedestal.position) < 2f)
        //    {
        //        isReturningDonut = false;
        //        isReturningToStart = true;
        //        tuskCosmeticGoldenDonut.SetActive(false);
        //        goldenDonutIdol.transform.localPosition = new Vector3(-0.2f, 0f, 0f);
        //        goldenDonutIdol.SetActive(true);
        //        StopCoroutine(currentCoroutine);
        //    }
        //}
        //else if (isReturningToStart)
        //{
        //    currentCoroutine = StartCoroutine(RunTowardsDestination(mammothStand));
        //    float distanceToTarget = Vector3.Distance(transform.position, mammothStand.position);
        //    if (distanceToTarget > 5f)
        //    {
        //        agent.speed = baseSpeed;
        //        agent.acceleration = baseAcceleration;
        //    }
        //    else
        //    {
        //        agent.speed = baseSpeed * (distanceToTarget / 5f);
        //        agent.acceleration = baseAcceleration * 200;
        //    }

        //    if (Vector3.Distance(transform.position, mammothStand.position) < 0.1f)
        //    {
        //        isReturningToStart = false;
        //    }
        //}




        //if (immobileFromBait)
        //{
        //    return;
        //}
        //if (hasReachedBait)
        //{
        //    hasReachedBait = false;
        //    if (currentCoroutine != null)
        //    {
        //        StopCoroutine(currentCoroutine);
        //    }
        //    currentCoroutine = StartCoroutine(BaitCooldown());
        //    return;
        //}

        //float distanceFromPlayer = Vector3.Distance(transform.position, player.position);
        //float distanceFromBait = Vector3.Distance(transform.position, bait.position);
        //float playerToBaitDistance = Vector3.Distance(player.position, bait.position);

        ////// Checks if:
        ////// 1. object is within a certain distance of the bait
        ////// 2. object is away from the player
        ////// 3. player is away from the bait
        //bool baitDistanceConditionals = distanceFromBait <= baitDetectionDistance &&
        //                                distanceFromPlayer > avoidDistance &&
        //                                playerToBaitDistance > avoidDistance;

        //if (baitDistanceConditionals)
        //{
        //    if (currentCoroutine != null)
        //    {
        //        StopCoroutine(currentCoroutine);
        //    }
        //    currentCoroutine = StartCoroutine(MoveToBait());
        //}
        //else if (distanceFromPlayer < avoidDistance && !isAvoiding)
        //{
        //    if (currentCoroutine != null)
        //    {
        //        StopCoroutine(currentCoroutine);
        //    }
        //    currentCoroutine = StartCoroutine(Avoid());
        //}
    }
}

//IEnumerator RunTowardsDonut()
//{
//    Vector3 directionTowardsDonut = (transform.position - goldenDonutIdol.position).normalized * -1;
//    agent.speed = baseSpeed;

//    Vector3 avoidTarget = transform.position + directionTowardsDonut * (avoidDistance);

//    NavMeshHit navHit;
//    NavMesh.SamplePosition(avoidTarget, out navHit, avoidDistance, NavMesh.AllAreas);

//    if (animator != null)
//    {
//        animator.SetBool("Moving", true);
//    }

//    Debug.Log("Chasing donut until position: " + navHit.position);
//    agent.SetDestination(navHit.position);

//    while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
//    {
//        yield return null;
//    }

//    if (animator != null)
//    {
//        animator.SetBool("Moving", false);
//    }
//}


///// <summary>
///// Wandering movement of the asset. Follows a randomly generated
///// path.
///// </summary>
//IEnumerator Wander()
//{
//    while (true)
//    {
//        yield return new WaitForSeconds(wanderWaitTime);
//        if (Random.value < wanderTriggerChance)
//        {
//            if (animator != null)
//            {
//                animator.SetBool("Moving", true);
//            }

//            Vector3 wanderTarget = GetRandomNavMeshLocation();
//            Debug.Log("Wandering to: " + wanderTarget);
//            agent.SetDestination(wanderTarget);

//            while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
//            {
//                yield return null;
//            }

//            if (animator != null)
//            {
//                animator.SetBool("Moving", false);
//            }
//        }
//    }
//}

///// <summary>
///// Generates a random position on the NavMesh. The NavMesh agent,
///// the GameObject, will move in the direction generated by this method.
///// </summary>
///// <returns> A vector pointing towards the random direction </returns>
//Vector3 GetRandomNavMeshLocation()
//{
//    Vector3 randomDirection = Random.insideUnitSphere * avoidDistance;
//    randomDirection += transform.position;
//    NavMeshHit navHit;
//    NavMesh.SamplePosition(randomDirection, out navHit, avoidDistance, NavMesh.AllAreas);
//    return navHit.position;
//}

///// <summary>
///// Avoiding behavior of the GameObject. It moves away from the player.
///// TODO: Implement faster animation for fast speeds.
///// </summary>
//IEnumerator Avoid()
//{
//    isAvoiding = true;
//    Vector3 directionAwayFromPlayer = (transform.position - player.position).normalized;

//    if (recentlyAvoided && increment < 2)
//    {
//        increment += 1;
//    }

//    float modifier = Mathf.Pow(1.3f, increment);
//    agent.speed = baseSpeed * modifier;

//    Vector3 avoidTarget = transform.position + directionAwayFromPlayer * (modifier * avoidDistance);

//    NavMeshHit navHit;
//    NavMesh.SamplePosition(avoidTarget, out navHit, avoidDistance, NavMesh.AllAreas);

//    if (animator != null)
//    {
//        animator.SetBool("Moving", true);
//    }

//    Debug.Log("Avoiding to: " + navHit.position);
//    agent.SetDestination(navHit.position);

//    while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
//    {
//        yield return null;
//    }

//    if (animator != null)
//    {
//        animator.SetBool("Moving", false);
//    }

//    if (!recentlyAvoided)
//    {
//        recentlyAvoided = true;
//    }

//    isAvoiding = false;

//    currentCoroutine = StartCoroutine(Wander());

//    StartCoroutine(AvoidCooldown());
//}

///// <summary>
///// Helper method that makes the GameObject moves towards
///// the designated bait object.
///// </summary>
///// <returns></returns>
//IEnumerator MoveToBait()
//{
//    Debug.Log("Moving to bait");
//    if (animator != null)
//    {
//        animator.SetBool("Moving", true);
//    }

//    Vector3 baitPosition = bait.position;
//    Vector3 directionToBait = (baitPosition - transform.position).normalized;
//    Vector3 stopPosition = baitPosition - directionToBait * baitStopDistance;

//    agent.SetDestination(stopPosition);

//    while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
//    {
//        yield return null;
//    }

//    if (animator != null)
//    {
//        animator.SetBool("Moving", false);
//    }

//    hasReachedBait = true;
//    //agent.enabled = false;
//    Debug.Log("Done moving towards bait");
//}

///// <summary>
///// Helper method that checks if the player has been within avoid distance of the GameObject
///// for a set amount of time, resetting the avoid speed of the GameObject if the player has not.
///// </summary>
//IEnumerator AvoidCooldown()
//{
//    float elapsedT = 0f;
//    while (elapsedT < cooldownTime)
//    {
//        if (Vector3.Distance(player.position, transform.position) <= avoidDistance)
//        {
//            yield break;
//        }
//        elapsedT += Time.deltaTime;
//        yield return null;
//    }
//    increment = 0;
//    recentlyAvoided = false;
//    agent.speed = baseSpeed;
//}

///// <summary>
///// Helper method to temporarily prevent the mammoth from moving
///// after reaching its bait.
///// </summary>
//IEnumerator BaitCooldown()
//{
//    immobileFromBait = true;
//    float elapsedT = 0f;
//    while (elapsedT < baitCooldownTime)
//    {
//        elapsedT += Time.deltaTime;
//        yield return null;
//    }
//    immobileFromBait = false;
//}