using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimerEndTrigger : MonoBehaviour
{
	public TowerController targetTower; // <-- Drag your Tower here

	private void OnTriggerEnter(Collider other)
	{
		if (other.transform.root.CompareTag("Player"))
		{
			var manager = other.transform.root.GetComponent<ParkourTimerManager>();
			if (manager != null)
			{
				manager.StopTimer();

				// Disable the tower colliders!
				if (targetTower != null) targetTower.SetTowerColliders(false);
			}
		}
	}
}
