using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortalTrigger : MonoBehaviour
{
	[Header("Destination")]
	public Transform destinationPoint;

	[Header("Animation Object")]
	[Tooltip("The object in the scene that should appear during teleport.")]
	public GameObject animationObject;

	[Tooltip("How long the object stays visible.")]
	public float displayDuration = 2.0f;

	private void OnTriggerEnter(Collider other)
	{
		// Check if the thing hitting the trigger is the player
		if (other.transform.root.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
		{
			GameObject root = other.transform.root.gameObject;
			CharacterController controller = root.GetComponent<CharacterController>();

			// 1. Teleport Logic
			if (controller != null)
			{
				PlatformTranslate.ClearAllPassengers();

				controller.enabled = false;
				root.transform.position = destinationPoint.position;
				root.transform.rotation = destinationPoint.rotation;
				controller.enabled = true;
			}

			// 2. Visual Logic
			if (animationObject != null)
			{
				// We use a Coroutine so the 'waiting' happens in the background
				StopAllCoroutines();
				StartCoroutine(PlayEffectSequence());
			}
		}
	}

	private IEnumerator PlayEffectSequence()
	{
		// 1. Unhide
		animationObject.SetActive(true);

		// 2. Force the Animator to play the first state from the beginning
		Animator anim = animationObject.GetComponent<Animator>();
		if (anim != null)
		{
			// 0 is the base layer, "" plays the default state
			anim.Play(0, -1, 0f);
		}

		// 3. Wait
		yield return new WaitForSeconds(displayDuration);

		// 4. Hide
		animationObject.SetActive(false);
	}
}