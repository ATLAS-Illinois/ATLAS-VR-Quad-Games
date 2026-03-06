using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LadderClimb : MonoBehaviour
{
	private void OnTriggerEnter(Collider other)
	{
		// Check if the object belongs to the Player
		Transform root = other.transform.root;
		if (!root.CompareTag("Player")) return;

		PlayerClimbController climb = root.GetComponent<PlayerClimbController>();

		if (climb != null)
		{
			climb.isOnLadder = true;
			Debug.Log("On Ladder");
		}
	}

	private void OnTriggerExit(Collider other)
	{
		Transform root = other.transform.root;
		if (!root.CompareTag("Player")) return;

		PlayerClimbController climb = root.GetComponent<PlayerClimbController>();

		if (climb != null)
		{
			climb.isOnLadder = false;
			Debug.Log("Off Ladder");
		}
	}
}