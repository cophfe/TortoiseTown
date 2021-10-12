﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DefaultExecutionOrder(10)]
[RequireComponent(typeof(CharacterController), typeof(PlayerController))]
//Controls all player movement
public class PlayerMotor : MonoBehaviour
{
	#region Exposed Variables
	//FORCES
	[Header("Forces")]
	[Tooltip("The acceleration applied to the player's input.")]
	public float acceleration = 10;
	[Tooltip("The target speed of a player's input")]
	public float targetSpeed = 10;
	[Tooltip("The vertical gravity amount.")]
	public float gravity = 10;
	[Tooltip("The maximum velocity overall.")]
	public float maxVelocity = 1000;

	//FRICTION
	[Tooltip("The magnitude of velocity removed every second when in the air")]
	public float airFriction = 0.5f;
	[Tooltip("The magnitude of velocity removed every second when grounded")]
	public float groundFriction = 0.5f;

	//JUMPING
	[Header("Jumping")]
	[Tooltip("The amount of upward velocity applied when jumping")]
	[Min(0)]
	public float jumpSpeed = 4;
	[Tooltip("The maximum length of time the jumpspeed is applied")]
	[Min(0)]
	public float jumpDuration = 0.3f;
	[Tooltip("The amount of jumps the player can do in the air")]
	[Min(0)]
	public float airJumps = 0;
	[Tooltip("If the player tries to jump before they have touched the ground, it will be processed if less than this amount of time has past")]
	[Min(0)]
	public float jumpBufferTime = 0.1f;
	[Tooltip("If the player tries to jump after they have left the ground, it will be processed if less than this amount of time has past")]
	[Min(0)]
	public float jumpCoyoteTime = 0.1f;

	[Header("Dash")]
	public float dashSpeed = 4;
	public float dashAcceleration = 100;
	public float dashDuration = 0.2f;
	public float dashCooldown = 2;
	public float dashTurnSpeed = 40;

	//COLLISION
	[Header("Collision Response")]
	[Tooltip("The minimum velocity into a collision before the input velocity is modified")]
	public float minCollisionVelocity = 0.1f;
	[Tooltip("The mass used for rigidbody collision")]
	public float mass = 1;
	[Tooltip("The restitution value used for rigidbody collision")]
	[Range(0,1)] public float bounciness = 0;
	[Tooltip("The layers that do not affect input velocity when colliding")]
	public LayerMask ignoredCollision;

	//CONTROL
	[Tooltip("The percent of acceleration applied to input while in the air")]
	[Range(0, 1)] public float airControlModifier = 0.5f;

	//GROUND DETECTION 
	[Header("Ground Detection")]
	[Tooltip("The percentage of the character controller's radius that will be used for ground check spherecasts")]
	[Range(0, 1)] public float groundDetectionRadiusModifier = 0.8f;
	[Tooltip("The amount that the ground detection sphere is offset from the bottom of the character controller capsule")]
	[Range(0, 0.1f)] public float groundDetectionAdditionalOffset = 0.01f;
	[Tooltip("Affects the distance that will be added to the ground detection distance when the ground magnet is enabled")]
	[Range(0, 1)] public float groundMagnetDistanceModifier = 0.5f;
	[Tooltip("The layers that aren't checked against when raycasting")]
	public LayerMask ignoredGround;

	//ROTATION
	[Header("Rotation")]
	[Tooltip("Whether the player is locked looking away from the camera or looks toward input")]
	public bool alwaysLookAway = false;
	[Tooltip("The speed of rotation")]
	public float turnSpeed = 2;
	[Tooltip("The speed of rotation when in air")]
	public float airTurnSpeed = 2;
	[Tooltip("The minimum movement before the player rotates")]
	[Min(0)]
	public float minPlayerRotation = 0.3f;
	[Tooltip("The speed of roll rotation")]
	[Min(0)]
	public float fixRotationSpeed = 1000;
	[Range(0,45)]
	public float bodyMaxGroundAlignAngle = 10;
	public float walkAlignSpeed = 7;

