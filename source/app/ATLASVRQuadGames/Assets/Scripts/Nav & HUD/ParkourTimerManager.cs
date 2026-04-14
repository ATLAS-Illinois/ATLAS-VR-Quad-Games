using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ParkourTimerManager : MonoBehaviour
{
	[Header("UI Reference")]
	// This is like your 'respawnEffectObject' - the thing we hide/show
	public GameObject timerHUDObject;
	public TextMeshProUGUI countdownText;

	[Header("Settings")]
	public Color normalColor = Color.white;
	public Color warningColor = Color.red;

	private float timeRemaining;
	private bool isTimerActive = false;
	private Transform startPlatformPoint;
	private CharacterController controller;

	void Start()
	{
		controller = GetComponent<CharacterController>();

		// HIDE FROM START (Exactly like your Respawn script)
		if (timerHUDObject != null)
			timerHUDObject.SetActive(false);
	}

	void Update()
	{
		if (!isTimerActive) return;

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

	public void StartTimer(float seconds, Transform startPoint)
	{
		// Cancel any pending "Hide" so it doesn't vanish early
		CancelInvoke("HideHUD");

		timeRemaining = seconds;
		startPlatformPoint = startPoint;
		isTimerActive = true;

		if (countdownText != null) countdownText.color = normalColor;

		// SHOW (Exactly like your EffectTimer)
		if (timerHUDObject != null)
			timerHUDObject.SetActive(true);
	}

	public void StopTimer()
	{
		if (!isTimerActive) return;
		isTimerActive = false;

		if (countdownText != null)
		{
			countdownText.text = "CLEARED!";
			countdownText.color = Color.green;
		}

		// Wait then Hide
		Invoke("HideHUD", 2f);
	}

	void UpdateUI()
	{
		if (countdownText == null) return;
		countdownText.text = timeRemaining.ToString("F1");
		countdownText.color = (timeRemaining <= 3f) ? warningColor : normalColor;
	}

	void TimerFailed()
	{
		isTimerActive = false;
		if (countdownText != null)
		{
			countdownText.text = "OUT OF TIME!";
			countdownText.color = warningColor;
		}

		TeleportBack();
		Invoke("HideHUD", 2f);
	}

	void TeleportBack()
	{
		if (startPlatformPoint == null) return;

		PlatformTranslate.ClearAllPassengers();

		if (controller != null) controller.enabled = false;

		transform.position = startPlatformPoint.position;
		transform.rotation = startPlatformPoint.rotation;

		if (controller != null) controller.enabled = true;
	}

	void HideHUD()
	{
		if (timerHUDObject != null)
			timerHUDObject.SetActive(false);
	}
}