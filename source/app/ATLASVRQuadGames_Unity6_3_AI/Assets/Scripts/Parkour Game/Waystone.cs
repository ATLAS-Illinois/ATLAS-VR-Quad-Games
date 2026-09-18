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

	[Header("Settings")]
	public float messageDuration = 3.0f; // Editable in Inspector
	private int levelIncrement = 1;      // Starts at 1

	private int selectedLevel = 0;
	private bool playerInRange = false;

	private PlayerParkourRespawn playerScript;
	private SmoothJump jumpScript;

	void Start()
	{
		if (statusText != null) statusText.text = "";
	}

	void Update()
	{
		if (!playerInRange || playerScript == null) return;

		// B Button (Right Controller) - Toggle Increment
		if (OVRInput.GetDown(OVRInput.Button.Two))
		{
			ToggleIncrement();
		}

		// Y (Left Controller) - Increase by Increment
		if (OVRInput.GetDown(OVRInput.Button.Four))
		{
			selectedLevel += levelIncrement;
			UpdateUI();
		}

		// X (Left Controller) - Decrease by Increment
		if (OVRInput.GetDown(OVRInput.Button.Three))
		{
			selectedLevel -= levelIncrement;
			if (selectedLevel < 0) selectedLevel = 0; // Don't go below 0
			UpdateUI();
		}

		// A (Right Controller) - Confirm
		if (OVRInput.GetDown(OVRInput.Button.One))
		{
			ConfirmSelection();
		}
	}

	void ToggleIncrement()
	{
		// Toggle between 1 and 10
		levelIncrement = (levelIncrement == 1) ? 10 : 1;

		// Show the player what the current "speed" is
		ShowStatusMessage("Increment: " + levelIncrement, Color.cyan);
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
		yield return new WaitForSeconds(messageDuration);
		statusText.text = "";
	}

	private void OnTriggerEnter(Collider other)
	{
		if (other.transform.root.CompareTag("Player"))
		{
			playerInRange = true;
			playerScript = other.transform.root.GetComponent<PlayerParkourRespawn>();
			jumpScript = other.transform.root.GetComponent<SmoothJump>();

			if (jumpScript != null) jumpScript.isAtWaystone = true;

			// NEW: Sync the Waystone's number with the Player's current level
			if (playerScript != null)
			{
				selectedLevel = playerScript.currentStageNumber;
			}

			UpdateUI();
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (other.transform.root.CompareTag("Player"))
		{
			playerInRange = false;
			if (jumpScript != null) jumpScript.isAtWaystone = false;
			if (statusText != null) statusText.text = "";
		}
	}
}