	//ROLLING
	//radius of rollCollider
	[Header("Rolling")]
	[Tooltip("Percentage of collider radius used for rotation")]
	public float rollRadiusModifier = 0.5f;
	[Tooltip("Percentage of target speed used when rolling")]
	public float rollTargetSpeedModifier = 1;
	[Tooltip("Percentage of acceleration used when rolling")]
	public float rollAccelerationModifier = 1;
	[Tooltip("The speed of turning while rolling")]
	public float rollTurnSpeed = 2;
	[Tooltip("The speed at which the player rotation aligns to the movement direction while rolling"), Min(0)]
	public float ballAlignSpeed = 1;
	[Tooltip("The cooldown time after rolling before the player can switch out of roll")]
	public float rollCooldownTime = 0;
	[Min(0)]
	[Tooltip("The amount of upwards force applied to the player when leaving roll")]
	public float leaveRollHopSpeed = 0;
	[Tooltip("The amount of friction applied per second while rolling (proportional to total velocity)")]
	[Range(0, 3)] public float rollFriction = 0.5f;
	[Tooltip("The speed at which the ball no longer attaches to the floor")]
	public float ignoreGroundMagnetSpeed = 9;

	//EVENTS
	[Header("Events")]
	[Tooltip("The event called when leaving the grounded state")]
	public UnityEvent onLeaveGround;
	[Tooltip("The event called when entering the grounded state")]
	public UnityEvent onEnterGround;
	[Tooltip("The event called when entering or leaving a roll")]
	public UnityEvent onChangeRoll;

	//OTHER
	[Tooltip("Whether or not to draw additional debug information")]
	public bool drawDebug = false;
	#endregion

	#region Private Variables
	//~~~~~~~~~PRIVATE~~~~~~~~~
	//REFERENCES
	PlayerController playerController;
	//VELOCITY
	Vector3 totalVelocity;
	Vector3 inputVelocity;
	Vector3 lastNonZeroInputVelocity;
	Vector3 targetVelocity;
	Vector3 forcesVelocity;
	//JUMP
	float jumpBufferTimer = 0;
	float jumpCoyoteTimer = 0;
	float jumpTimer = 0;
	float airJumpsLeft = 0;
	//dash
	float dashCooldownTimer = 0;
	float dashTimer = 0;
	bool dashing = false;
	bool dashedInThisJump = false;
	//GROUND DETECTION
	bool isGrounded = false;
	Vector3 groundPosition;
	Vector3 groundNormal;
	float groundDistance;
	Collider groundCollider;
	bool enableGroundMagnet = false;
	float groundMagnetOffset;
	bool collisionGroundDetected = false;
	//ROTATION
	Quaternion targetRotation;
	//ROLLING
	bool isRolling = false;
	float rollCooldownTimer = 0;
	//MOVING PLATFORM
	MovingPlatform movingPlatform = null;
	Vector3 movingPlatformOffset = Vector3.zero;
	//OTHER
	MovementState state = MovementState.FALLING;
	Vector3 inputForward;
	#endregion

	public enum MovementState
	{
		GROUNDED,
		FALLING,
		RISING,
		JUMPING //jumping is seperate from rising for variable jump height reasons
	}

	private void Start()
	{
		playerController = GetComponent<PlayerController>();
	}

	private void Update()
	{
		UpdateRotation();

		if (drawDebug && isGrounded)
		{
			Debug.DrawRay(groundPosition, groundNormal, Color.blue, Time.deltaTime);
		}
	}

	private void FixedUpdate()
	{
		Run();
	}

	void Run()
	{
		if (movingPlatform)
			movingPlatformOffset = movingPlatform.GetOffset();
		else
			movingPlatformOffset = Vector3.zero;

		ScanForGround();
		SetState();
		EvaluateJump();
		EvaluateRoll();
		EvaluateDash();
		UpdateMovementVector();
		UpdateForcesVector();

		enableGroundMagnet = state == MovementState.GROUNDED || collisionGroundDetected;

		totalVelocity = inputVelocity + forcesVelocity;
		
		//move player
		playerController.CharacterController.Move(totalVelocity * Time.deltaTime 
			+ groundMagnetOffset * Vector3.up + movingPlatformOffset);

		//Set all timers
		jumpTimer -= Time.deltaTime;
		jumpCoyoteTimer -= Time.deltaTime;
		jumpBufferTimer -= Time.deltaTime;
		rollCooldownTimer -= Time.deltaTime;
		dashCooldownTimer -= Time.deltaTime;
		dashTimer -= Time.deltaTime;
	}

