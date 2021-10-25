﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Events;

public class PlayerCombat : MonoBehaviour
{
	PlayerController playerController;

	[Header("Ranged")]
	public GameObject rangedWeapon;
	public ArrowData arrowData;
	public Transform arrowPosRest;
	public Transform arrowPosCharged;
	public float rangedCooldownTime = 0.5f;
	public float rangedCameraShakeMagnitude = 2;
	[Header("Aiming")]
	public float aimingSpeedPercent = 0.5f;
	[Min(0.001f)] public float zoomInSpeed = 2;
	[Min(0.001f)] public float zoomOutSpeed = 2;
	[Min(0.001f)] public float rangedChargeUpSpeed = 2;
	[Range(0,1)] public float chargedThreshold = 0.75f;
	[Min(0.001f)] public float rangedChargeDownSpeed= 4;
	[Header("Melee")]
	public GameObject meleeWeapon;
	public float meleeDamage = 10;
	public float meleeKnockback = 0;
	public Vector3 meleeSphereLocalOffset = new Vector3(0.1f, 0);
	public float meleeSphereRadius = 1;
	public float meleeMaximumAngle = 40;
	public float meleeCooldownTime = 0.5f;
	public float meleeStepSpeed = 10;
	public float meleeStepDuration = 0.1f;
	public float meleeCameraShakeMagnitude = 2;
	public LayerMask enemyMask;
	Vector3 meleeForward;
	bool gotToEndOfZoom = false;
	bool cameraChanged = false;
	public CameraData aimingCameraData;

	Arrow equippedArrow = null;
	bool charging = false;
	float chargeUpPercent = 0;
	float zoomInPercent = 0;
	float cooldownTimer = 0;

	WeaponType currentWeapon = WeaponType.NONE;
	public enum WeaponType
	{
		NONE,
		MELEE,
		RANGED
	}

	private void Start()
	{
		playerController = GetComponent<PlayerController>();
		playerController.Motor.onChangeRoll.AddListener(Dequip);
		playerController.Motor.onLeaveGround.AddListener(() => { if (playerController.Motor.State == PlayerMotor.MovementState.JUMPING) Dequip(); });
		playerController.Motor.onDash.AddListener(Dequip);
	}

	private void Update()
	{
		//camera zoom is updated here
		if (charging)
		{
			//camera zoom
			zoomInPercent = zoomInPercent + Time.deltaTime * zoomInSpeed;
			if (zoomInPercent < 1)
			{
				LerpCamera(1 - (1 - zoomInPercent) * (1 - zoomInPercent));
				cameraChanged = true;
			}
			else
			{
				if (cameraChanged)
				{
					cameraChanged = false;
					LerpCamera(1);
				}
				//gotToEndOfZoom = true;
				zoomInPercent = 1;
			}
		}
		else
		{
			//cameraZoom
			zoomInPercent -= Time.deltaTime * zoomInSpeed;

			if (zoomInPercent > 0)
			{
				if (gotToEndOfZoom)
					LerpCamera(zoomInPercent * zoomInPercent);
				else
					LerpCamera(1 - (1 - zoomInPercent) * (1 - zoomInPercent));

				cameraChanged = true;
			}
			else
			{
				if (cameraChanged)
				{
					cameraChanged = false;
					LerpCamera(0);
				}
				gotToEndOfZoom = false;
				zoomInPercent = 0;
			}
		}
	}

