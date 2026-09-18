using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ParkourTimerManager : MonoBehaviour
{
	[Header("UI Reference")]
	public GameObject timerHUDObject;
	public TextMeshProUGUI countdownText;

	[Header("Settings")]
	public Color normalColor = Color.white;
	public Color warningColor = Color.red;
	public Color successColor = Color.green; // Added for clarity

	[Header("State")]
	public bool isStarted = false;

	private float timeRemaining;
	private Transform startPlatformPoint;
	private CharacterController controller;

	void Start()
	{
		controller = GetComponent<CharacterController>();
		if (timerHUDObject != null) timerHUDObject.SetActive(false);
		isStarted = false;
	}

	void Update()
	{
		if (!isStarted) return;

		if (timeRemaining > 0)
		{
			timeRemaining -= Time.deltaTime;
			UpdateUI();
		}
		else
		{
			TimerFailed();
		}
	}

	public void StartChallenge(float seconds, Transform startPoint)
	{
		CancelInvoke("HideHUD");

		timeRemaining = seconds;
		startPlatformPoint = startPoint;
		isStarted = true;

		if (countdownText != null)
		{
			countdownText.color = normalColor;
			countdownText.text = timeRemaining.ToString("F1");
		}

		if (timerHUDObject != null) timerHUDObject.SetActive(true);
	}

	public void FinishChallenge()
	{
		if (!isStarted) return;
		isStarted = false;

		if (countdownText != null)
		{
			// Set to COMPLETED and turn GREEN
			countdownText.text = "COMPLETED!";
			countdownText.color = successColor;
		}
		Invoke("HideHUD", 2f);
	}

	void TimerFailed()
	{
		isStarted = false;

		if (countdownText != null)
		{
			// Set to OUT OF TIME and turn RED
			countdownText.text = "OUT OF TIME!";
			countdownText.color = warningColor;
		}

		PlatformTranslate.ClearAllPassengers();
		TeleportBack();
		Invoke("HideHUD", 2f);
	}

	void UpdateUI()
	{
		if (countdownText == null) return;

		countdownText.text = timeRemaining.ToString("F1");

		// Flash Red when close to failing
		if (timeRemaining <= 3f)
		{
			countdownText.color = warningColor;
		}
		else
		{
			countdownText.color = normalColor;
		}
	}

	void TeleportBack()
	{
		if (startPlatformPoint == null) return;
		if (controller != null) controller.enabled = false;
		transform.position = startPlatformPoint.position;
		transform.rotation = startPlatformPoint.rotation;
		if (controller != null) controller.enabled = true;
	}

	void HideHUD()
	{
		if (timerHUDObject != null) timerHUDObject.SetActive(false);
	}
}