	void UpdateMovementVector()
	{
		if (inputVelocity != Vector3.zero)
			lastNonZeroInputVelocity = inputVelocity;

		if (dashing)
		{
			if (playerController.inputVector == Vector2.zero)
			{
				inputForward = Vector3.ProjectOnPlane(playerController.MainCamera.transform.forward, Vector3.up).normalized;
				if (groundNormal.y != 0)
				{
					inputForward += Vector3.up * -(groundNormal.x * inputForward.x + groundNormal.z * inputForward.z) / groundNormal.y;
					inputForward.Normalize();
				}
				targetVelocity = inputForward * dashSpeed;
			}
			else
			{
				targetVelocity = GetTargetDirection().normalized * dashSpeed;
			}
			inputVelocity = Vector3.MoveTowards(inputVelocity, targetVelocity, dashAcceleration * Time.deltaTime);

			if (dashTimer <= 0)
			{
				dashing = false;
				dashedInThisJump = false;
			}
		}
		else if (isRolling)
		{
			if (state == MovementState.GROUNDED)
			{
				targetVelocity = GetTargetDirection() * rollTargetSpeedModifier;
				if (playerController.inputVector != Vector2.zero)
				{
					inputVelocity += Vector3.ClampMagnitude(targetVelocity, acceleration * rollAccelerationModifier * Time.deltaTime);
				}
				//add friction
				inputVelocity = inputVelocity + -Mathf.Clamp(Time.deltaTime * rollFriction, 0, 1) * inputVelocity;//Vector3.MoveTowards(Vector3.ProjectOnPlane(forcesVelocity, Vector3.up), Vector3.zero, groundFriction * Time.deltaTime);
				//remove vertical force to prevent unwanted accumulation
				float verticalForce = Vector3.Dot(groundNormal, inputVelocity);
				inputVelocity = Vector3.ProjectOnPlane(inputVelocity, groundNormal) + groundNormal * Mathf.MoveTowards(verticalForce, 0, acceleration * Time.deltaTime);
				//clamp to target speed
				inputVelocity = Vector3.ClampMagnitude(inputVelocity, targetSpeed * rollTargetSpeedModifier);
				//LINEAR FRICTION
				//inputVelocity -= Vector3.MoveTowards(Vector3.ProjectOnPlane(inputVelocity, groundNormal), Vector3.zero, groundFriction * Time.deltaTime *0.0000001f);

				//do not magnet to ground if going fast enough
				if (Vector3.Dot(groundMagnetOffset * Vector3.up, groundNormal) < 0 && Vector3.ProjectOnPlane(inputVelocity, groundNormal).sqrMagnitude > ignoreGroundMagnetSpeed * ignoreGroundMagnetSpeed)
				{
					groundMagnetOffset = 0;
					state = totalVelocity.y > 0 ? MovementState.RISING : MovementState.FALLING;
				}
			}
			else
			{
				targetVelocity = Vector3.zero;
				//FRICTION
				inputVelocity = Vector3.MoveTowards(inputVelocity, Vector3.zero, airFriction * Time.deltaTime);
			}
		}
		else
		{
			if (state == MovementState.GROUNDED)
			{
				targetVelocity = GetTargetDirection() * targetSpeed;

				float currentAcceleration = acceleration;

				//make it turn around fast
				if (Vector3.Dot(targetVelocity, inputVelocity) < 0)
				{
					currentAcceleration *= 2;
				}
				inputVelocity = Vector3.MoveTowards(inputVelocity, targetVelocity, currentAcceleration * Time.deltaTime);
			}
			else
			{
				if (playerController.inputVector != Vector2.zero)
				{
					targetVelocity = GetTargetDirection() * targetSpeed;

					inputVelocity = Vector3.MoveTowards(inputVelocity, targetVelocity, airControlModifier * acceleration * Time.deltaTime * (isRolling ? rollTargetSpeedModifier : 1));
				}

				//FRICTION
				inputVelocity = Vector3.MoveTowards(inputVelocity, Vector3.zero, airFriction * Time.deltaTime);
			}
		}

		Vector3 GetTargetDirection()
		{
			inputForward = Vector3.ProjectOnPlane(playerController.MainCamera.transform.forward, Vector3.up).normalized;
			if (groundNormal.y != 0)
			{
				inputForward += Vector3.up * -(groundNormal.x * inputForward.x + groundNormal.z * inputForward.z)/groundNormal.y;
				inputForward.Normalize();
			}

			Vector3 targetVelocity = Vector3.Cross(inputForward, groundNormal) * -playerController.inputVector.x
				+ inputForward * playerController.inputVector.y;
			targetVelocity = Vector3.ClampMagnitude(targetVelocity, 1);

			if (drawDebug)
			{
				Debug.DrawRay(groundPosition, Vector3.Cross(inputForward, groundNormal), Color.red, Time.deltaTime, false);
				Debug.DrawRay(groundPosition, inputForward, Color.red, Time.deltaTime, false);
			}
			
			return targetVelocity;
		}
	}

