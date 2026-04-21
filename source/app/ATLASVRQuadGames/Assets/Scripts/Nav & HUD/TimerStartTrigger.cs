using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimerStartTrigger : MonoBehaviour
{
	public float timeForThisChallenge = 15.0f; // Set this in Inspector
	public Transform spawnPoint;
	public TowerController targetTower;

	private void OnTriggerEnter(Collider other)
	{
		if (other.transform.root.CompareTag("Player"))
		{
			var manager = other.transform.root.GetComponent<ParkourTimerManager>();
			if (manager != null)
			{
				// Send the time unique to THIS platform to the manager
				manager.StartChallenge(timeForThisChallenge, spawnPoint != null ? spawnPoint : transform);

				if (targetTower != null) targetTower.SetTowerColliders(true);
			}
		}
	}
}