using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using System.IO;
using System;

public class VRCamera : MonoBehaviour
{

#region CameraSetup	
	// This is used for rotating the camera with another object
	//for example tilting the camera while going along a racetrack or rollercoaster
	public bool add_rotation_gameobject=false;
	public GameObject rotation_gameobject;
	
	// mouse emulation
	public bool emulateMouseInEditor=true;
	public enum RotationAxes { MouseXAndY = 0, MouseX = 1, MouseY = 2 }
	public RotationAxes axes = RotationAxes.MouseXAndY;
	public Texture nogyrotexture;
	
	/// Offset projection for 2 cameras in VR
	private float offset =0.0f;
	private float max_offset=0.002f;
	//public float max_offcenter_warp=0.1f;
	public Camera cameraleft;
	public Camera cameraright;
	
	public float zoom=0.1f;
	private float IPDCorrection=0.0f;
	private float aspectRatio;
	public float znear=0.1f;
	public float zfar=10000.0f;
	
	private float time_since_last_fullscreen=0;
	private int is_tablet;
	
	AndroidJavaObject mConfig;
	AndroidJavaObject mWindowManager;
	
	private float q0,q1,q2,q3;
	private float m0,m1,m2;
	Quaternion rot;
	private bool show_gyro_error_message=false;
	
	string errormessage;

	Vector3 cameraRotation;
#endregion CameraSetup
	
#region VRDeviceSetup
	#if UNITY_EDITOR
	private float sensitivityX = 15F;
	private float sensitivityY = 15F;
	private float minimumX = -360F;
	private float maximumX = 360F;
	private float minimumY = -90F;
	private float maximumY = 90F;
	float rotationY = 0F;
	#elif UNITY_ANDROID
	private static AndroidJavaClass javadivepluginclass;
	private static AndroidJavaClass javaunityplayerclass;
	private static AndroidJavaObject currentactivity;
	private static AndroidJavaObject javadiveplugininstance;
	
	[DllImport("divesensor")]	private static extern void initialize_sensors();
	[DllImport("divesensor")]	private static extern int get_q(ref float q0,ref float q1,ref float q2,ref float q3);
	[DllImport("divesensor")]	private static extern int get_m(ref float m0,ref float m1,ref float m2);
	[DllImport("divesensor")]	private static extern int get_error();
	[DllImport("divesensor")]   private static extern void dive_command(string command);
	#elif UNITY_IPHONE
	[DllImport("__Internal")]	private static extern void initialize_sensors();
	[DllImport("__Internal")]	private static extern float get_q0();
	[DllImport("__Internal")]	private static extern float get_q1();
	[DllImport("__Internal")]	private static extern float get_q2();
	[DllImport("__Internal")]	private static extern float get_q3();
	[DllImport("__Internal")]	private static extern void DiveUpdateGyroData();
	[DllImport("__Internal")]	private static extern int get_q(ref float q0,ref float q1,ref float q2,ref float q3);
	#endif 	
#endregion VRDeviceSetup

	public static VRCamera instance;
	public void Start(){

		if( instance == null )
			instance = this;

		rot=Quaternion.identity;
		// Disable screen dimming
		Screen.sleepTimeout = SleepTimeout.NeverSleep;
		Application.targetFrameRate = 60;
		
		#if UNITY_EDITOR
		if (rigidbody)
			rigidbody.freezeRotation = true;
		#elif UNITY_ANDROID
		// Java part
		javadivepluginclass = new AndroidJavaClass("com.shoogee.divejava.divejava") ;
		javaunityplayerclass= new AndroidJavaClass("com.unity3d.player.UnityPlayer");
		currentactivity = javaunityplayerclass.GetStatic<AndroidJavaObject>("currentActivity");
		javadiveplugininstance = javadivepluginclass.CallStatic<AndroidJavaObject>("instance");
		object[] args={currentactivity};
		javadiveplugininstance.Call<string>("set_activity",args);
		
		initialize_sensors ();
		
		String answer;
		answer= javadiveplugininstance.Call<string>("initializeDive");
		answer= javadiveplugininstance.Call<string>("getDeviceType");
		if (answer=="Tablet"){
			is_tablet=1;
			Debug.Log("Dive Unity Tablet Mode activated");
		}
		else{
			Debug.Log("Dive Phone Mode activated "+answer);
		}
		
		answer= javadiveplugininstance.Call<string>("setFullscreen");
		show_gyro_error_message=true;
		Network.logLevel = NetworkLogLevel.Full;
		
		int err = get_error();
		if (err==0){ errormessage="";
			show_gyro_error_message=false;
		}
		if (err==1){
			show_gyro_error_message=true;
			errormessage="ERROR: Dive needs a Gyroscope and your telephone has none, we are trying to go to Accelerometer compatibility mode. Dont expect too much.";
		}
		#elif UNITY_IPHONE
		initialize_sensors();
		#endif
		
		float tabletcorrection=-0.028f;
		//is_tablet=0;
		if (is_tablet==1)
		{
			Debug.Log ("Is tablet, using tabletcorrection");
			IPDCorrection=tabletcorrection;
		}
		else 
		{
			IPDCorrection=IPDCorrection;
		}
		//setIPDCorrection(IPDCorrection); 
	}
	
	
	
