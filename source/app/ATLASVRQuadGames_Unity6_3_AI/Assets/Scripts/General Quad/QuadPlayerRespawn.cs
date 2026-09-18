using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuadPlayerRespawn : MonoBehaviour
{
	public Transform currentQuadRespawn;
	public int currentStage = 0;
	public float respawnHeightOffset = 1.0f;

	[Header("Effects")]
	[Tooltip("The Teleporter effect object that stays hidden by default.")]
	public GameObject respawnEffectObject;

	private CharacterController controller;
	private bool isRespawning = false;

	void Start()
	{
		controller = GetComponent<CharacterController>();

		// Ensure the effect is hidden when the game starts
		if (respawnEffectObject != null)
			respawnEffectObject.SetActive(false);
	}

	public void SetQuadRespawn(Transform point, int stageNum)
	{
		if (stageNum >= currentStage)
		{
			currentQuadRespawn = point;
			currentStage = stageNum;
		}
	}

	public void RespawnAtQuad()
	{
		if (isRespawning || currentQuadRespawn == null) return;

		isRespawning = true;

		// 1. TRIGGER THE VISUAL EFFECT
		if (respawnEffectObject != null)
		{
			StartCoroutine(TriggerEffectTimer());
		}

		// 2. DISABLE PHYSICS & TELEPORT
		if (controller != null) controller.enabled = false;

		Vector3 respawnPos = currentQuadRespawn.position + Vector3.up * respawnHeightOffset;
		transform.position = respawnPos;

		// 3. CLEAR MOMENTUM (Prevents falling physics from carrying over)
		var ovr = GetComponent<OVRPlayerController>();
		if (ovr != null)
		{
			var field = typeof(OVRPlayerController).GetField("MoveThrottle",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (field != null) field.SetValue(ovr, Vector3.zero);
		}

		if (controller != null) controller.enabled = true;

		// Small delay to unlock the script so we don't double-trigger
		Invoke(nameof(ResetRespawnLock), 0.1f);
	}

	// ---------------------------
	// THE 2-SECOND EFFECT TIMER
	// ---------------------------
	private IEnumerator TriggerEffectTimer()
	{
		respawnEffectObject.SetActive(true); // Show it (like SF_Rainbow)

		yield return new WaitForSeconds(5.0f); // Wait for exactly 2 seconds

		respawnEffectObject.SetActive(false); // Hide it again
	}

	private void ResetRespawnLock()
	{
		isRespawning = false;
	}
}