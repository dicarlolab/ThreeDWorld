using UnityEngine;
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
    // How fast the avatar can translate using the client controls
    public float moveSpeed = 5.0f;
    // How fast the avatar can rotate using the client controls
    public float rotSpeed = 5.0f;
    // The range for which this avatar can observe SemanticObject's
    public float observedRange = 25.0f;

    private AbstractInputModule _myInput = null;
    private List<SemanticObject> _observedObjs = new List<SemanticObject>();
    private Vector3 _targetVelocity = Vector3.zero;
    private Rigidbody _myRigidbody = null;
    private bool _readyForSimulation = false;
    private NetMessenger _myMessenger = null;
    private NetMQ.Sockets.ResponseSocket _myServer = null;
    private CameraStreamer.CaptureRequest _request;
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
#endregion

#region Unity callbacks
    private void Awake()
    {
        _request = new CameraStreamer.CaptureRequest();
        _request.shadersList = shaders;
        _request.capturedImages = new List<CameraStreamer.CapturedImage>();
        _myInput = new InputModule(this);
    }

    private void FixedUpdate()
    {
        if(myRigidbody != null)
        {
            myInput.OnFixedUpdate();
        }
    }
#endregion

    public void ReadyFramesForRequest()
    {
        // Set up rendering
        myCam.RequestCaptures(_request);
    }

    // Looks for all the SemanticObject's that are within the range
    public void UpdateObservedObjects()
    {
        _observedObjs.Clear();
        Collider[] observedObjects = Physics.OverlapSphere(transform.position, observedRange);
        foreach(Collider col in observedObjects)
        {
            SemanticObjectSimple obj = null;
            if (col.attachedRigidbody == null)
                Debug.LogWarningFormat("{0} Collider doesn't have an attached rigidbody!", col.name);
            else
            {
                obj = col.attachedRigidbody.GetComponent<SemanticObjectSimple>();
                if (obj == null)
                    Debug.LogWarningFormat("{0} Rigidbody doesn't have an associated SemanticObject!", col.attachedRigidbody.name);
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
    public void HandleNetInput(NetMQMessage msg)
    {
        _myInput.HandleNetInput(msg, ref _targetVelocity);

        _readyForSimulation = true;
        // Now ready the output and run the simulation a few frames
        SimulationManager.CheckToggleUpdates();
    }
}
