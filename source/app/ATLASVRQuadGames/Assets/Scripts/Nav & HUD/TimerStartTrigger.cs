using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimerStartTrigger : MonoBehaviour
{
	public float challengeTime = 10.0f;
	public Transform spawnPoint;
	public TowerController targetTower; // <-- Drag your Tower here

	private void OnTriggerEnter(Collider other)
	{
		if (other.transform.root.CompareTag("Player"))
		{
			var manager = other.transform.root.GetComponent<ParkourTimerManager>();
			if (manager != null)
			{
				manager.StartTimer(challengeTime, spawnPoint != null ? spawnPoint : transform);

				// Reset the tower so it's solid again for the new run
				if (targetTower != null) targetTower.SetTowerColliders(true);
			}
		}
	}
}