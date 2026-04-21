using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimerEndTrigger : MonoBehaviour
{
	public TowerController targetTower;

	private void OnTriggerEnter(Collider other)
	{
		if (other.transform.root.CompareTag("Player"))
		{
			var manager = other.transform.root.GetComponent<ParkourTimerManager>();

			// Check if the timer is actually running before letting them finish
			if (manager != null && manager.isStarted)
			{
				manager.FinishChallenge();

				// Disable the tower
				if (targetTower != null) targetTower.SetTowerColliders(false);
			}
		}
	}
}