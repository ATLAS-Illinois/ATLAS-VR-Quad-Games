using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

public class PlayerClimbController : MonoBehaviour
{
	public float climbSpeed = 2.5f;
	public float horizontalClimbSpeed = 2f;
	public bool isOnLadder = false;

	[Header("Vault Settings")]
	public float vaultForwardDistance = 1.2f; // How far forward to "teleport"
	public float vaultUpDistance = 1.0f;      // How high to "teleport"
	public float vaultCooldown = 0.5f;

	private OVRPlayerController ovr;
	private CharacterController controller;
	private FieldInfo moveThrottleField;
	private float cooldownTimer = 0f;

	void Start()
	{
		ovr = GetComponent<OVRPlayerController>();
		controller = GetComponent<CharacterController>();
		moveThrottleField = typeof(OVRPlayerController)
			.GetField("MoveThrottle", BindingFlags.NonPublic | BindingFlags.Instance);
	}

	void Update()
	{
		if (cooldownTimer > 0)
		{
			cooldownTimer -= Time.deltaTime;
			isOnLadder = false;
			return;
		}

		if (!isOnLadder) return;

		// Button X (Left Controller)
		if (OVRInput.GetDown(OVRInput.Button.Three))
		{
			ExecuteVault();
			return;
		}

		HandleClimbing();
	}

	public void ExecuteVault()
	{
		Debug.Log("Direct Vault Executed!");

		// 1. CALCULATE DESTINATION
		// We move based on where the player is looking
		Vector3 vaultVector = (transform.forward * vaultForwardDistance) + (Vector3.up * vaultUpDistance);

		// 2. KILL ALL EXISTING VELOCITY
		// We stop the player's current "falling" or "climbing" momentum 
		// so it doesn't interfere with the vault
		moveThrottleField.SetValue(ovr, Vector3.zero);

		// 3. THE DIRECT MOVE
		// This moves the CharacterController instantly, ignoring OVR friction
		controller.Move(vaultVector);

		// 4. START COOLDOWN
		cooldownTimer = vaultCooldown;
		isOnLadder = false;
	}

	private void HandleClimbing()
	{
		Vector2 input = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
		Vector3 forward = transform.forward;
		Vector3 right = transform.right;
		forward.y = 0; right.y = 0;
		forward.Normalize(); right.Normalize();

		Vector3 move = (forward * input.y + right * input.x) * horizontalClimbSpeed;

		float verticalMove = 0f;
		if (OVRInput.Get(OVRInput.Button.One)) verticalMove = climbSpeed;
		else if (OVRInput.Get(OVRInput.Button.Two)) verticalMove = -climbSpeed;

		move.y = verticalMove;
		moveThrottleField.SetValue(ovr, move * Time.deltaTime);
	}
}