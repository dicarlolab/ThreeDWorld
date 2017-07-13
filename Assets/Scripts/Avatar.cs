using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using NetMQ;

/// <summary>
/// Controls a single avatar driven by a connection to a client
/// </summary>
public class Avatar : MonoBehaviour
{
#region Fields
    // Reference to camera component we are streaming from. TODO: Change to a list to allow multiple cameras?
    public CameraStreamer myCam = null;
    // Reference to all the shaders we are using with each camera(null uses standard rendering)
    public List<Shader> shaders = null;
    // Output format corresponding to shaders
	public List<string> outputFormatList;
    // How fast the avatar can translate using the client controls
    public float moveSpeed = 5.0f;
    // How fast the avatar can rotate using the client controls
    public float rotSpeed = 5.0f;
    // The range for which this avatar can observe SemanticObject's
    public float observedRange = 25.0f;
    // send scene info?
	public bool sendSceneInfo = false;

	public ProceduralGeneration scene{get; set;}

    private AbstractInputModule _myInput = null;
    private List<SemanticObject> _observedObjs = new List<SemanticObject>();
    private Vector3 _targetVelocity = Vector3.zero;
    private Rigidbody _myRigidbody = null;
    private bool _readyForSimulation = false;
    private NetMessenger _myMessenger = null;
    private NetMQ.Sockets.ResponseSocket _myServer = null;
    private CameraStreamer.CaptureRequest _request;
    private bool _shouldCollectObjectInfo = true;
    private List<string> _relationshipsToRetrieve = new List<string>();
	private System.Random _rand;
	private float initialHeight = 0.0f;

#endregion

#region Properties
    // The rigidbody associated with the avatar's body
    public Rigidbody myRigidbody {
        get {
            if (_myRigidbody == null)
                _myRigidbody = gameObject.GetComponent<Rigidbody>();
            return _myRigidbody;
        }
    }
    
    // Indicates when the client for this avatar is awaiting running the next set of frames
    public bool readyForSimulation {
        get { return _readyForSimulation; }
        set { _readyForSimulation = value; }
    }
    
    // The server-to-client connection associated with this avatar
    public NetMQ.Sockets.ResponseSocket myServer {
        get { return _myServer; }
    }
    
    // Objects found within observation radius of this avatar
    public List<SemanticObject> observedObjs {
        get { return _observedObjs; }
    }

    // Simulates input with a controller and interprets input messages
    public AbstractInputModule myInput {
        get { return _myInput; }
    }

    public bool shouldCollectObjectInfo {
        get { return _shouldCollectObjectInfo; }
        set { _shouldCollectObjectInfo = value; }
    }

    public List<string> relationshipsToRetrieve {
        get { return _relationshipsToRetrieve; }
        set { _relationshipsToRetrieve = value; }
    }
#endregion

#region Unity callbacks
    private void Awake()
    {
    	Debug.Assert(this.outputFormatList.Count == this.shaders.Count);

		Debug.Log ("I'm awake!");
		_rand = new System.Random ();
		Debug.Log ("got random");
        _request = new CameraStreamer.CaptureRequest();
		Debug.Log ("got streamer");
        _request.shadersList = shaders;
		Debug.Log ("got shaders");
		_request.outputFormatList = outputFormatList;
		Debug.Log ("got output formats");
        _request.capturedImages = new List<CameraStreamer.CapturedImage>();
		Debug.Log ("got capt images");
        _myInput = new InputModule(this);
		Debug.Log ("got input");
        TeleportToValidPosition();
		Debug.Log ("teleported");
    }

    public void SetShaderList(List<string[]> shaderList)
    {
		this.shaders = new List<Shader> ();
		this.outputFormatList = new List<string> ();
    	foreach(string[] shader in shaderList)
    	{
    		Debug.Assert(shader.Length == 2);
    		if(shader[0] == "Image")
    			this.shaders.Add(null);
    		else
    			this.shaders.Add(Shader.Find(shader[0]));
    		this.outputFormatList.Add(shader[1]);
    	}
    	_request.shadersList = this.shaders;
    	_request.outputFormatList = this.outputFormatList;
    }

    private void FixedUpdate()
    {
        if(myRigidbody != null)
        {
            myInput.OnFixedUpdate();
        }
    }
#endregion

	public void ResetObjectTransformations()
	{
		if( myCam != null) {
			int n_cameras = 2;
			myCam.ResetObjectTransforms(n_cameras);
		}
		else
		{
			Debug.LogWarning("ResetObjectTransformations myCam null");
		}
	}

	public void ReadyFramesForRequest()
    {
        // Set up rendering
		if (myCam != null) {
			myCam.RequestCaptures (_request);
		} else {
			Debug.LogWarning ("ReadyFramesForRequest myCam null");
		}
    }

    public void setRotationEuler(Vector3 newAngles) {
		Quaternion avatarRotation = this.transform.rotation;
		avatarRotation.eulerAngles = newAngles;
		this.transform.rotation = avatarRotation;
    }