	private void FixedUpdate()
	{
		if (cooldownTimer >= 0 || playerController.Motor.IsDashing || playerController.Motor.IsRolling)
		{
			cooldownTimer -= Time.deltaTime;
			playerController.EvaluateAttackPressed();
			if (charging)
			{
				playerController.Motor.TargetSpeedManipulator *= aimingSpeedPercent;
				playerController.Motor.alwaysLookAway = true;
			}
			return;
		}

		//BOW CHARGE LOGIC
		if (charging)
		{
			//charge bow
			chargeUpPercent = Mathf.Min(chargeUpPercent + Time.deltaTime * rangedChargeUpSpeed, 1);
			
			//draw hit position
			if (playerController.DrawDebug)
			{
				if (Physics.Raycast(playerController.MainCamera.transform.position, playerController.MainCamera.transform.forward, out var hit, Mathf.Infinity, ~arrowData.ignoreCollisionLayers, QueryTriggerInteraction.Ignore))
				{
					Debug.DrawRay(hit.point, hit.normal, Color.red, Time.deltaTime, false);
				}
			}

			//SHOOT BOW
			if (playerController.EvaluateAttackPressed() && chargeUpPercent > chargedThreshold)
			{
				ShootBow();
			}
		}
		else
		{
			chargeUpPercent = Mathf.Max(chargeUpPercent - Time.deltaTime * rangedChargeDownSpeed, 0);
		}

		if (chargeUpPercent > 0 && !playerController.Motor.IsRolling)
		{
			//slow player down
			playerController.Motor.TargetSpeedManipulator *= aimingSpeedPercent;
			//make player turn away from camera
			playerController.Motor.alwaysLookAway = true;

			if (!equippedArrow)
			{
				equippedArrow = (Arrow)playerController.GameManager.ArrowPool.GetPooledObject(arrowPosRest);
				equippedArrow.ignoredInPool = true;
			}

			equippedArrow.transform.SetPositionAndRotation(Vector3.Lerp(arrowPosRest.position, arrowPosCharged.position, chargeUpPercent)
				,Quaternion.Slerp(arrowPosRest.rotation, arrowPosCharged.rotation, chargeUpPercent));
		}
		else
		{
			playerController.Motor.alwaysLookAway = false;
			
			//SWING SWORD
			//if (playerController.EvaluateAttackPressed())
			//{
			//	SwingSword();
			//}
		}

	}
	
	void SwingSword()
	{
		meleeForward = Vector3.ProjectOnPlane(playerController.MainCamera.transform.forward, playerController.Motor.GroundNormal).normalized;
		playerController.Motor.StartExternalDash(meleeStepSpeed, meleeStepDuration, meleeForward);
		playerController.Animator.AnimateEquip(WeaponType.MELEE);

		cooldownTimer = meleeCooldownTime;
		playerController.Animator.AnimateAttack();
		HitEnemies();
	}

	public void HitEnemies()
	{
		playerController.MainCamera.AddCameraShake(meleeCameraShakeMagnitude * meleeForward);

		Vector3 playerPosition = playerController.RotateChild.TransformPoint(playerController.CharacterController.center);
		Vector3 circlePosition = playerPosition + Quaternion.FromToRotation(Vector3.forward, meleeForward) * meleeSphereLocalOffset;

		Collider[] enemies = Physics.OverlapSphere(circlePosition, meleeSphereRadius, enemyMask);
		for (int i = 0; i < enemies.Length; i++)
		{
			var health = enemies[i].GetComponent<Health>();
			Vector3 delta = enemies[i].transform.position - playerPosition;
			if (health && Vector3.Angle(Vector3.ProjectOnPlane(meleeForward,Vector3.up), Vector3.ProjectOnPlane(delta, Vector3.up)) < meleeMaximumAngle)
			{
				health.Damage(meleeDamage);
				health.ApplyKnockback(meleeForward * meleeKnockback);
			}
		}
	}

