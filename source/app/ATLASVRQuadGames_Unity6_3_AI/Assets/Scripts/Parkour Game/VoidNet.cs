using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoidNet : MonoBehaviour
{
	private void OnTriggerEnter(Collider other)
	{
		// Get the root object (OVRPlayerController)
		GameObject root = other.transform.root.gameObject;
		PlayerParkourRespawn respawn = root.GetComponent<PlayerParkourRespawn>();

		if (respawn != null)
		{
			respawn.Respawn();
		}
	}
}