	public void UpdateCamera () {
		aspectRatio=(Screen.height*2.0f)/Screen.width;
		setIPDCorrection(IPDCorrection); 
		
		//Debug.Log ("Divecamera"+cameraleft.aspect+"1/asp "+1/cameraleft.aspect+" Screen Width/Height "+ aspectRatio);
		#region CameraUpdate
		#if UNITY_EDITOR
		#elif UNITY_ANDROID
		time_since_last_fullscreen+=Time.deltaTime;
		
		if (time_since_last_fullscreen >8){
			setFullscreen ();
			time_since_last_fullscreen=0;
		}
		
		get_q(ref q0,ref q1,ref q2,ref q3);
		//get_m(ref m0,ref m1,ref m2);
		rot.x=-q2;rot.y=q3;rot.z=-q1;rot.w=q0;
		
		if (add_rotation_gameobject){
			rotationDelta = rotation_gameobject.transform.rotation* rot;


			transform.rotation =rotation_gameobject.transform.rotation* rot;
		}
		else {
			transform.rotation = rot;
			if (is_tablet==1)transform.rotation=rot*Quaternion.AngleAxis(90,Vector3.forward);			
		}
		#elif UNITY_IPHONE
		DiveUpdateGyroData();
		get_q(ref q0,ref q1,ref q2,ref q3);
		rot.x=-q2;
		rot.y=q3;
		rot.z=-q1;
		rot.w=q0;
		transform.rotation = rot;
		
		if (add_rotation_gameobject){
			transform.rotation =rotation_gameobject.transform.rotation* rot;
		}
		else
		{
			transform.rotation = rot;
			if (is_tablet==1)transform.rotation=rot*Quaternion.AngleAxis(90,Vector3.forward);	
		}
		#endif
		#endregion 
		
		#if UNITY_EDITOR
		if (emulateMouseInEditor){
			if (axes == RotationAxes.MouseXAndY)
			{
				float rotationX = transform.localEulerAngles.y + Input.GetAxis("Mouse X") * sensitivityX;				
				rotationY += Input.GetAxis("Mouse Y") * sensitivityY;
				rotationY = Mathf.Clamp (rotationY, minimumY, maximumY);
				transform.localEulerAngles = new Vector3(-rotationY, rotationX, 0);
			}
			else if (axes == RotationAxes.MouseX)
			{
				transform.Rotate(0, Input.GetAxis("Mouse X") * sensitivityX, 0);
			}
			else
			{
				rotationY += Input.GetAxis("Mouse Y") * sensitivityY;
				rotationY = Mathf.Clamp (rotationY, minimumY, maximumY);
				transform.localEulerAngles = new Vector3(-rotationY, transform.localEulerAngles.y, 0);
			}
		}
		#endif
	}

	public static void divecommand(string command){
		#if UNITY_EDITOR
		#elif UNITY_ANDROID
		dive_command(command);
		#elif UNITY_IPHONE
		#endif
		
		return;
	}
	
	public static void setFullscreen(){
		#if UNITY_EDITOR
		#elif UNITY_ANDROID
		String answer;
		answer= javadiveplugininstance.Call<string>("setFullscreen");
		#elif UNITY_IPHONE
		#endif 
		
		return;
	}

	void setIPDCorrection(float correction) {
		// not using camera nearclipplane value because that leads to problems with field of view in different projects
		cameraleft.projectionMatrix = PerspectiveOffCenter((-zoom+correction)*(znear/0.1f), (zoom+correction)*(znear/0.1f), -zoom*(znear/0.1f)*aspectRatio, zoom*(znear/0.1f)*aspectRatio, znear, zfar);;
		cameraright.projectionMatrix = PerspectiveOffCenter((-zoom-correction)*(znear/0.1f), (zoom-correction)*(znear/0.1f), -zoom*(znear/0.1f)*aspectRatio, zoom*(znear/0.1f)*aspectRatio, znear, zfar);;
	}

	private static Matrix4x4 PerspectiveOffCenter (float left, float right, float bottom, float top, float near, float far)
	{
		float x =  (2.0f * near)		/ (right - left);
		float y =  (2.0f * near)		/ (top - bottom);
		float a =  (right + left)		/ (right - left);
		float b =  (top + bottom)		/ (top - bottom);
		float c = -(far + near)		/ (far - near);
		float d = -(2.0f * far * near) / (far - near);
		float e = -1.0f;
		
		Matrix4x4 m = new Matrix4x4();
		m[0,0] =   x;  m[0,1] = 0.0f;  m[0,2] = a;   m[0,3] = 0.0f;
		m[1,0] = 0.0f;  m[1,1] =   y;  m[1,2] = b;   m[1,3] = 0.0f;
		m[2,0] = 0.0f;  m[2,1] = 0.0f;  m[2,2] = c;   m[2,3] =   d;
		m[3,0] = 0.0f;  m[3,1] = 0.0f;  m[3,2] = e;   m[3,3] = 0.0f;
		return m;
	}

	private void SetVanishingPoint (Camera cam, Vector2 perspectiveOffset) {
		Matrix4x4 m = cam.projectionMatrix;
		float w = 2*cam.nearClipPlane/m.m00;
		float h = 2*cam.nearClipPlane/m.m11;
		
		float left = -w/2 - perspectiveOffset.x;
		float right = left+w;
		float bottom = -h/2 - perspectiveOffset.y;
		float top = bottom+h;
		
		cam.projectionMatrix = PerspectiveOffCenter(left, right, bottom, top, cam.nearClipPlane, cam.farClipPlane);
	}

	public void setCorrectionFactor(float fac){
		offset=max_offset*fac;
		correctionfactor=fac;
		SetVanishingPoint(cameraleft, new Vector2(offset, 0.0f));
		SetVanishingPoint(cameraright, new Vector2(-offset, 0.0f));
	}
}

