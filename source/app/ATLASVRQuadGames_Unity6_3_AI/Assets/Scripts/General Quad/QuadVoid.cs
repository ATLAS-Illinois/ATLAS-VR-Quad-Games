using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuadVoid : MonoBehaviour
{
	private void OnTriggerEnter(Collider other)
	{
		GameObject root = other.transform.root.gameObject;
		QuadPlayerRespawn qRespawn = root.GetComponent<QuadPlayerRespawn>();

		if (qRespawn != null)
		{
			qRespawn.RespawnAtQuad();
		}
	}
}
