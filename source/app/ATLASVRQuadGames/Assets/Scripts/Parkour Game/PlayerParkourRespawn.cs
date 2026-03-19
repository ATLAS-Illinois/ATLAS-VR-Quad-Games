using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

public class PlayerParkourRespawn : MonoBehaviour
{
	public Transform currentRespawn;
	public float respawnHeightOffset = 3.0f;

	private CharacterController controller;
	private bool isRespawning = false; // The "Crash-Proof" lock

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
		if (isRespawning || currentRespawn == null) return;
		isRespawning = true;

		// 1. Tell the SPECIFIC platform to play its 2-second effect
		RespawnPoint pointScript = currentRespawn.GetComponent<RespawnPoint>();
		if (pointScript != null)
		{
			pointScript.PlayEffect();
		}

		// 2. Teleport Logic
		if (controller != null) controller.enabled = false;

		transform.position = currentRespawn.position + Vector3.up * respawnHeightOffset;

		// 3. Clear Momentum (prevents the physics engine from crashing)
		ClearMomentum();

		if (controller != null) controller.enabled = true;

		// Unlock after a tiny delay so we don't double-trigger
		Invoke(nameof(ResetRespawnLock), 0.1f);
	}

	private void ClearMomentum()
	{
		var ovr = GetComponent<OVRPlayerController>();
		if (ovr != null)
		{
			var field = typeof(OVRPlayerController).GetField("MoveThrottle",
				BindingFlags.NonPublic | BindingFlags.Instance);
			if (field != null) field.SetValue(ovr, Vector3.zero);
		}
	}

	private void ResetRespawnLock()
	{
		isRespawning = false;
	}
}