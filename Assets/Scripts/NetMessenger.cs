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

    private NetMQMessage lastMessage = new NetMQMessage();
    private NetMQMessage lastMessageSent = new NetMQMessage();
#endregion

#region Const message values
    // To Send From Server
    const string MSG_S_ConfirmClientJoin = "CLIENT_INIT";
    const string MSG_S_FrameData = "FRAME_UPDATE";

    // To Receive From Client
    const string MSG_R_ClientJoin = "CLIENT_JOIN";
    const string MSG_R_FrameInput = "CLIENT_INPUT";
#endregion

#region Unity callbacks
    void Start()
    {
        ctx = NetMQContext.Create();
        CreateServer();
#if UNITY_EDITOR
        CreateTestClient();
        Test();
#endif
    }
    
    void Update()
    {
        if (server != null)
        {
            if (server.TryReceiveMultipartMessage(ref lastMessage))
                HandleFrameMessage();
        }
    }
#endregion

    private void CreateServer()
    {
        // TODO: Set up in a way that allows for multiple clients
        // We'd need separate sockets for each client
        server = ctx.CreateResponseSocket();
        server.Bind("tcp://127.0.0.1:5556");
    }

    private void CreateTestClient()
    {
        client = ctx.CreateRequestSocket();
        client.Connect("tcp://127.0.0.1:5556");
        client.SendFrame("Hello");
    }

#region Receive messages from the client
    public void HandleFrameMessage()
    {
        string msgHeader = lastMessage.First.ToString();

        switch(msgHeader.ToString())
        {
            case MSG_R_ClientJoin:
                OnClientJoin(lastMessage);
                break;
            case MSG_R_FrameInput:
                RecieveClientInput(lastMessage);
                break;
        }
    }

    public void RecieveClientInput(NetMQMessage msg)
    {
        // TODO: Ferry input to correct location
    }

    public void OnClientJoin(NetMQMessage msg)
    {
        lastMessageSent.Clear();
        lastMessageSent.Append(MSG_S_ConfirmClientJoin);
        client.SendMultipartMessage(lastMessageSent);
    }
#endregion
    
    public void SendFrameUpdate(CameraStreamer.CaptureRequest streamCapture)
    {
        lastMessageSent.Clear();
        lastMessageSent.Append(MSG_S_FrameData);
        // TODO: Additional frame message description?
        
        // Add in captured frames
        int numValues = Mathf.Min(streamCapture.shadersList.Count, streamCapture.retValue.Count);
        for(int i = 0; i < numValues; ++i)
            lastMessageSent.Append(streamCapture.retValue[i].pictureBuffer);
        
        // TODO: Insert other wanted data
        
        // Send out the real message
        client.SendMultipartMessage(lastMessageSent);
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