	void UpdateForcesVector()
	{
		Vector3 planeVelocity = Vector3.ProjectOnPlane(forcesVelocity, Vector3.up);

		if (dashing)
		{
			forcesVelocity = Vector3.zero;
			return;
		}
		if (state == MovementState.GROUNDED)
		{
			//GRAVITY
			if (Vector3.Dot(forcesVelocity, Vector3.up) < 0)
			{
				forcesVelocity = planeVelocity;
			}

			//FRICTION
			planeVelocity = Vector3.MoveTowards(planeVelocity, Vector3.zero, groundFriction * Time.deltaTime);
		}
		else
		{
			//GRAVITY
			forcesVelocity -= Vector3.up * (gravity * Time.deltaTime);

			//FRICTION
			planeVelocity = Vector3.MoveTowards(planeVelocity, Vector3.zero, airFriction * Time.deltaTime);
		}

		//apply friction
		float up = Vector3.Dot(forcesVelocity, Vector3.up);
		forcesVelocity = planeVelocity + up * Vector3.up;

		//clamp magnitude
		Vector3.ClampMagnitude(forcesVelocity, maxVelocity);
	}

	public void UpdateRotation()
	{
		float turnSpeed;

		
		if (isRolling)
		{
			float distance = totalVelocity.magnitude * Time.deltaTime;
			if (distance < 0.001f)
				return;
			
			float angle = distance * Mathf.Rad2Deg / (rollRadiusModifier * (playerController.RollColliderRadius + playerController.CharacterController.skinWidth));
			Vector3 axis = Vector3.Cross(groundNormal, totalVelocity).normalized;
			targetRotation = Quaternion.Euler(axis * angle) * playerController.RotateChild.localRotation;

			if (ballAlignSpeed > 0)
			{
				//align to input velocity direction
				Quaternion aligned = Quaternion.FromToRotation(playerController.RotateChild.right, axis) * targetRotation;
				float alignedAngle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(playerController.RotateChild.up, axis), -1f, 1f)) * Mathf.Rad2Deg;
				float frameMaxAlignedAngle = ballAlignSpeed * distance * Time.deltaTime;
				if (alignedAngle <= frameMaxAlignedAngle)
					targetRotation = aligned;
				else
					targetRotation = Quaternion.SlerpUnclamped(targetRotation, aligned, frameMaxAlignedAngle / angle);
			}

			playerController.RotateChild.localRotation = targetRotation;// Quaternion.RotateTowards(playerController.RotateChild.rotation, targetRotation, Quaternion.Angle(playerController.RotateChild.rotation, targetRotation) * fixRotationSpeed * Time.deltaTime);
			
