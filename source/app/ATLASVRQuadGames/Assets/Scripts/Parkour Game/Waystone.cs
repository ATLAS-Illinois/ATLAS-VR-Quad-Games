using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;

public class Waystone : MonoBehaviour
{
	[Header("UI Reference")]
	public TextMeshProUGUI levelText;
	public TextMeshProUGUI statusText;

	private int selectedLevel = 0;
	private bool playerInRange = false;

	// Player Script References
	private PlayerParkourRespawn playerScript;
	private SmoothJump jumpScript;

	void Start()
	{
		if (statusText != null) statusText.text = "";
	}

	void Update()
	{
		if (!playerInRange || playerScript == null) return;

		// Y (Increase)
		if (OVRInput.GetDown(OVRInput.Button.Four))
		{
			selectedLevel++;
			UpdateUI();
		}

		// X (Decrease)
		if (OVRInput.GetDown(OVRInput.Button.Three))
		{
			if (selectedLevel > 0) selectedLevel--;
			UpdateUI();
		}

		// A (Confirm)
		if (OVRInput.GetDown(OVRInput.Button.One))
		{
			ConfirmSelection();
		}
	}

	void UpdateUI()
	{
		if (levelText == null) return;
		levelText.text = selectedLevel.ToString();

		bool levelExists = FindObjectsOfType<RespawnPoint>().Any(p => p.stageNumber == selectedLevel);
		bool isLocked = !playerScript.bypassLevelRestriction && selectedLevel > playerScript.maxStageReached;

		if (isLocked || !levelExists)
			levelText.color = Color.red;
		else
			levelText.color = Color.white;
	}

	void ConfirmSelection()
	{
		bool levelExists = FindObjectsOfType<RespawnPoint>().Any(p => p.stageNumber == selectedLevel);
		bool isLocked = !playerScript.bypassLevelRestriction && selectedLevel > playerScript.maxStageReached;

		if (!levelExists)
		{
			ShowStatusMessage("Level doesn't exist", Color.red);
		}
		else if (isLocked)
		{
			ShowStatusMessage("Respawn number not reached", new Color(1f, 0.6f, 0f));
		}
		else
		{
			playerScript.SetRespawnByLevel(selectedLevel);
			levelText.color = Color.green;
			ShowStatusMessage("Respawn set!", Color.green);
		}
	}

	void ShowStatusMessage(string message, Color color)
	{
		if (statusText == null) return;
		StopAllCoroutines();
		StartCoroutine(StatusTimer(message, color));
	}

	IEnumerator StatusTimer(string message, Color color)
	{
		statusText.text = message;
		statusText.color = color;
		yield return new WaitForSeconds(5.0f);
		statusText.text = "";
	}

	private void OnTriggerEnter(Collider other)
	{
		if (other.transform.root.CompareTag("Player"))
		{
			playerInRange = true;

			// Get both scripts from the player
			playerScript = other.transform.root.GetComponent<PlayerParkourRespawn>();
			jumpScript = other.transform.root.GetComponent<SmoothJump>();

			// Disable jumping!
			if (jumpScript != null) jumpScript.isAtWaystone = true;

			UpdateUI();
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (other.transform.root.CompareTag("Player"))
		{
			playerInRange = false;

			// Re-enable jumping when they walk away!
			if (jumpScript != null) jumpScript.isAtWaystone = false;

			if (statusText != null) statusText.text = "";
		}
	}
}