    // Looks for all the SemanticObject's that are within the range
    public void UpdateObservedObjects()
    {
//		Debug.Log ("UpdateObserveredObjects");
        _observedObjs.Clear();
        if (!_shouldCollectObjectInfo)
            return;
//        if (NetMessenger.logTimingInfo)
//            Debug.LogFormat("Starting Avatar.UpdateObservedObjects() {0}", Utils.GetTimeStamp());
        Collider[] observedObjects = Physics.OverlapSphere(transform.position, observedRange);
//        if (NetMessenger.logTimingInfo)
//            Debug.LogFormat("Finished OverlapSphere() and found {1}, {0}", Utils.GetTimeStamp(), observedObjects.Length);

        foreach(Collider col in observedObjects)
        {
			//Spawn areas are not observable objects!
			if (col.gameObject.GetComponent<SpawnArea> () != null)
				continue;
			
            SemanticObjectSimple obj = null;
            if (col.attachedRigidbody == null)
                Debug.LogWarningFormat("{0} Collider doesn't have an attached rigidbody!", col.name);
            else
            {
                obj = col.attachedRigidbody.GetComponent<SemanticObjectSimple>();
//				if (obj == null)
//					Debug.LogWarningFormat ("{0} Rigidbody doesn't have an associated SemanticObject!", col.attachedRigidbody.name);
            }
            if (obj != null && !_observedObjs.Contains(obj))
            {
                _observedObjs.Add(obj);
                foreach(SemanticObjectComplex parentObj in obj.GetParentObjects())
                {
                    if (!_observedObjs.Contains(parentObj))
                        _observedObjs.Add(parentObj);
                }
            }
        }
//		if (NetMessenger.logTimingInfo) {
//			Debug.LogFormat ("Finished Avatar.UpdateObservedObjects() and found {1} {0}", Utils.GetTimeStamp (), observedObjs.Count);
//		}
//		Debug.Log ("exiting UpdateObserveredObjects");
    }

    public void TeleportToGivenPosition(Vector3 new_position, Vector3 new_rotation)
    {
		scene = FindObjectOfType<ProceduralGeneration>();
		transform.localRotation = Quaternion.LookRotation(new_rotation);

		RaycastHit hit = new RaycastHit();
		const float radius = 0.3f;
		float setHeight = new_position.y;
		new_position.y = scene.roomDim.y - 0.5f;

		if (Physics.Raycast (new_position, Vector3.down, out hit, 2 * scene.roomDim.y)) {
			new_position.y = hit.point.y + radius;
		}
		new_position.y = Mathf.Max(setHeight, new_position.y);
        transform.position = new_position;
    }

    public void TeleportToValidPosition()
    {
		scene = FindObjectOfType<ProceduralGeneration>();
		Vector3 look_at_position = new Vector3((scene.roomDim.x-1) * 0.5f, 0, (scene.roomDim.z-1) * 0.5f);

		Debug.Log ("Teleporting!");
        // TODO: Have this check for more than a simple sphere of radius 0.5f for when we extend the avatar
        const float radius = 0.3f;
        transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
		Debug.Log ("set radius");
        float startHeight = 1.1f * radius;
		Debug.Log ("spawning...");

		SpawnArea[] spawnAreaCandidates = GameObject.FindObjectsOfType<SpawnArea> ();

		if (spawnAreaCandidates.Length == 0)
			Debug.LogWarning ("No Spawn Areas found!");

		Vector3 look_direction = new Vector3(0f, 0f, 0f);
        for (int i = 0; i < 1000; ++i)
        {
			SpawnArea chosenSpawnArea = spawnAreaCandidates [_rand.Next (0, spawnAreaCandidates.Length)];

			Vector3 spawnTest = chosenSpawnArea.acquireLocation();
			spawnTest.y = startHeight;

			Debug.Log ("Attempting avatar spawn at: " + spawnTest);
            if (!Physics.CheckSphere(spawnTest, radius))
            {
                RaycastHit hit = new RaycastHit();
                if (Physics.SphereCast(spawnTest, radius, Vector3.down, out hit, startHeight))
                {
					spawnTest.y += UnityEngine.Random.Range(0, hit.distance);
					bool HOVER = true;
					if(HOVER) 
					{
						spawnTest.y += 0.5f;
					}
                    transform.position = spawnTest;
					initialHeight = transform.position.y;
                    // Looking towards center
					look_at_position.y = transform.position.y;
					look_direction = (look_at_position - transform.position).normalized;
					transform.rotation = Quaternion.LookRotation(look_direction); //Quaternion.identity;
                    return;
                }

                transform.position = spawnTest;
				initialHeight = transform.position.y;
                // Looking towards center
				look_at_position.y = transform.position.y;
				look_direction = (look_at_position - transform.position).normalized;
				transform.rotation = Quaternion.LookRotation(look_direction); //Quaternion.identity;
				Debug.Log ("...spawned");
                return;
            }
        }
        Debug.LogWarning("Couldn't find a spot to place the avatar!");
    }

    public void InitNetData(NetMessenger myNewMessenger, NetMQ.Sockets.ResponseSocket myNewServer)
    {
        Debug.Log("Calling InitNetData");
        _myMessenger = myNewMessenger;
        _myServer = myNewServer;
        _request.callbackFunc = (CameraStreamer.CaptureRequest req)=>{_myMessenger.SendFrameUpdate(req, this);};
        ReadyFramesForRequest();
    }

    // Parse the input sent from the client and use it to update the controls for the next simulation segment
    public void HandleNetInput(LitJson.JsonData msgJsonData)
    {
        _myInput.HandleNetInput(msgJsonData, ref _targetVelocity);

        _readyForSimulation = true;
        // Now ready the output and run the simulation a few frames
        SimulationManager.CheckToggleUpdates();
    }
}
