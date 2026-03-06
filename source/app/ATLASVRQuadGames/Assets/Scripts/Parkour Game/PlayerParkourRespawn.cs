using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerParkourRespawn : MonoBehaviour
{
	public Transform currentRespawn;
	public float respawnHeightOffset = 3.0f;

	private CharacterController controller;

	void Start()
	{
		controller = GetComponent<CharacterController>();
	}

	public void SetRespawnPoint(Transform point)
	{
		currentRespawn = point;
		Debug.Log("Respawn point set to: " + point.name);
	}

	public void Respawn()
	{
		if (currentRespawn == null) return;

		// THE FIX: Disable controller so transform.position actually works
		if (controller != null) controller.enabled = false;

		Vector3 respawnPos = currentRespawn.position + Vector3.up * respawnHeightOffset;

		// Move the whole OVRPlayerController root
		transform.position = respawnPos;

		// Re-enable controller
		if (controller != null) controller.enabled = true;

		Debug.Log("Player respawned at " + currentRespawn.name);
	}
}