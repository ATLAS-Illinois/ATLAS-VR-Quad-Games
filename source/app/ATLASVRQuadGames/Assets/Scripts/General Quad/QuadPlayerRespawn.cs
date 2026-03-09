using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuadPlayerRespawn : MonoBehaviour
{
	public Transform currentQuadRespawn;
	public int currentStage = 0; // Starts at 0 by default
	public float respawnHeightOffset = 1.0f;

	private CharacterController controller;

	void Start()
	{
		controller = GetComponent<CharacterController>();
	}

	public void SetQuadRespawn(Transform point, int stageNum)
	{
		// Only update if the new stage is the same or higher than our current one
		if (stageNum >= currentStage)
		{
			currentQuadRespawn = point;
			currentStage = stageNum;
			Debug.Log($"Quad Respawn set to Stage {stageNum}: {point.name}");
		}
	}

	public void RespawnAtQuad()
	{
		if (currentQuadRespawn == null)
		{
			Debug.LogWarning("No Quad Respawn point set!");
			return;
		}

		if (controller != null) controller.enabled = false;

		Vector3 respawnPos = currentQuadRespawn.position + Vector3.up * respawnHeightOffset;
		transform.position = respawnPos;

		if (controller != null) controller.enabled = true;
	}
}
