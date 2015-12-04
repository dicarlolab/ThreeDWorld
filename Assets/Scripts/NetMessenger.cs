using UnityEngine;
using System.Collections;
using NetMQ;
using System;
using UnityEngine.UI;
using System.Collections.Generic;

public class NetMessenger : MonoBehaviour
{
#region Fields
    public NetMQContext ctx;
    public NetMQ.Sockets.ResponseSocket server = null;
    public NetMQ.Sockets.RequestSocket client = null;
#endregion

#region Unity callbacks
    void Start()
    {
        ctx = NetMQContext.Create();
        Connect();
        Test();
    }
    
    void Update()
    {
    }
#endregion

    private void Connect()
    {
        // TODO: set up connection properly
#if UNITY_EDITOR
        server = ctx.CreateResponseSocket();
        server.Bind("tcp://127.0.0.1:5556");
#endif
        client = ctx.CreateRequestSocket();
        client.Connect("tcp://127.0.0.1:5556");
    }

    public void SendFrameUpdate(CameraStreamer.CaptureRequest streamCapture)
    {
        NetMQMessage msg = new NetMQ.NetMQMessage();
        // TODO: Additional frame message description?

        // Add in captured frames
        int numValues = Mathf.Min(streamCapture.shadersList.Count, streamCapture.retValue.Count);
        for(int i = 0; i < numValues; ++i)
            msg.Append(streamCapture.retValue[i].pictureBuffer);

        // TODO: Insert other wanted data

        // Send out the real message
        client.SendMultipartMessage(msg);
    }

    private void Test()
    {
#if UNITY_EDITOR
        client.SendFrame("Hello");
        string fromClientMessage = server.ReceiveFrameString();
        Debug.LogFormat("From Client: {0}", fromClientMessage);
        server.SendFrame("Hi Back");
        string fromServerMessage = client.ReceiveFrameString();
        Debug.LogFormat("From Server: {0}", fromServerMessage);
#endif
    }
}
