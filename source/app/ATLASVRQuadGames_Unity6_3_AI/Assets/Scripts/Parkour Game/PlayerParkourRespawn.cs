using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System.Linq;

public class PlayerParkourRespawn : MonoBehaviour
{
	public Transform currentRespawn;
	public float respawnHeightOffset = 3.0f;

	[Header("Progression")]
	public int maxStageReached = 0; // The "gatekeeper" for the Waystone
	public int currentStageNumber = 0; // Tracks the CURRENT active level
	public bool bypassLevelRestriction = false;

	private CharacterController controller;
	private bool isRespawning = false;

	void Start()
	{
		controller = GetComponent<CharacterController>();
	}

	// Called when physically hitting a RespawnPoint trigger
	public void SetRespawnPoint(Transform point, int stageNum)
	{
		currentRespawn = point;
		currentStageNumber = stageNum; // Update the active ID

		if (stageNum > maxStageReached)
		{
			maxStageReached = stageNum;
			Debug.Log($"Max Level Updated: {maxStageReached}");
		}
	}

	public void SetRespawnByLevel(int levelID)
	{
		if (!bypassLevelRestriction && levelID > maxStageReached) return;

		RespawnPoint target = FindObjectsOfType<RespawnPoint>()
			.FirstOrDefault(p => p.stageNumber == levelID);

		if (target != null)
		{
			currentRespawn = target.transform;
			currentStageNumber = levelID; // Sync the ID here
			Debug.Log($"Waystone set respawn to Stage: {levelID}");
		}
	}

	public void Respawn()
	{
		if (isRespawning || currentRespawn == null) return;
		isRespawning = true;

		RespawnPoint pointScript = currentRespawn.GetComponent<RespawnPoint>();
		if (pointScript != null) pointScript.PlayEffect();

		if (controller != null) controller.enabled = false;
		transform.position = currentRespawn.position + Vector3.up * respawnHeightOffset;
		ClearMomentum();
		if (controller != null) controller.enabled = true;

		Invoke(nameof(ResetRespawnLock), 0.1f);
	}

	private void ClearMomentum()
	{
		var ovr = GetComponent<OVRPlayerController>();
		if (ovr != null)
		{
			var field = typeof(OVRPlayerController).GetField("MoveThrottle", BindingFlags.NonPublic | BindingFlags.Instance);
			if (field != null) field.SetValue(ovr, Vector3.zero);
		}
	}

	private void ResetRespawnLock() => isRespawning = false;
}