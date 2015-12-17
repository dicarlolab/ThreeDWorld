using UnityEngine;
using System.Collections;
using NetMQ;
using System;
using UnityEngine.UI;
using System.Collections.Generic;
using NetMQ.Sockets;
using SimpleJSON;

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
    private List<SemanticRelationship> relationsToTest = new List<SemanticRelationship>();
#endregion

#region Const message values
    // To Send From Server
    const string MSG_S_ConfirmClientJoin = "CLIENT_INIT";
    const string MSG_S_FrameData = "FRAME_UPDATE";

    // To Receive From Client
    const string MSG_R_ClientJoin = "CLIENT_JOIN";
    const string MSG_R_FrameInput = "CLIENT_INPUT";
#endregion

    public List<Avatar> GetAllAvatars()
    {
        List<Avatar> ret = new List<Avatar>(avatars.Values);
        return ret;
    }

#region Unity callbacks
    void Start()
    {
        relationsToTest.Add(new OnRelation());
        SimulationManager.Init();
    }

    public void Init()
    {
        ctx = NetMQContext.Create();
        CreateNewSocketConnection();
    }

    public bool AreAllAvatarsReady()
    {
        bool allReady = true;
        foreach(Avatar a in avatars.Values)
            allReady = allReady && a.readyForSimulation;
        return allReady;
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
            HashSet<SemanticObject> allObserved = new HashSet<SemanticObject>();
            foreach(Avatar a in avatars.Values)
            {
                a.UpdateObservedObjects();
                allObserved.UnionWith(a.observedObjs);
            }

            // Process all the relation changes
            foreach(SemanticRelationship rel in relationsToTest)
                rel.Setup(allObserved);

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
        ResponseSocket server = GetServerForClient(client);
        Avatar myAvatar = avatars[server];

//        if (framedDataMsg.FrameCount > 1)
//            Debug.Log("Received JSON: "+framedDataMsg[1].ConvertToString());

//#if (UNITY_STANDALONE_WIN)
//        // Just save out the png data
//        if (framedDataMsg.FrameCount > 2)
//        {
//            for(int i = 0; i < myAvatar.shaders.Count; ++i)
//                CameraStreamer.SaveOutImages(framedDataMsg[2 + i].ToByteArray(), i);
//            CameraStreamer.fileIndex++;
//        }
//#endif

        // Send input message
        lastMessageSent.Clear();
        lastMessageSent.Append(MSG_R_FrameInput);

        // Set movement
        Quaternion curRotation = myAvatar.transform.rotation;
        Vector3 targetVelocity = Vector3.zero;
        targetVelocity.x = Input.GetAxis("Horizontal");
        targetVelocity.y = Input.GetAxis("Vertical");
        targetVelocity.z = Input.GetAxis("VerticalD");
        targetVelocity = curRotation * targetVelocity;

        // Read angular velocity
        Vector3 targetRotationVel = Vector3.zero;
        targetRotationVel.x = -Input.GetAxis("Vertical2");
        targetRotationVel.y = Input.GetAxis("Horizontal2");
        targetRotationVel.z = -Input.GetAxis("HorizontalD");

        // Convert from relative coordinates
        Quaternion test = Quaternion.identity;
        test = test * Quaternion.AngleAxis(targetRotationVel.z, curRotation * Vector3.forward);
        test = test * Quaternion.AngleAxis(targetRotationVel.x, curRotation * Vector3.left);
        test = test * Quaternion.AngleAxis(targetRotationVel.y, curRotation * Vector3.up);
        targetRotationVel = test.eulerAngles;

        lastMessageSent.Append(Mathf.RoundToInt(targetVelocity.x * 4096.0f));
        lastMessageSent.Append(Mathf.RoundToInt(targetVelocity.y * 4096.0f));
        lastMessageSent.Append(Mathf.RoundToInt(targetVelocity.z * 4096.0f));
        lastMessageSent.Append(Mathf.RoundToInt(targetRotationVel.x * 4096.0f));
        lastMessageSent.Append(Mathf.RoundToInt(targetRotationVel.y * 4096.0f));
        lastMessageSent.Append(Mathf.RoundToInt(targetRotationVel.z * 4096.0f));

        client.SendMultipartMessage(lastMessageSent);
    }
#endregion
    public void SendFrameUpdate(CameraStreamer.CaptureRequest streamCapture, Avatar a)
    {
        lastMessageSent.Clear();
        lastMessageSent.Append(MSG_S_FrameData);
        // TODO: Additional frame message description?
        
        // Look up relationship values for all observed semantics objects
        JSONClass retInfo = new JSONClass();
        retInfo["observed_objects"] = new JSONArray();
        retInfo["observed_relations"] = new JSONClass();
        foreach(SemanticObject o in a.observedObjs)
            retInfo["observed_objects"].Add(o.identifier);
        foreach(SemanticRelationship rel in relationsToTest)
            retInfo["observed_relations"][rel.name] = rel.GetJsonString(a.observedObjs);
        // Send out the real message
        lastMessageSent.Append(retInfo.ToJSON(0));

        // Add in captured frames
        int numValues = Mathf.Min(streamCapture.shadersList.Count, streamCapture.capturedImages.Count);
        for(int i = 0; i < numValues; ++i)
            lastMessageSent.Append(streamCapture.capturedImages[i].pictureBuffer);
        
        a.myServer.SendMultipartMessage(lastMessageSent);
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
