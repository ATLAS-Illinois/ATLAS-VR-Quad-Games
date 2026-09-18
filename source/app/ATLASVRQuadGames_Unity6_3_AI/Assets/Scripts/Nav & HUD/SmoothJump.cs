using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using UnityEngine;

public class SmoothJump : MonoBehaviour
{
	private OVRPlayerController ovr;
	private CharacterController controller;
	private PlayerClimbController climb;

	[Header("State Flags")]
	public bool isAtWaystone = false;

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
		climb = GetComponent<PlayerClimbController>();
		ovr.GravityModifier = 0f;

		moveThrottleField = typeof(OVRPlayerController)
			.GetField("MoveThrottle", BindingFlags.NonPublic | BindingFlags.Instance);
	}

	void Update()
	{
		if (climb != null && climb.isOnLadder)
		{
			verticalVelocity = 0;
			return;
		}

		bool isGrounded = controller.isGrounded;

		Vector3 currentThrottle = (Vector3)moveThrottleField.GetValue(ovr);

		Vector2 primaryAxis = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
		Vector3 forward = transform.forward;
		Vector3 right = transform.right;
		forward.y = 0; right.y = 0;
		forward.Normalize(); right.Normalize();
		Vector3 targetAirMove = (forward * primaryAxis.y + right * primaryAxis.x) * airSpeed;

		if (isGrounded)
		{
			horizontalMove = Vector3.zero;

			if (verticalVelocity < 0) verticalVelocity = -1f;

			// Only jump if we are NOT at the waystone
			if (!isAtWaystone && OVRInput.GetDown(OVRInput.Button.One))
			{
				verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
			}

			currentThrottle.y = verticalVelocity * Time.deltaTime;
		}
		else
		{
			horizontalMove = Vector3.Lerp(horizontalMove, targetAirMove, airDamping);
			verticalVelocity += gravity * Time.deltaTime;

			currentThrottle = horizontalMove * Time.deltaTime;
			currentThrottle.y = verticalVelocity * Time.deltaTime;
		}

		if (moveThrottleField != null)
		{
			moveThrottleField.SetValue(ovr, currentThrottle);
		}
	}
}