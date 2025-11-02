using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NavMeshMovement : MonoBehaviour
{
    // Global variables used throughout the script. May be changed from the Unity Editor.
    public float wanderWaitTime = 2.0f; // in seconds
    public float wanderTriggerChance = 0.3f; // has to be between 0 and 1
    public float avoidDistance = 4.0f;
    public float cooldownTime = 30f;

    [SerializeField]
    private Transform player;

    public float agentSpeed = 6.0f; // Adjust this value to change speed
    public float agentAcceleration = 8.0f; // Adjust this value to change acceleration

    private Animator animator;
    private NavMeshAgent agent;
    private Coroutine currentCoroutine;

    // Variables used for the GameObject's avoiding behavior.
    private bool isAvoiding = false;
    private bool recentlyAvoided = false;
    private int increment = 1;

    // Start is called before the first frame update

    void Start()
    {
        Debug.Log("Starting Object!");
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

        // Set agent speed and acceleration
        agent.speed = agentSpeed;
        agent.acceleration = agentAcceleration;

        currentCoroutine = StartCoroutine(Wander());
    }

    /// <summary>
    /// Wandering movement of the asset. Follows a randomly generated
    /// path.
    /// </summary>
    IEnumerator Wander()
    {
        while (true)
        {
            yield return new WaitForSeconds(wanderWaitTime);
            if (Random.value < wanderTriggerChance)
            {
                if (animator != null)
                {
                    animator.SetBool("Moving", true);
                }

                Vector3 wanderTarget = GetRandomNavMeshLocation();
                Debug.Log("Wandering to: " + wanderTarget);
                agent.SetDestination(wanderTarget);

                while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
                {
                    yield return null;
                }

                if (animator != null)
                {
                    animator.SetBool("Moving", false);
                }
            }
        }
    }

    /// <summary>
    /// Generates a random position on the NavMesh
    /// </summary>
    /// <returns> A vector pointing towards the random direction </returns>
    Vector3 GetRandomNavMeshLocation()
    {
        Vector3 randomDirection = Random.insideUnitSphere * avoidDistance;
        randomDirection += transform.position;
        NavMeshHit navHit;
        NavMesh.SamplePosition(randomDirection, out navHit, avoidDistance, NavMesh.AllAreas);
        return navHit.position;
    }

    /// <summary>
    /// Avoiding behavior of the GameObject. It moves away from the player.
    /// TODO: Implement the faster movement
    /// </summary>
    IEnumerator Avoid()
    {
        isAvoiding = true;
        Vector3 directionAwayFromPlayer = (transform.position - player.position).normalized;
        float modifier = Mathf.Pow(1.5f, increment);
        Vector3 avoidTarget = transform.position + directionAwayFromPlayer * (modifier * avoidDistance);

        NavMeshHit navHit;
        NavMesh.SamplePosition(avoidTarget, out navHit, avoidDistance, NavMesh.AllAreas);

        if (animator != null)
        {
            animator.SetBool("Moving", true);
        }

        Debug.Log("Avoiding to: " + navHit.position);
        agent.SetDestination(navHit.position);

        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
        {
            yield return null;
        }

        if (animator != null)
        {
            animator.SetBool("Moving", false);
        }

        if (!recentlyAvoided)
        {
            recentlyAvoided = true;
        }

        isAvoiding = false;

        currentCoroutine = StartCoroutine(Wander());

        StartCoroutine(AvoidCooldown());
    }

    IEnumerator AvoidCooldown()
    {
        float elapsedT = 0f;
        while (elapsedT < cooldownTime)
        {
            if (Vector3.Distance(player.position, transform.position) <= avoidDistance)
            {
                yield break;
            }
            elapsedT += Time.deltaTime;
            yield return null;
        }
        increment = 0;  // Reset increment after cooldown period
        recentlyAvoided = false;
    }

    void Update()
    {
        float distanceFromPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceFromPlayer < avoidDistance && !isAvoiding)
        {
            if (currentCoroutine != null)
            {
                StopCoroutine(currentCoroutine);
            }
            currentCoroutine = StartCoroutine(Avoid());
        }
    }
}