			Transform modelTransform = playerController.Animator.transform;
			modelTransform.localRotation = Quaternion.RotateTowards(modelTransform.localRotation, Quaternion.identity,
				Quaternion.Angle(modelTransform.localRotation, Quaternion.identity) * walkAlignSpeed * Time.deltaTime);
		}
		else
		{
			Vector3 rotVec;

			if (dashing)
			{
				rotVec = Vector3.ProjectOnPlane(targetVelocity, Vector3.up);
				turnSpeed = dashTurnSpeed;
			}
			else if (state == MovementState.GROUNDED)
			{
				if (targetVelocity != Vector3.zero)
					rotVec = Vector3.ProjectOnPlane(targetVelocity, Vector3.up);
				else
					rotVec = Vector3.ProjectOnPlane(playerController.RotateChild.forward, Vector3.up);
				turnSpeed = this.turnSpeed;
				
			}
			else
			{
				rotVec = Vector3.ProjectOnPlane(totalVelocity, Vector3.up);
				turnSpeed = airTurnSpeed;
			}
			if (alwaysLookAway)
			{
				rotVec = Vector3.ProjectOnPlane(playerController.MainCamera.transform.forward, Vector3.up);
			}

			if (rotVec.magnitude >= minPlayerRotation)
			{
				targetRotation = Quaternion.LookRotation(rotVec, Vector3.up);
				playerController.RotateChild.localRotation = Quaternion.RotateTowards(playerController.RotateChild.localRotation, targetRotation,
				   Quaternion.Angle(playerController.RotateChild.localRotation, targetRotation) * Time.deltaTime * turnSpeed);
			}
			else
			{
				playerController.RotateChild.localRotation = Quaternion.RotateTowards(playerController.RotateChild.localRotation, targetRotation,
				   Quaternion.Angle(playerController.RotateChild.localRotation, targetRotation) * Time.deltaTime * turnSpeed);
			}

			Transform modelTransform = playerController.Animator.transform;
			Vector3 clampedGroundNormal = Vector3.RotateTowards(Vector3.up, groundNormal, bodyMaxGroundAlignAngle * Mathf.Deg2Rad, 1);
			var target = Quaternion.FromToRotation(Vector3.up, Quaternion.Inverse(playerController.RotateChild.rotation) * clampedGroundNormal);
			modelTransform.localRotation = Quaternion.RotateTowards(modelTransform.localRotation, target,
				Quaternion.Angle(modelTransform.localRotation, target) *  walkAlignSpeed * Time.deltaTime);
		}
	}

	void SetState()
	{
		bool movingUp = Vector3.Dot(Vector3.up, forcesVelocity) > 0.0001f;
		bool groundTooSteep = Vector3.Angle(groundNormal, Vector3.up) > playerController.CharacterController.slopeLimit;

		switch (state)
		{
			case MovementState.GROUNDED:
				if (movingUp)
				{
					state = MovementState.RISING;
					OnLeaveGround();
				}
				else if (!isGrounded)
				{
					state = MovementState.FALLING;
					OnLeaveGround();
				}
				else if (groundTooSteep)
				{
					state = MovementState.FALLING;
					OnLeaveGround();
				}
				break;

			case MovementState.RISING:
				if (!movingUp)
				{
					if (isGrounded)
					{
						if (groundTooSteep)
						{
							state = MovementState.FALLING;
						}
						else
						{
							state = MovementState.GROUNDED;
							OnLand();
						}
					}
					else
					{
						state = MovementState.FALLING;
					}
				}
				break;

			case MovementState.FALLING:
				if (movingUp)
				{
					state = MovementState.RISING;
				}
				else if (isGrounded)
				{
					if (groundTooSteep)
					{
						state = MovementState.FALLING;
					}
					else
					{
						state = MovementState.GROUNDED;
						OnLand();
					}
				}
				break;
		}
	}

	void ScanForGround()
	{
		Vector3 direction = -Vector3.up;
		Vector3 origin = transform.TransformPoint(playerController.CharacterController.center);
		float length = playerController.CharacterController.height / 2 
			+ playerController.CharacterController.skinWidth;

		if (enableGroundMagnet)
		{
			length *= (1+ groundMagnetDistanceModifier);
		}
		else
		{
			groundMagnetOffset = 0;
		}
		float radius = playerController.CharacterController.radius * groundDetectionRadiusModifier;
		isGrounded = Physics.SphereCast(origin, radius, direction, out var hit,
			length * (1 + groundDetectionAdditionalOffset) - radius, ~ignoredGround);

		if (isGrounded)
		{
			groundPosition = hit.point;
			groundDistance = Mathf.Abs(Vector3.Dot(groundPosition - origin, direction));
			groundNormal = hit.normal;
			if (hit.collider != groundCollider)
				OnChangedCollider(hit.collider);
			groundCollider = hit.collider;
			//if we need surface normal instead of collision normal:
			//if (hit.collider.Raycast(new Ray(hit.point - direction, direction), out hit, 10) && Vector3.Angle(hit.normal, -direction) < 89.5f)
			//{
			//	groundNormal = hit.normal;
			//}

			float offset = (playerController.CharacterController.height / 2 + playerController.CharacterController.skinWidth - groundDistance );
			if (enableGroundMagnet && (offset < 0 || movingPlatformOffset != Vector3.zero))
			{
				groundMagnetOffset = offset;
			}
		}
		else
		{
			if (groundCollider != null)
				OnChangedCollider(null);

			groundNormal = Vector3.up;
			groundCollider = null;
		}
	}

	void EvaluateJump()
	{
		//start jump
		if (playerController.EvaluateJumpPressed())
		{
			//if rolling
			if (isRolling)
			{
				if (rollCooldownTimer <= 0)
				{
					OnLeaveRoll(true);
				}
				else
					return;
			}

			if (state == MovementState.GROUNDED)
			{
				OnJump();
				OnLeaveGround();
			}
			else
			{
				if (airJumpsLeft > 0)
				{
					airJumpsLeft--;
					OnJump();
				}
				else if (jumpCoyoteTimer > 0)
				{
					jumpCoyoteTimer = 0;
					OnJump();
				}
				else
				{
					playerController.EvaluateJumpCancelled();
					jumpBufferTimer = jumpBufferTime;
				}
			}
		}

		if (state == MovementState.JUMPING)
		{
			//add jump force
			forcesVelocity = Vector3.ProjectOnPlane(forcesVelocity, Vector3.up);
			forcesVelocity += Vector3.up * jumpSpeed;

			//check jump cancel
			if (playerController.EvaluateJumpCancelled() || jumpTimer <= 0)
			{
				state = MovementState.RISING;
			}
		}
		else if (state == MovementState.GROUNDED)
		{
			playerController.EvaluateJumpCancelled();
		}
	}

	void EvaluateDash()
	{
		if (playerController.EvaluateSprintPressed() && !isRolling && dashCooldownTimer <= 0 && !dashing)
		{
			OnStartDash();
		}
	}

	void EvaluateRoll()
	{
		//start roll
		if (playerController.EvaluateCrouchPressed() && rollCooldownTimer <= 0 && state == MovementState.GROUNDED)
		{
			if (isRolling)
			{
				OnLeaveRoll();	
			}
			else
			{
				OnRoll();
			}
		}
	}

	void OnRoll()
	{
		rollCooldownTimer = rollCooldownTime;
		isRolling = true;
		onChangeRoll.Invoke();
		targetRotation = Quaternion.identity;
	}

	void OnLeaveRoll(bool fromJump = false)
	{
		rollCooldownTimer = rollCooldownTime;
		isRolling = false;
		onChangeRoll.Invoke();
		if (leaveRollHopSpeed > 0 && !fromJump && state == MovementState.GROUNDED)
		{
			state = MovementState.RISING;
			forcesVelocity += Vector3.up * leaveRollHopSpeed;
			OnLeaveGround();
		}
		targetRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(lastNonZeroInputVelocity, Vector3.up), Vector3.up);
	}

	void OnJump()
	{
		//set state
		state = MovementState.JUMPING;
		groundMagnetOffset = 0;
		//set the jump timer
		jumpTimer = jumpDuration;
		//add force
		forcesVelocity = Vector3.ProjectOnPlane(forcesVelocity, Vector3.up);
		forcesVelocity += Vector3.up * jumpSpeed;
		//remove inputvelocity going upward
		float upVel = Vector3.Dot(Vector3.up, inputVelocity);
		if (upVel > 0)
		{
			inputVelocity -= Vector3.up * upVel;
		}
		if (playerController.EvaluateJumpCancelled())
			state = MovementState.RISING;
	}

	void OnStartDash()
	{
		if (state != MovementState.GROUNDED)
		{
			if (dashedInThisJump) return;
			dashedInThisJump = true;
			state = MovementState.RISING;
		}
		dashing = true;
		dashCooldownTimer = dashCooldown;
		dashTimer = dashDuration;
	}

	void OnLand()
	{
		//call land event
		onEnterGround.Invoke();
		//refresh air jumps
		airJumpsLeft = airJumps;
		dashedInThisJump = false;

		if (isRolling)
		{
			inputVelocity += Vector3.ProjectOnPlane(forcesVelocity, Vector3.up);
			forcesVelocity = Vector3.zero;
		}
		//if there is a jump buffered
		if (jumpBufferTimer > 0)
		{
			jumpBufferTimer = 0;
			OnJump();
			OnLeaveGround();
			//cancel jump immediatly if player let go of button
			if (playerController.EvaluateJumpCancelled())
			{
				state = MovementState.RISING;
			}
		}
	}

	void OnLeaveGround()
	{
		onLeaveGround.Invoke();
		jumpCoyoteTimer = jumpCoyoteTime;
		collisionGroundDetected = false;
		groundMagnetOffset = 0;
		OnChangedCollider(null);
		groundCollider = null;
	}

	void OnChangedCollider(Collider newCollider)
	{
		if (newCollider == null)
		{
			//leave the platform if no new collider && was on platform
			if (movingPlatform != null)
			{
				//calculate velocity gained from leaving
				float time = Time.timeScale == 0 ? Mathf.Infinity : Time.deltaTime;
				Vector3 vel = movingPlatformOffset / time;
				forcesVelocity += new Vector3(vel.x, Mathf.Max(vel.y, 0) , vel.z);

				//disconnect from platform
				movingPlatformOffset = Vector3.zero;
				movingPlatform = null;
				playerController.InterpolateVisuals = true;
			}
		}
		else
		{
			//connect to moving platform if there is a moving platform component
			var mp = newCollider.GetComponent<MovingPlatform>();
			if (mp != null)
			{
				//connect
				movingPlatform = mp;
				MovingPlatformOffset = movingPlatform.GetOffset();
				playerController.InterpolateVisuals = false;
			}
			else
			{
				//Disconnect from platform if new collider has no moving platform component
				if (movingPlatform)
				{
					//calculate velocity gained from leaving
					float time = Time.timeScale == 0 ? Mathf.Infinity : Time.deltaTime;
					Vector3 vel = movingPlatformOffset / time;
					forcesVelocity += new Vector3(vel.x, 0, vel.z);

					//disconnect from platform
					movingPlatformOffset = Vector3.zero;
					movingPlatform = null;
					playerController.InterpolateVisuals = true;
				}
			}
		}
	}

	private void OnControllerColliderHit(ControllerColliderHit hit)
	{
		float angle = Vector3.Angle(hit.normal, Vector3.up);
		Rigidbody rb = hit.rigidbody;

		if ((hit.gameObject.layer & ignoredCollision) == 0)
		{
			if (rb && !rb.isKinematic)
			{
				Vector3 rv = inputVelocity - rb.GetPointVelocity(hit.point);
				float projRV = Vector3.Dot(rv, hit.normal);
				if (projRV < 0)
				{
					float impulseMag = ((1 + bounciness) * projRV) / (1 / mass + 1 / rb.mass);

					inputVelocity -= impulseMag * hit.normal / mass;
					rb.AddForceAtPosition(impulseMag * hit.normal, hit.point, ForceMode.Impulse);
				}

				if (drawDebug)
				{
					Debug.DrawRay(hit.point, hit.normal, Color.red, 1);
				}
			}
			else
			{
				//cancel input velocity going into collision
				float inputHitAmount = Vector3.Dot(hit.normal, inputVelocity);
				if ((hit.gameObject.layer & ignoredCollision) == 0 && inputHitAmount < -minCollisionVelocity)
				{
					inputVelocity -= ((1 - minCollisionVelocity) * inputHitAmount) * hit.normal;


					if (drawDebug)
					{
						Debug.DrawRay(hit.point, hit.normal, Color.red, 1);
					}
				}
			}
		}

		if (state == MovementState.FALLING)
		{
			float forcesHitAmount = Vector3.Dot(hit.normal, forcesVelocity);
			if (forcesHitAmount < 0)
			{
				forcesVelocity -= forcesHitAmount * hit.normal;
			}
		}
		//check if collision is on bottom part of sphere and is under the controller slope limit (and therefore can be detected as onground)
		//if it is, the groundmagnet will be enabled, leading to better ground detection
		//this is disabled while jumping
		collisionGroundDetected = state != MovementState.JUMPING 
			&& angle < playerController.CharacterController.slopeLimit 
			&& hit.point.y - transform.position.y < playerController.CharacterController.radius;
	}

	#region Properties
	public MovementState State { get { return state; } }
	public Vector3 TotalVelocity { get { return totalVelocity; } }
	public Vector3 TargetVelocity { get { return targetVelocity; } }
	public Vector3 GroundNormal { get { return groundNormal; } }
	public Vector3 MovingPlatformOffset { get { return movingPlatformOffset; } set { movingPlatformOffset = value; } }
	public bool IsRolling { get { return isRolling; } }
	#endregion

	private void OnDrawGizmosSelected()
	{
		if (drawDebug && Application.isPlaying)
		{
			float radius = playerController.CharacterController.radius * groundDetectionRadiusModifier;

			float length = playerController.CharacterController.height / 2 + playerController.CharacterController.skinWidth;
			if (enableGroundMagnet)
				length *= (1+ groundMagnetDistanceModifier);

			Gizmos.color = Color.magenta;
			Gizmos.DrawWireSphere(transform.TransformPoint(playerController.CharacterController.center) 
				+ (length * (1 + groundDetectionAdditionalOffset) - radius) * -Vector3.up, radius);
		}
	}
}
