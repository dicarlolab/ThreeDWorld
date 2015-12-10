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

    private Vector3 targetVelocity = Vector3.zero;
    private Rigidbody _myRigidbody = null;
    private bool _readyForSimulation = false;
    private NetMessenger myMessenger = null;
//    private NetMQ.Sockets.ResponseSocket myServer = null;
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
#endregion

    private void Awake()
    {
        request = new CameraStreamer.CaptureRequest();
        request.shadersList = shaders;
        request.capturedImages = new List<CameraStreamer.CapturedImage>();
    }

    public void ReadyFramesForRequest()
    {
        myCam.RequestCaptures(request);
        _readyForSimulation = false; // TODO: Set this in the correct location
    }

    public void InitNetData(NetMessenger myNewMessenger, NetMQ.Sockets.ResponseSocket myNewServer)
    {
        Debug.Log("Calling InitNetData");
        myMessenger = myNewMessenger;
//        myServer = myNewServer;
        request.callbackFunc = myMessenger.SendFrameUpdate;
        ReadyFramesForRequest();
    }

    public void HandleNetInput(NetMQMessage msg)
    {
//        Debug.Log("HandleNetInput");
        // Get movement
        if (msg.FrameCount > 3)
            targetVelocity = (moveSpeed / 4096.0f) * new Vector3(msg[1].ConvertToInt32(), msg[2].ConvertToInt32(), msg[3].ConvertToInt32());

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