	void ShootBow()
	{
		Transform cam = playerController.MainCamera.transform;
		//first add camera shake
		playerController.MainCamera.AddCameraShake(rangedCameraShakeMagnitude * cam.forward);
		//THIS CALCULATES THE DIRECTION TO SHOOT THAT WILL MAKE THE ARROW LAND IN THE RIGHT PLACE 
		//THE INITIAL VELOCITY WILL ALWAYS BE THE SAME
		Vector3 hitPoint;
		if (!Physics.Raycast(cam.position, cam.forward, out var hit, Mathf.Infinity, ~arrowData.ignoreCollisionLayers, QueryTriggerInteraction.Ignore))
		{
			hitPoint = cam.forward * 100 + cam.position;
		}
		else
		{
			hitPoint = hit.point;
		}

		var arrow = equippedArrow.GetComponent<Arrow>();
		float initialSpeed = arrowData.maxInitialSpeed * chargeUpPercent;
		Vector3 velocity = Vector3.zero;

		//convert 3d problem into 2d problem like this:
		//calculate x axis
		Vector3 xAxis = (hitPoint - equippedArrow.transform.position);
		xAxis.y = 0;
		xAxis.Normalize();
		//convert 3d point into 2d point
		Vector2 positionToHit = new Vector2(Vector3.Dot(hitPoint, xAxis)
			- Vector3.Dot(equippedArrow.transform.position, xAxis), hitPoint.y - equippedArrow.transform.position.y);

		//now calculate tan(0) of arrow angle and turns it into direction vector
		//https://en.wikipedia.org/wiki/Projectile_motion#Angle_%CE%B8_required_to_hit_coordinate_(x,_y)
		float v4 = initialSpeed * initialSpeed * initialSpeed * initialSpeed;
		float g2 = arrowData.gravity * arrowData.gravity;
		float possibleNeg = v4 - arrowData.gravity * (arrowData.gravity * positionToHit.x * positionToHit.x + 2 * positionToHit.y * initialSpeed * initialSpeed);

		//if distance is too far away to hit at this speed
		if (possibleNeg < 0)
		{
			//find closest valid point that gives a 0 'possibleNeg' value
			//this is technically wrong sometimes, but not when possibleNeg is less than 0
			//(it is wrong because it uses the cubic formula, which gives multiple results, but it only uses one)
			//here is a visualisation: https://www.desmos.com/calculator/olszi1qcpd

			//this should fix for floating point error by looking not for 0 neg value, but errorfix neg value
			const float errorFix = 1;
			double q = -positionToHit.x * v4 / g2;
			double p = (2 * positionToHit.y * arrowData.gravity * initialSpeed * initialSpeed + v4 + errorFix) / (3 * g2);
			double newSqrt = Math.Sqrt(q * q + p * p * p);
			//since pow cannot handle negative cube rooting we will use some jank to fix
			double newXValue = (-Math.Pow(Math.Abs(q + newSqrt), 1.0f / 3.0f) * Math.Sign(q + newSqrt) - Math.Pow(Math.Abs(q - newSqrt), 1.0f / 3.0f) * Math.Sign(q - newSqrt));
			double newYValue = ((v4 - errorFix) / arrowData.gravity - arrowData.gravity * newXValue * newXValue) / (2 * initialSpeed * initialSpeed);
			positionToHit.x = (float)newXValue;
			positionToHit.y = (float)newYValue;
			possibleNeg = v4 - arrowData.gravity * (arrowData.gravity * positionToHit.x * positionToHit.x + 2 * positionToHit.y * initialSpeed * initialSpeed);
		}
		float sqrt = Mathf.Sqrt(possibleNeg);
		//there are technically two options, but this one is always best
		float tanOfAngle = (initialSpeed * initialSpeed - sqrt) / (arrowData.gravity * positionToHit.x);
		// tanOfAngleOption2 = (initialSpeed * initialSpeed + sqrt) / (arrowData.gravity * positionToHit.x);

		Vector2 v = new Vector3(1, tanOfAngle).normalized * initialSpeed;
		velocity.y = v.y;
		velocity += xAxis * v.x;
		//Debug.Log($"x: {positionToHit.x}, y: {positionToHit.y} v: {initialSpeed}, g: {arrowData.gravity}, sq: {possibleNeg}");
		arrow.Shoot(velocity, arrowData);

		cooldownTimer = rangedCooldownTime;
		playerController.Animator.AnimateAttack();
		chargeUpPercent = 0.001f;
		equippedArrow.transform.parent = null;
		equippedArrow.ignoredInPool = false;
		equippedArrow = null;
	}

