using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

public class SmoothJump : MonoBehaviour
{
	private OVRPlayerController ovr;
	private CharacterController controller;

	[Header("Jump Settings")]
	public float jumpHeight = 1.2f;
	public float gravity = -15.0f;

	[Header("Air Control")]
	public float airSpeed = 3.0f;
	[Range(0, 1)] public float airDamping = 0.1f;

	private float verticalVelocity;
	private Vector3 horizontalMove;
	private FieldInfo moveThrottleField;

	void Start()
	{
		ovr = GetComponent<OVRPlayerController>();
		controller = GetComponent<CharacterController>();
		ovr.GravityModifier = 0f;

		moveThrottleField = typeof(OVRPlayerController)
			.GetField("MoveThrottle", BindingFlags.NonPublic | BindingFlags.Instance);
	}

	void Update()
	{
		bool isGrounded = controller.isGrounded;

		// 1. GET CURRENT THROTTLE
		// We read what OVR calculated this frame before we mess with it
		Vector3 currentThrottle = (Vector3)moveThrottleField.GetValue(ovr);

		// 2. CALCULATE AIR INPUT
		Vector2 primaryAxis = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
		Vector3 forward = transform.forward;
		Vector3 right = transform.right;
		forward.y = 0; right.y = 0;
		forward.Normalize(); right.Normalize();
		Vector3 targetAirMove = (forward * primaryAxis.y + right * primaryAxis.x) * airSpeed;

		if (isGrounded)
		{
			// Reset our air tracker when we hit the floor
			horizontalMove = Vector3.zero;

			if (verticalVelocity < 0) verticalVelocity = -1f;

			if (OVRInput.GetDown(OVRInput.Button.One))
			{
				// Physics: v = sqrt(h * -2 * g)
				verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
			}

			// --- THE GROUND FIX ---
			// Keep OVR's horizontal (X, Z), only override the vertical (Y)
			currentThrottle.y = verticalVelocity * Time.deltaTime;
		}
		else
		{
			// --- THE AIR CONTROL ---
			// Smoothly move toward our target air direction
			horizontalMove = Vector3.Lerp(horizontalMove, targetAirMove, airDamping);
			verticalVelocity += gravity * Time.deltaTime;

			// In the air, we overwrite everything to ensure "Parkour" feel
			currentThrottle = horizontalMove * Time.deltaTime;
			currentThrottle.y = verticalVelocity * Time.deltaTime;
		}

		// 3. INJECT BACK INTO OVR
		if (moveThrottleField != null)
		{
			moveThrottleField.SetValue(ovr, currentThrottle);
		}
	}
}