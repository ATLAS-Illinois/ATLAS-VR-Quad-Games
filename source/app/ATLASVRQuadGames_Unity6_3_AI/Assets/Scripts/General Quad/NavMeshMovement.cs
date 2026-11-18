using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

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

    [Header("Game Over Cutscene / Reset")]
    [Tooltip("How close the player has to get to the golden donut to anger the mammoth.")]
    public float donutTriggerDistance = 2f;

    [Tooltip("Seconds the mammoth stands still (donut on its tusk) before walking the donut back.")]
    public float gameOverPauseTime = 1.5f;

    [Tooltip("Walking speed while returning the donut and going home. Slower than the chase so it reads as a cutscene.")]
    public float returnSpeed = 10f;

    [Tooltip("How close the mammoth needs to get to the pedestal / its stand to count as arrived.")]
    public float returnStopDistance = 3f;

    [Tooltip("If the mammoth is stuck for this many seconds on a return leg, it is warped to the destination instead.")]
    public float returnLegTimeout = 20f;

    [Tooltip("Seconds spent turning back to face its original direction at the end of the cutscene.")]
    public float finalTurnTime = 0.75f;

    [Tooltip("Invoked when a game over starts (hook up sounds, UI, etc.).")]
    public UnityEvent onGameOver;

    [Tooltip("Invoked when the game is ready to be played again.")]
    public UnityEvent onGameReset;

    //[SerializeField]
    //private Transform bait;

    private Animator animator;
    private NavMeshAgent agent;
    private Coroutine currentCoroutine;
    private Coroutine sequenceCoroutine;

    // Starting state, captured in Start() so every round begins the same way.
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Transform donutStartParent;
    private Vector3 donutStartLocalPosition;
    private Quaternion donutStartLocalRotation;
    private bool donutStartKinematic;
    private bool donutStartUseGravity;
    private bool hasDonutRigidbody;

    // After a reset the player must step away from the donut before it can anger the mammoth again,
    // so standing next to the pedestal while it resets doesn't instantly start a new chase.
    private bool armed = true;
    private bool defaultUpdateRotation = true;

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
    /// True while the game-over cutscene is playing. Other mammoth scripts use this
    /// to ignore collisions / proximity until the game has been reset.
    /// </summary>
    public bool IsResetting => isReturningDonut || isReturningToStart || sequenceCoroutine != null;

    /// <summary>
    /// Start is called before the first frame update.
    /// Initiates the GameObject's properties.
    /// </summary>

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        SetMoving(false);

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

        startPosition = transform.position;
        startRotation = transform.rotation;
        defaultUpdateRotation = agent.updateRotation;
        CaptureDonutStartState();

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

        SetMoving(true);

        Debug.Log("Mammoth: Moving to position " + navHit.position + " to track down " + destination.name);
        agent.SetDestination(navHit.position);

        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
        {
            yield return null;
        }

        SetMoving(false);
    }




    /// <summary>
    /// Update is called once per frame. It is used to check the distance between the player, 
    /// bait, and the GameObject.
    /// </summary>
    /// 
    void Update()
    {
        if (agent == null) return;

        // The game-over cutscene drives the mammoth on its own; don't chase or re-anger meanwhile.
        if (IsResetting) return;

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
            StopChase();

            float playerToDonut = Vector3.Distance(player.position, goldenDonutIdol.transform.position);

            // Re-arm once the player has stepped away from the donut after a reset.
            if (!armed && playerToDonut > donutTriggerDistance + 1f)
            {
                armed = true;
            }

            if (armed && !previouslyEnraged && goldenDonutIdol.activeInHierarchy && playerToDonut < donutTriggerDistance)
            {
                if (signpost != null) signpost.Anger();
                enraged = true;
                previouslyEnraged = true;
            }
            else
            {
                Vector3 directionTowardsPlayer = (transform.position - player.position).normalized * -1;
                agent.SetDestination(transform.position + 0.001f * directionTowardsPlayer);
            }
        }

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

    // ------------------------------------------------------------------
    // GAME OVER CUTSCENE
    // ------------------------------------------------------------------

    /// <summary>
    /// Called by MammothEatDonut when the mammoth gets the donut.
    /// Plays: pause -> walk the donut back to the pedestal -> walk home to the stand -> re-arm the game.
    /// </summary>
    public void BeginGameOverSequence()
    {
        if (IsResetting || agent == null) return;

        enraged = false;
        StopChase();

        if (signpost != null) signpost.ShowMessage("GAME OVER\r\n\r\nthe mammoth got its donut back", 24);
        onGameOver?.Invoke();

        sequenceCoroutine = StartCoroutine(GameOverSequence());
    }

    private IEnumerator GameOverSequence()
    {
        // 1. Brief pause so the player sees the donut on the tusk.
        isReturningDonut = true;
        SetMoving(false);
        agent.ResetPath();
        yield return new WaitForSeconds(gameOverPauseTime);

        // 2. Carry the donut back to the pedestal.
        Vector3 pedestalPosition = goldenPedestal != null ? goldenPedestal.position : DonutWorldStartPosition();
        yield return WalkTo(pedestalPosition);

        // 3. Put the donut back.
        if (tuskCosmeticGoldenDonut != null) tuskCosmeticGoldenDonut.SetActive(false);
        RestoreDonut();
        if (signpost != null) signpost.ShowMessage("the donut is back.\r\n\r\nthink you can do better?", 22);

        // 4. Walk home to the wireframe stand.
        isReturningDonut = false;
        isReturningToStart = true;
        Vector3 homePosition = mammothStand != null ? mammothStand.position : startPosition;
        yield return WalkTo(homePosition);

        // 5. Settle exactly on the starting pose and turn to face the original direction.
        agent.ResetPath();
        agent.Warp(startPosition);
        Quaternion from = transform.rotation;
        agent.updateRotation = false;
        for (float t = 0f; t < finalTurnTime; t += Time.deltaTime)
        {
            transform.rotation = Quaternion.Slerp(from, startRotation, t / finalTurnTime);
            yield return null;
        }
        transform.rotation = startRotation;

        isReturningToStart = false;
        sequenceCoroutine = null;
        FinishReset();
    }

    /// <summary>
    /// Walks the mammoth to a point on the NavMesh. Warps there if it gets stuck,
    /// so the cutscene can never softlock the game.
    /// </summary>
    private IEnumerator WalkTo(Vector3 destination)
    {
        NavMeshHit navHit;
        if (!NavMesh.SamplePosition(destination, out navHit, 10f, NavMesh.AllAreas))
        {
            Debug.LogWarning("Mammoth: no NavMesh near " + destination + ", skipping this leg.");
            yield break;
        }

        agent.speed = returnSpeed;
        agent.acceleration = baseAcceleration;
        agent.stoppingDistance = returnStopDistance;
        agent.SetDestination(navHit.position);
        SetMoving(true);

        float elapsed = 0f;
        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
        {
            if (agent.pathStatus == NavMeshPathStatus.PathInvalid || elapsed > returnLegTimeout)
            {
                Debug.LogWarning("Mammoth: couldn't walk to " + navHit.position + ", warping instead.");
                agent.Warp(navHit.position);
                break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetMoving(false);
    }

    // ------------------------------------------------------------------
    // RESET
    // ------------------------------------------------------------------

    /// <summary>
    /// Skips the cutscene and puts everything back immediately.
    /// Used by MammothResetButton; safe to call at any time (mid-chase, mid-cutscene, or idle).
    /// </summary>
    public void ResetGameInstantly()
    {
        if (agent == null) return;

        StopChase();
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        enraged = false;
        isReturningDonut = false;
        isReturningToStart = false;

        if (tuskCosmeticGoldenDonut != null) tuskCosmeticGoldenDonut.SetActive(false);
        RestoreDonut();

        agent.ResetPath();
        agent.Warp(startPosition);
        transform.rotation = startRotation;

        FinishReset();
    }

    private void FinishReset()
    {
        agent.updateRotation = defaultUpdateRotation;
        agent.speed = baseSpeed;
        agent.acceleration = baseAcceleration;
        agent.stoppingDistance = baitStopDistance;
        SetMoving(false);

        enraged = false;
        previouslyEnraged = false;
        armed = false; // re-arms once the player steps away from the donut
        noFrames = 0;

        if (signpost != null) signpost.Calm();
        Debug.Log("Mammoth: game reset, ready to play again.");
        onGameReset?.Invoke();
    }

    private void StopChase()
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
            currentCoroutine = null;
        }
        SetMoving(false);
    }

    private void SetMoving(bool moving)
    {
        if (animator != null)
        {
            animator.SetBool("Moving", moving);
        }
    }

    private void CaptureDonutStartState()
    {
        if (goldenDonutIdol == null) return;

        Transform donut = goldenDonutIdol.transform;
        donutStartParent = donut.parent;
        donutStartLocalPosition = donut.localPosition;
        donutStartLocalRotation = donut.localRotation;

        Rigidbody rb = goldenDonutIdol.GetComponent<Rigidbody>();
        hasDonutRigidbody = rb != null;
        if (hasDonutRigidbody)
        {
            donutStartKinematic = rb.isKinematic;
            donutStartUseGravity = rb.useGravity;
        }
    }

    private Vector3 DonutWorldStartPosition()
    {
        return donutStartParent != null
            ? donutStartParent.TransformPoint(donutStartLocalPosition)
            : donutStartLocalPosition;
    }

    /// <summary>
    /// Puts the real (grabbable) golden donut back on the pedestal exactly where it started.
    /// Deactivating it first forces any hand that is still holding it to let go.
    /// </summary>
    private void RestoreDonut()
    {
        if (goldenDonutIdol == null) return;

        goldenDonutIdol.SetActive(false);

        Transform donut = goldenDonutIdol.transform;
        donut.SetParent(donutStartParent, false);
        donut.localPosition = donutStartLocalPosition;
        donut.localRotation = donutStartLocalRotation;

        if (hasDonutRigidbody)
        {
            Rigidbody rb = goldenDonutIdol.GetComponent<Rigidbody>();
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = donutStartKinematic;
            rb.useGravity = donutStartUseGravity;
        }

        goldenDonutIdol.SetActive(true);
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