	public void StartChargeUp()
	{
		if (playerController.Motor.IsRolling || playerController.Motor.IsDashing || playerController.Motor.State != PlayerMotor.MovementState.GROUNDED) return;

		playerController.Animator.AnimateEquip(WeaponType.RANGED);
		charging = true;
		cameraChanged = true;

		if (playerController.GUI)
			playerController.GUI.EnableCrossHair(true);
	}

	public void EndChargeUp()
	{
		charging = false;
		if (playerController.GUI)
			playerController.GUI.EnableCrossHair(false);
	}

	public void EquipWeapon(WeaponType weaponType)
	{
		if (currentWeapon == weaponType) return;
		
		switch (weaponType)
		{
			case WeaponType.MELEE:
				if (equippedArrow)
				{
					playerController.GameManager.ArrowPool.ReturnPooledObject(equippedArrow);
					equippedArrow = null;
				}
				rangedWeapon.SetActive(false);
				meleeWeapon.SetActive(true);
				break;
			case WeaponType.RANGED:
				rangedWeapon.SetActive(true);
				meleeWeapon.SetActive(false);
				break;
			case WeaponType.NONE:
				if (equippedArrow)
				{
					playerController.GameManager.ArrowPool.ReturnPooledObject(equippedArrow);
					equippedArrow = null;
				}
				rangedWeapon.SetActive(false);
				meleeWeapon.SetActive(false);
				break;
		}
		currentWeapon = weaponType;
	}

	void Dequip()
	{
		playerController.Animator.AnimateEquip(WeaponType.NONE);
		chargeUpPercent = 0;
		EndChargeUp();
	}

	void LerpCamera(float t)
	{
		CameraData data = playerController.MainCamera.GetCameraData();
		CameraData aData = playerController.MainCamera.defaultCameraData;

		data.followSpeed = Mathf.Lerp(aData.followSpeed, aimingCameraData.followSpeed, t);
		data.maxFollowDistance = Mathf.Lerp(aData.maxFollowDistance, aimingCameraData.maxFollowDistance, t);
		data.maximumUpRotation = Mathf.Lerp(aData.maximumUpRotation, aimingCameraData.maximumUpRotation, t);
		data.minimumUpRotation = Mathf.Lerp(aData.minimumUpRotation, aimingCameraData.minimumUpRotation, t);
		data.rotateSpeed = Mathf.Lerp(aData.rotateSpeed, aimingCameraData.rotateSpeed, t);
		data.yOffsetChangeSpeed = Mathf.Lerp(aData.yOffsetChangeSpeed, aimingCameraData.yOffsetChangeSpeed, t);
		data.yOffsetDistance = Mathf.Lerp(aData.zoomOutSpeed, aimingCameraData.zoomOutSpeed, t);
		data.yOffsetMagnitude =	Mathf.Lerp(aData.yOffsetMagnitude, aimingCameraData.yOffsetMagnitude, t);
		data.yOffsetStartDistance = Mathf.Lerp(aData.yOffsetStartDistance, aimingCameraData.yOffsetStartDistance, t);
		data.zoomOutSpeed =	Mathf.Lerp(aData.zoomOutSpeed, aimingCameraData.zoomOutSpeed, t);
		if (playerController.Motor.IsRolling)
		{
			data.targetOffset =	Vector3.Lerp(new Vector3(aData.targetOffset.x, playerController.RollCameraOffset, aData.targetOffset.z), aimingCameraData.targetOffset, t);
		}
		else
			data.targetOffset =	Vector3.Lerp(aData.targetOffset, aimingCameraData.targetOffset, t);
		playerController.MainCamera.InputMove(Vector2.zero);
	}

	public float ChargePercentage { get { return Mathf.Clamp01(chargeUpPercent); } }
}
