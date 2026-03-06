using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnPoint : MonoBehaviour
{
	public int stageNumber;

	private void OnTriggerEnter(Collider other)
	{
		GameObject root = other.transform.root.gameObject;
		PlayerParkourRespawn respawn = root.GetComponent<PlayerParkourRespawn>();

		if (respawn != null)
		{
			// Only update if this is a newer stage or a different point
			if (respawn.currentRespawn != transform)
			{
				respawn.SetRespawnPoint(transform);
				Debug.Log("Stage " + stageNumber + " Saved!");
			}
		}
	}
}