using UnityEngine;
using System.Collections;
using NetMQ;
using System;
using UnityEngine.UI;
using System.Collections.Generic;
using NetMQ.Sockets;

public class NetMessenger : MonoBehaviour
{
#region Fields
    public Avatar avatarPrefab;
    public NetMQContext ctx;
    public bool shouldCreateTestClient = true;

    private NetMQMessage lastMessage = new NetMQMessage();
    private NetMQMessage lastMessageSent = new NetMQMessage();
    private List<ResponseSocket> createdSockets = new List<ResponseSocket>();
    private Dictionary<ResponseSocket, Avatar> avatars = new Dictionary<ResponseSocket, Avatar>();
    private Dictionary<ResponseSocket, RequestSocket> avatarClients = new Dictionary<ResponseSocket, RequestSocket>();
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
        SimulationManager.Init();
    }

    public void Init()
    {
        ctx = NetMQContext.Create();
        CreateNewSocketConnection();
    }

    public bool AreAllAvatarsReady()
    {
        bool allready = true;
        foreach(Avatar a in avatars.Values)
            allready = allready && a.readyForSimulation;
        return allready;
    }
    
    void Update()
    {
        foreach(ResponseSocket server in createdSockets)
        {
//            Debug.LogFormat("Server In: {0}, Out: {1}", server.HasIn, server.HasOut);
            if (server.HasIn && server.TryReceiveMultipartMessage(TimeSpan.Zero, ref lastMessage))
                HandleFrameMessage(server, lastMessage);
            RequestSocket client = null;
            if (avatarClients.ContainsKey(server))
                client = avatarClients[server];
            if (client != null)
            {
//                Debug.LogFormat("Client In: {0}, Out: {1}", client.HasIn, client.HasOut);
                if (client.HasIn && client.TryReceiveMultipartMessage(TimeSpan.Zero, ref lastMessage))
                    HandleClientFrameMessage(client, lastMessage);
                
            }
        }
    }

    private void FixedUpdate()
    {
        // TODO: Handle this for when we have multiple Avatars
        if (SimulationManager.FinishUpdatingFrames())
        {
            foreach(Avatar a in avatars.Values)
                a.ReadyFramesForRequest();
        }
    }

    private void OnDisable()
    {
        foreach(ResponseSocket server in createdSockets)
        {
            if (avatarClients.ContainsKey(server))
            {
                avatarClients[server].Close();
                avatarClients[server].Dispose();
            }
            server.Close();
            server.Dispose();
            if (avatars.ContainsKey(server))
            {
                Avatar avatar = avatars[server];
                if (avatar != null && avatar.gameObject != null)
                    GameObject.Destroy(avatars[server].gameObject);
            }
        }
        avatars.Clear();
        createdSockets.Clear();
        avatarClients.Clear();
        if (ctx != null)
        {
            ctx.Terminate();
            ctx.Dispose();
            ctx = null;
        }
    }
#endregion

#region Setup
    private void CreateNewSocketConnection()
    {
        ResponseSocket server = ctx.CreateResponseSocket();
        server.Bind("tcp://127.0.0.1:5556");
        createdSockets.Add(server);
        if (shouldCreateTestClient)
            CreateTestClient(server);
    }

    private void CreateTestClient(ResponseSocket server)
    {
        RequestSocket client = ctx.CreateRequestSocket();
        client.Connect("tcp://127.0.0.1:5556");
        avatarClients[server] = client;
        client.SendFrame(MSG_R_ClientJoin);
    }
#endregion

#region Receive messages from the client
    public void HandleFrameMessage(ResponseSocket server, NetMQMessage msg)
    {
        string msgHeader = msg.First.ConvertToString();
        Debug.LogFormat("Got message on Server: {0}, {1} frames", msgHeader, msg.FrameCount);

        switch(msgHeader.ToString())
        {
            case MSG_R_ClientJoin:
                OnClientJoin(server, msg);
                break;
            case MSG_R_FrameInput:
                RecieveClientInput(server, msg);
                break;
        }
    }

    public void RecieveClientInput(ResponseSocket server, NetMQMessage msg)
    {
        avatars[server].HandleNetInput(msg);
    }

    public void OnClientJoin(ResponseSocket server, NetMQMessage msg)
    {
        // Setup new avatar object from prefab
        Avatar newAvatar = UnityEngine.Object.Instantiate<Avatar>(avatarPrefab);
        avatars[server] = newAvatar;
        newAvatar.InitNetData(this, server);
//
//        // Send confirmation message
//        lastMessageSent.Clear();
//        lastMessageSent.Append(MSG_S_ConfirmClientJoin);
//        server.SendMultipartMessage(lastMessageSent);
    }
#endregion
    
#region Simulate recieving message on the client
    // Used for debugging without an agent
    public void HandleClientFrameMessage(RequestSocket client, NetMQMessage msg)
    {
        string msgHeader = msg.First.ConvertToString();
        Debug.LogFormat("Got message on Client: {0}, {1} frames", msgHeader, msg.FrameCount);
        
        switch(msgHeader.ToString())
        {
            case MSG_S_ConfirmClientJoin:
                SimulateClientInput(client, msg);
                break;
            case MSG_S_FrameData:
                SimulateClientInput(client, msg);
                break;
        }
    }
    
    public void SimulateClientInput(RequestSocket client, NetMQMessage framedDataMsg)
    {
//        ResponseSocket server = GetServerForClient(client);


//#if (UNITY_STANDALONE_WIN)
//        Avatar myAvatar = avatars[server];
//        // Just save out the png data
//        if (framedDataMsg.FrameCount > 1)
//        {
//            for(int i = 0; i < myAvatar.shaders.Count; ++i)
//                CameraStreamer.SaveOutImages(framedDataMsg[1 + i].ToByteArray(), i);
//            CameraStreamer.fileIndex++;
//        }
//#endif

        // Send input message
        lastMessageSent.Clear();
        lastMessageSent.Append(MSG_R_FrameInput);

        // Set movement
        Vector3 targetVelocity = Vector3.zero;
        targetVelocity.x = Input.GetAxis("Horizontal");
        targetVelocity.y = Input.GetAxis("Vertical");
        lastMessageSent.Append(Mathf.RoundToInt(targetVelocity.x * 4096.0f));
        lastMessageSent.Append(Mathf.RoundToInt(targetVelocity.y * 4096.0f));
        lastMessageSent.Append(Mathf.RoundToInt(targetVelocity.z * 4096.0f));
        
        client.SendMultipartMessage(lastMessageSent);
    }
#endregion

    public void SendFrameUpdate(CameraStreamer.CaptureRequest streamCapture)
    {
        lastMessageSent.Clear();
        lastMessageSent.Append(MSG_S_FrameData);
        // TODO: Additional frame message description?
        
        // Add in captured frames
        int numValues = Mathf.Min(streamCapture.shadersList.Count, streamCapture.capturedImages.Count);
        for(int i = 0; i < numValues; ++i)
            lastMessageSent.Append(streamCapture.capturedImages[i].pictureBuffer);
        
        // TODO: Insert other wanted data
        
        // Send out the real message
        // TODO: Look up the correct server socket
        ResponseSocket server = createdSockets[0];
        server.SendMultipartMessage(lastMessageSent);
//        Debug.LogFormat("Sending frame message with {0} frames for {1} values", lastMessageSent.FrameCount, numValues);
    }

    public ResponseSocket GetServerForClient(RequestSocket client)
    {
        foreach(ResponseSocket server in createdSockets)
        {
            if (avatarClients.ContainsKey(server) && avatarClients[server] == client)
                return server;
        }
        return null;
    }
}
