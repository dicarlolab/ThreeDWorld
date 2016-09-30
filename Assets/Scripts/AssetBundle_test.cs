using UnityEngine;
using System.Collections;
using System.IO;
using System;
using System.Linq;
using System.Text.RegularExpressions;


#if UNITY_EDITOR
using UnityEditor;
#endif

public class AssetBundle_test : MonoBehaviour
{
	#region Private variables

	private int _randSeed = 123;
	System.Random _rand = null;
	private string[] assetBundleList = null;

	#endregion

	#region Movement variables
	public string script_mode = "merged";			// Modes are "merged" and "separated"

	public float speedH = 2.0f;
	public float speedV = 2.0f;

	private float yaw = 0.0f;
	private float pitch = 0.0f;
	private float xPos = 0.0f;
	private float zPos = 0.0f;
	#endregion

	// Use this for initialization
	void Start ()
	{
		Init ();
	}

	private void Init ()
	{
		System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew ();

		_rand = new System.Random (_randSeed);

		// List all files in AssetBundles directory
		string currentPath = Directory.GetCurrentDirectory ();
		string AssetBundlesPath = Path.Combine (currentPath, "Assets/AssetBundles/");
		if (script_mode == "merged") {
			assetBundleList = Directory.GetFiles (Path.Combine(AssetBundlesPath, "Merged"), "*.bundle");
		} else if (script_mode == "separated") {
			assetBundleList = Directory.GetFiles (Path.Combine(AssetBundlesPath, "Separated"), "*.bundle");
			//			assetBundleList = dir.GetFiles ("*.bundle");
//			Regex re = new Regex("(?!.*default.*)");
//			assetBundleList = Directory.GetFiles (AssetBundlesPath, "*.bundle");
//				.Where (s => re.IsMatch (s)).ToList ();
//				.Where (s => !s.Remove(0, AssetBundlesPath.Length).StartsWith ("default_"));

		}

		foreach (string f in assetBundleList) {
			var loadedAssetBundle = AssetBundle.LoadFromFile (f);

			if (loadedAssetBundle == null) {
				Debug.Log ("Failed to load AssetBundle!");
				return;
			} else {
				string[] assetList = loadedAssetBundle.GetAllAssetNames ();
				foreach (string asset in assetList) { 
					var prefab = loadedAssetBundle.LoadAsset<GameObject> (asset);
					Vector3 randLoc = new Vector3 ((float)_rand.NextDouble () * 50, 10.0f, (float)_rand.NextDouble () * 50);
					Instantiate (prefab,
						randLoc,
						Quaternion.identity
					);
				}
			}
			loadedAssetBundle.Unload (false);
		}
		stopwatch.Stop ();
		Debug.Log("Elapsed time: " + stopwatch.ElapsedMilliseconds);
	}

	// Update is called once per frame
	void Update ()
	{
		float xAxisValue = Input.GetAxis ("Horizontal");
		float zAxisValue = Input.GetAxis ("Vertical");
	

		if (Input.GetMouseButton (0)) {
//			var target = SceneView.FindObjectOfType<Light> ();
//			transform.LookAt (target);
//			transform.RotateAround (target.position, Vector3.up, Input.GetAxis ("Mouse X") * speed);
		}
		if (Camera.main != null) {
			if (xAxisValue != 0 | zAxisValue != 0) {
				xPos += speedH * xAxisValue;
				zPos += speedV * zAxisValue;

				// Movement should be wrt veiwing angle of the camera (agent)
				Vector3 RIGHT = transform.TransformDirection(Vector3.right);
				RIGHT.y = 0.0f;
				Vector3 FORWARD = transform.TransformDirection(Vector3.forward);
				FORWARD.y = 0.0f;
//				transform.position = new Vector3 (xPos, 2.0f, zPos);
				transform.localPosition += RIGHT * xAxisValue;
				transform.localPosition += FORWARD * zAxisValue;
			}
			if (Input.GetMouseButton (0)) {
				yaw += speedH * Input.GetAxis("Mouse X");
				pitch -= speedV * Input.GetAxis("Mouse Y");
				transform.eulerAngles = new Vector3(pitch, yaw, 0.0f);
			}			
		}
	}
}

