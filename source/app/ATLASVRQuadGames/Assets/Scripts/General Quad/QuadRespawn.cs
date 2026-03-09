using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuadRespawn : MonoBehaviour
{
	// Defaulting to 0 in the inspector
	public int quadStageNumber;

	private void OnTriggerEnter(Collider other)
	{
		GameObject root = other.transform.root.gameObject;
		QuadPlayerRespawn qRespawn = root.GetComponent<QuadPlayerRespawn>();

		if (qRespawn != null)
		{
			// Passes the transform AND the stage number
			qRespawn.SetQuadRespawn(this.transform, quadStageNumber);
		}
	}
}
