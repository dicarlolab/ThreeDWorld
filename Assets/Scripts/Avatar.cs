using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using NetMQ;

public class Avatar : MonoBehaviour
{
#region Fields
    public CameraStreamer myCam = null;
    public List<Shader> shaders = null;
    public float moveSpeed = 5.0f;
    public float rotSpeed = 5.0f;
    public float observedRange = 25.0f;

    private List<SemanticObject> _observedObjs = new List<SemanticObject>();
    private Vector3 targetVelocity = Vector3.zero;
    private Rigidbody _myRigidbody = null;
    private bool _readyForSimulation = false;
    private NetMessenger myMessenger = null;
    private NetMQ.Sockets.ResponseSocket _myServer = null;
    private CameraStreamer.CaptureRequest request;
#endregion

#region Properties
    public Rigidbody myRigidbody {
        get {
            if (_myRigidbody == null)
                _myRigidbody = gameObject.GetComponent<Rigidbody>();
            return _myRigidbody;
        }
    }
    
    public bool readyForSimulation {
        get { return _readyForSimulation; }
        set { _readyForSimulation = value; }
    }
    
    public NetMQ.Sockets.ResponseSocket myServer {
        get { return _myServer; }
    }

    public List<SemanticObject> observedObjs {
        get { return _observedObjs; }
    }
#endregion

    private void Awake()
    {
        request = new CameraStreamer.CaptureRequest();
        request.shadersList = shaders;
        request.capturedImages = new List<CameraStreamer.CapturedImage>();
    }

    public void ReadyFramesForRequest()
    {
        // Set up rendering
        myCam.RequestCaptures(request);
    }

    public void UpdateObservedObjects()
    {
        _observedObjs.Clear();
        Collider[] observedObjects = Physics.OverlapSphere(transform.position, observedRange);
        foreach(Collider col in observedObjects)
        {
            SemanticObject obj = col.attachedRigidbody.GetComponent<SemanticObject>();
            if (obj != null && !_observedObjs.Contains(obj))
                _observedObjs.Add(obj);
        }
    }

    public void InitNetData(NetMessenger myNewMessenger, NetMQ.Sockets.ResponseSocket myNewServer)
    {
        Debug.Log("Calling InitNetData");
        myMessenger = myNewMessenger;
        _myServer = myNewServer;
        request.callbackFunc = (CameraStreamer.CaptureRequest req)=>{myMessenger.SendFrameUpdate(req, this);};
        ReadyFramesForRequest();
    }

    public void HandleNetInput(NetMQMessage msg)
    {
        // Get movement
        if (msg.FrameCount > 3)
            targetVelocity = (moveSpeed / 4096.0f) * new Vector3(msg[1].ConvertToInt32(), msg[2].ConvertToInt32(), msg[3].ConvertToInt32());
        if (msg.FrameCount > 6)
        {
            Vector3 angChange = (rotSpeed / 4096.0f) * new Vector3(msg[4].ConvertToInt32(), msg[5].ConvertToInt32(), msg[6].ConvertToInt32());
            angChange -= myRigidbody.angularVelocity;
            // Clamp value to max change?
            if (angChange.magnitude > rotSpeed)
                angChange = angChange.normalized * rotSpeed;
            myRigidbody.AddTorque(angChange, ForceMode.VelocityChange);
        }

        _readyForSimulation = true;
        // Now ready the output and run the simulation a few frames
        SimulationManager.CheckToggleUpdates();
    }

    private void FixedUpdate()
    {
        if(myRigidbody != null)
        {
            myRigidbody.velocity = targetVelocity;
        }
    }
}
