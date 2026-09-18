using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformTranslate : MonoBehaviour
{
	[Header("Movement Settings")]
	public Vector3 moveDistance = new Vector3(2f, 0f, 0f);
	public float cycleTime = 4.0f;
	public float timeOffset = 0f;

	private Vector3 startPosition;
	private Vector3 previousPosition;
	private Rigidbody rb;

	// List to keep track of players/objects standing on the platform
	private List<CharacterController> passengers = new List<CharacterController>();

	public static List<PlatformTranslate> allPlatforms = new List<PlatformTranslate>();

	void OnEnable() { allPlatforms.Add(this); }
	void OnDisable() { allPlatforms.Remove(this); }

	void Start()
	{
		startPosition = transform.position;
		previousPosition = startPosition;

		rb = GetComponent<Rigidbody>();
		if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

		rb.isKinematic = true;
		rb.useGravity = false;
	}

	void FixedUpdate()
	{
		// 1. Calculate the new position
		float time = (Time.time + timeOffset) * (Mathf.PI * 2 / cycleTime);
		float movementFactor = Mathf.Sin(time);
		Vector3 targetPosition = startPosition + (moveDistance * movementFactor);

		// 2. Calculate the "Delta" (the difference in position since last frame)
		Vector3 platformDelta = targetPosition - transform.position;

		// 3. Move the passengers by that delta
		foreach (CharacterController passenger in passengers)
		{
			if (passenger != null)
			{
				// We use .Move() so the player physically shifts with the wood
				passenger.Move(platformDelta);
			}
		}

		// 4. Move the platform itself
		rb.MovePosition(targetPosition);
	}

	// Detection using the "Trigger Zone" method
	private void OnTriggerEnter(Collider other)
	{
		CharacterController cc = other.transform.root.GetComponent<CharacterController>();
		if (cc != null && !passengers.Contains(cc))
		{
			passengers.Add(cc);
		}
	}

	private void OnTriggerExit(Collider other)
	{
		CharacterController cc = other.transform.root.GetComponent<CharacterController>();
		if (cc != null && passengers.Contains(cc))
		{
			passengers.Remove(cc);
		}
	}
	public static void ClearAllPassengers()
	{
		foreach (PlatformTranslate platform in allPlatforms)
		{
			platform.passengers.Clear();
		}
		Debug.Log("All platform passenger lists cleared!");
	}
}