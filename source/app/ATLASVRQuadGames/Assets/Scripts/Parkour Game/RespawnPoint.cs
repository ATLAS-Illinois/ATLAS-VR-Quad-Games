using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnPoint : MonoBehaviour
{
	public int stageNumber;

	[Header("Visual Effect")]
	[Tooltip("The specific teleporter effect child for THIS platform.")]
	public GameObject respawnEffectObject;

	void Start()
	{
		// Make sure it starts hidden
		if (respawnEffectObject != null)
			respawnEffectObject.SetActive(false);
	}

	public void PlayEffect()
	{
		if (respawnEffectObject != null)
		{
			StartCoroutine(EffectTimer());
		}
	}

	private IEnumerator EffectTimer()
	{
		respawnEffectObject.SetActive(true);
		yield return new WaitForSeconds(2.0f);
		respawnEffectObject.SetActive(false);
	}

	private void OnTriggerEnter(Collider other)
	{
		GameObject root = other.transform.root.gameObject;
		PlayerParkourRespawn respawn = root.GetComponent<PlayerParkourRespawn>();

		if (respawn != null && respawn.currentRespawn != transform)
		{
			respawn.SetRespawnPoint(transform);
			Debug.Log($"Stage {stageNumber} Saved!");
		}
	}
}