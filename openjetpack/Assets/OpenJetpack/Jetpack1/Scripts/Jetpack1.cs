/*
	The following license supersedes all notices in the source code.

	Copyright (c) 2019 Kurt Dekker/PLBM Games All rights reserved.

	http://www.twitter.com/kurtdekker

	Redistribution and use in source and binary forms, with or without
	modification, are permitted provided that the following conditions are
	met:

	Redistributions of source code must retain the above copyright notice,
	this list of conditions and the following disclaimer.

	Redistributions in binary form must reproduce the above copyright
	notice, this list of conditions and the following disclaimer in the
	documentation and/or other materials provided with the distribution.

	Neither the name of the Kurt Dekker/PLBM Games nor the names of its
	contributors may be used to endorse or promote products derived from
	this software without specific prior written permission.

	THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS
	IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
	TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
	PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
	HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
	SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED
	TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
	PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
	LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
	NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
	SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Jetpack1 : MonoBehaviour
{
	Jetpack1Configuration config;

	Rigidbody rb;

	Transform pivot;

	bool EngineEnabled;

	float heading;

	public static Jetpack1 Create( Camera cam, Transform spawnPoint, Jetpack1Configuration config)
	{
		Jetpack1 jp1 = new GameObject( "Jetpack1.Create();").AddComponent<Jetpack1>();

		jp1.transform.position = spawnPoint.position;
		jp1.transform.rotation = spawnPoint.rotation;
		jp1.config = config;

		// we'll keep this Rigidbody completely upright (no rotation except in Y axis),
		// but we'll have a separate gimbal for the camera
		Rigidbody rb = jp1.gameObject.AddComponent<Rigidbody>();
		rb.constraints = RigidbodyConstraints.FreezeRotationX & RigidbodyConstraints.FreezeRotationZ;

		CapsuleCollider cc = jp1.gameObject.AddComponent<CapsuleCollider>();
		cc.radius = 0.5f;
		cc.height = 2.0f;

		// this will do the tilting in this mode
		Transform pivot = new GameObject( "pivot").transform;
		pivot.transform.SetParent( jp1.transform);
		pivot.transform.localPosition = Vector3.up * 1.5f;
		pivot.transform.localRotation = Quaternion.identity;

		// gang the camera to us
		cam.transform.SetParent( pivot);
		cam.transform.localPosition = Vector3.zero;
		cam.transform.localRotation = Quaternion.identity;

		// finally save the bits we might need later
		jp1.rb = rb;
		jp1.pivot = pivot;

		jp1.heading = jp1.transform.eulerAngles.y;

		jp1.EngineEnabled = false;

		return jp1;
	}

	// raw inputs
	Vector2 CyclicInput;					// left/right fore/aft
	Vector2 CollectiveYawInput;				// up/down (power) and yaw
	bool EngineToggleInput;					// toggle engine on/off
	bool PreviousEngineToggleInput;			// for edge detect

	// time-filtered inputs
	const float CyclicSnappiness = 5.0f;
	Vector2 FilteredCyclicInput;
	const float CollectiveSnappiness = 10.0f;
	const float YawSnappiness = 5.0f;
	Vector2 FilteredCollectiveYawInput;

	void UpdateGatherUnityInput()
	{
		CyclicInput += new Vector2( Input.GetAxis( "Horizontal"), Input.GetAxis( "Vertical"));

		// I won't try to guess your Input axes for a second analog stick.
		// You can map your own input axes so you have two analog sticks and uncomment this...
//		CollectiveYawInput += new Vector2( Input.GetAxis( "Horizontal2"), Input.GetAxis( "Vertical2"));

		// Super-cheap and cheerful mapping of IJKL for collective and yaw
		if (Input.GetKey( KeyCode.I)) CollectiveYawInput.y = 1;
		if (Input.GetKey( KeyCode.K)) CollectiveYawInput.y = -1;
		if (Input.GetKey( KeyCode.J)) CollectiveYawInput.x = -1;
		if (Input.GetKey( KeyCode.L)) CollectiveYawInput.x = 1;

		// toggle engine on/off
		EngineToggleInput = Mathf.Abs( Input.GetAxis( "Fire1")) > 0.5f;

		if (Input.GetKey( KeyCode.Space)) EngineToggleInput = true;
	}

	// applies simple lerpy lowpass filtering to control inputs
	void UpdateApplyTimeFiltering()
	{
		FilteredCyclicInput = Vector2.Lerp( FilteredCyclicInput, CyclicInput, CyclicSnappiness * Time.deltaTime);

		FilteredCollectiveYawInput.x = Mathf.Lerp( FilteredCollectiveYawInput.x, CollectiveYawInput.x, YawSnappiness * Time.deltaTime);
		FilteredCollectiveYawInput.y = Mathf.Lerp( FilteredCollectiveYawInput.y, CollectiveYawInput.y, CollectiveSnappiness * Time.deltaTime);
	}

	void Update()
	{
		CyclicInput = Vector2.zero;
		CollectiveYawInput = Vector2.zero;
		PreviousEngineToggleInput = EngineToggleInput;
		EngineToggleInput = false;

		UpdateGatherUnityInput();

		// TODO: add mobile touch screen input here

		UpdateApplyTimeFiltering();

		ProcessEngineOnOffInput();

		ProcessRotationalInput();
	}

	// you can get creative here and put your own non-linear functions in,
	// but I'm going to stick with simple: -1 to 0 to +1 maps to 0 to 1
	float CollectiveInputMappingFunction( float x)
	{
		if (x < -1) x = -1;
		if (x >  1) x =  1;

		x += 1;
		x /= 2;

		return x;
	}

	// this assumes 0 to 1 input and just tweens from minThrust to maxThrust
	float CollectiveToPowerMapping( float x)
	{
		return Mathf.Lerp( config.MinimumThrust, config.MaximumThrust, x);
	}

	void ProcessEngineOnOffInput()
	{
		if (EngineToggleInput && !PreviousEngineToggleInput)
		{
			EngineEnabled = !EngineEnabled;
		}
	}

	void ProcessRotationalInput()
	{
		heading += FilteredCollectiveYawInput.x * config.MaximumYawRate * Time.deltaTime;

		float xtilt = FilteredCyclicInput.y * config.MaximumLeanAngle;
		float ztilt = -FilteredCyclicInput.x * config.MaximumLeanAngle;

		pivot.rotation = Quaternion.Euler( xtilt, heading, ztilt);
	}

	void ProcessPowerInput()
	{
		if (EngineEnabled)
		{
			float collective = CollectiveInputMappingFunction( FilteredCollectiveYawInput.y);

			float power = CollectiveToPowerMapping( collective);

			power *= Physics.gravity.magnitude;

			rb.AddForce( pivot.up * power);
		}
	}

	void FixedUpdate()
	{
		ProcessPowerInput();
	}
}
