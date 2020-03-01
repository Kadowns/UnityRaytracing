using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CameraController : MonoBehaviour {

	public float Speed = 20f;
	
	[Range(0.1f, 20f)]
	public float Sensitivity = 15F;
	public float MinimumX = -360F;
	public float MaximumX = 360F;
	public float MinimumY = -60F;
	public float MaximumY = 60F;
	public float FrameCounter = 20;
	
	private readonly List<float> m_rotArrayX = new List<float>();
	private readonly List<float> m_rotArrayY = new List<float>();
	
	private float m_rotationX;
	private float m_rotationY;
	private float m_rotAverageX;
	private float m_rotAverageY;
	
	private Quaternion m_originalRotation;
	private Rigidbody m_rb;

	private bool m_dragging;

	private void Awake() {
		m_rb = GetComponent<Rigidbody>();
		m_originalRotation = transform.localRotation;
	}

	public static float ClampAngle(float angle, float min, float max) {
		angle = angle % 360;
		if ((angle >= -360F) && (angle <= 360F)) {
			if (angle < -360F) {
				angle += 360F;
			}

			if (angle > 360F) {
				angle -= 360F;
			}
		}

		return Mathf.Clamp(angle, min, max);
	}


	private void Update() {

		if (Input.GetButton("Vertical")) {
			m_rb.AddForce(transform.forward * Time.deltaTime * Input.GetAxis("Vertical") * Speed);
		}
		
		if (Input.GetButton("Horizontal")) {
			m_rb.AddForce(transform.right * Time.deltaTime * Input.GetAxis("Horizontal") * Speed);
		}
		
		
		if (Input.GetMouseButtonDown(0)) {
			m_dragging = true;
			Cursor.visible = false;
			Cursor.lockState = CursorLockMode.Locked;
		}
		else if(Input.GetMouseButtonUp(0)) {
			m_dragging = false;
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;
		}

		m_rotAverageX = 0f;
		m_rotAverageY = 0f;

		if (m_dragging) {
			m_rotationX += Input.GetAxis("Mouse X") * Sensitivity;
			m_rotationY += Input.GetAxis("Mouse Y") * Sensitivity;	
		}
		
		m_rotArrayX.Add(m_rotationX);
		m_rotArrayY.Add(m_rotationY);

		
		if (m_rotArrayX.Count >= FrameCounter) {
			m_rotArrayX.RemoveAt(0);
		}
		if (m_rotArrayY.Count >= FrameCounter) {
			m_rotArrayY.RemoveAt(0);
		}
 
		for(int i = 0; i < m_rotArrayX.Count; i++) {
			m_rotAverageX += m_rotArrayX[i];
		}
		for(int j = 0; j < m_rotArrayY.Count; j++) {
			m_rotAverageY += m_rotArrayY[j];
		}

		m_rotAverageX /= m_rotArrayX.Count;
		m_rotAverageY /= m_rotArrayY.Count;

		m_rotAverageX = ClampAngle (m_rotAverageX, MinimumX, MaximumX);
		m_rotAverageY = ClampAngle (m_rotAverageY, MinimumY, MaximumY);

		Quaternion xQuaternion = Quaternion.AngleAxis (m_rotAverageX, Vector3.up);
		Quaternion yQuaternion = Quaternion.AngleAxis (m_rotAverageY, Vector3.left);

		transform.localRotation = m_originalRotation * xQuaternion * yQuaternion;
	}
}
