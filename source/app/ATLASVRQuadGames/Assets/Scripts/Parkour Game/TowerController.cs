using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TowerController : MonoBehaviour
{
	private Collider[] allColliders;

	void Awake()
	{
		// Automatically find every collider in the tower's children
		allColliders = GetComponentsInChildren<Collider>();
	}

	public void SetTowerColliders(bool state)
	{
		if (allColliders == null) return;

		foreach (Collider col in allColliders)
		{
			col.enabled = state;
		}

		Debug.Log(state ? "Tower Colliders Enabled" : "Tower Colliders Disabled");
	}
}