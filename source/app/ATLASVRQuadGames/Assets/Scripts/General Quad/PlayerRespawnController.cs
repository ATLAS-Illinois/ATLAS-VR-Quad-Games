using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
	public Transform currentRespawn;
	public float respawnHeightOffset = 1.5f;

	public void SetRespawnPoint(Transform point)
	{
		currentRespawn = point;
		Debug.Log("Respawn point set to: " + point.name);
	}

	public void Respawn()
	{
		if (currentRespawn == null) return;

		Vector3 respawnPos = currentRespawn.position + Vector3.up * respawnHeightOffset;
		transform.position = respawnPos;

		Debug.Log("Player respawned");
	}
}
