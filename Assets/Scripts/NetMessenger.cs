using UnityEngine;
using System.Collections;
using NetMQ;
using System;
using UnityEngine.UI;
using System.Collections.Generic;
using NetMQ.Sockets;
using SimpleJSON;

/// <summary>
/// Manages connections with all clients
/// </summary>
public class NetMessenger : MonoBehaviour
{
#region Fields
    // Template for the avatar to create for each connection
    public Avatar avatarPrefab;
    public bool shouldCreateTestClient = true;

    private NetMQContext _ctx;
    private NetMQMessage _lastMessage = new NetMQMessage();
    private NetMQMessage _lastMessageSent = new NetMQMessage();
    private List<ResponseSocket> _createdSockets = new List<ResponseSocket>();
    private Dictionary<ResponseSocket, Avatar> _avatars = new Dictionary<ResponseSocket, Avatar>();
    private Dictionary<ResponseSocket, RequestSocket> _avatarClients = new Dictionary<ResponseSocket, RequestSocket>();
    private List<SemanticRelationship> _relationsToTest = new List<SemanticRelationship>();
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
        List<Avatar> ret = new List<Avatar>(_avatars.Values);
        return ret;
    }

#region Unity callbacks
    void Start()
    {
        _relationsToTest.Add(new OnRelation());
		_relationsToTest.Add (new PushRelation ());
		_relationsToTest.Add (new TouchingRelation ());
        SimulationManager.Init();
    }

    public void Init()
    {
        // Create Procedural generation
        if (ProceduralGeneration.Instance == null)
            GameObject.Instantiate(Resources.Load("Prefabs/ProceduralGeneration"));

        // Start up connections
        _ctx = NetMQContext.Create();
        CreateNewSocketConnection();
    }

    public bool AreAllAvatarsReady()
    {
        bool allReady = true;
        foreach(Avatar a in _avatars.Values)
            allReady = allReady && a.readyForSimulation;
        return allReady;
    }
    
    void Update()
    {
        foreach(ResponseSocket server in _createdSockets)
        {
//            Debug.LogFormat("Server In: {0}, Out: {1}", server.HasIn, server.HasOut);
            if (server.HasIn && server.TryReceiveMultipartMessage(TimeSpan.Zero, ref _lastMessage))
                HandleFrameMessage(server, _lastMessage);
            RequestSocket client = null;
            if (_avatarClients.ContainsKey(server))
                client = _avatarClients[server];
            if (client != null)
            {
//                Debug.LogFormat("Client In: {0}, Out: {1}", client.HasIn, client.HasOut);
                if (client.HasIn && client.TryReceiveMultipartMessage(TimeSpan.Zero, ref _lastMessage))
                    HandleClientFrameMessage(client, _lastMessage);
                
            }
        }
    }

    private void FixedUpdate()
    {
        // TODO: Handle this for when we have multiple Avatars
        if (SimulationManager.FinishUpdatingFrames())
        {
            HashSet<SemanticObject> allObserved = new HashSet<SemanticObject>();
            foreach(Avatar a in _avatars.Values)
            {
                a.UpdateObservedObjects();
                allObserved.UnionWith(a.observedObjs);
            }

            // Process all the relation changes
            foreach(SemanticRelationship rel in _relationsToTest)
                rel.Setup(allObserved);

            foreach(Avatar a in _avatars.Values)
                a.ReadyFramesForRequest();
        }
    }

    private void OnDisable()
    {
        foreach(ResponseSocket server in _createdSockets)
        {
            if (_avatarClients.ContainsKey(server))
            {
                _avatarClients[server].Close();
                _avatarClients[server].Dispose();
            }
            server.Close();
            server.Dispose();
            if (_avatars.ContainsKey(server))
            {
                Avatar avatar = _avatars[server];
                if (avatar != null && avatar.gameObject != null)
                    GameObject.Destroy(_avatars[server].gameObject);
            }
        }
        _avatars.Clear();
        _createdSockets.Clear();
        _avatarClients.Clear();
        if (_ctx != null)
        {
            _ctx.Terminate();
            _ctx.Dispose();
            _ctx = null;
        }
    }
#endregion

#region Setup
    private void CreateNewSocketConnection()
    {
        ResponseSocket server = _ctx.CreateResponseSocket();
        server.Bind("tcp://127.0.0.1:5556");
        _createdSockets.Add(server);
        if (shouldCreateTestClient)
            CreateTestClient(server);
    }

    private void CreateTestClient(ResponseSocket server)
    {
        RequestSocket client = _ctx.CreateRequestSocket();
        client.Connect("tcp://127.0.0.1:5556");
        _avatarClients[server] = client;
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
        _avatars[server].HandleNetInput(msg);
    }

    public void OnClientJoin(ResponseSocket server, NetMQMessage msg)
    {
        // Setup new avatar object from prefab
        Avatar newAvatar = UnityEngine.Object.Instantiate<Avatar>(avatarPrefab);
        _avatars[server] = newAvatar;
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
        Avatar myAvatar = _avatars[server];

        if (framedDataMsg.FrameCount > 1)
            Debug.Log("Received JSON: "+framedDataMsg[1].ConvertToString());

//#if (UNITY_STANDALONE_WIN)
//        // Just save out the png data to the local filesystem(Debugging code only)
//        if (framedDataMsg.FrameCount > 2)
//        {
//            for(int i = 0; i < myAvatar.shaders.Count; ++i)
//                CameraStreamer.SaveOutImages(framedDataMsg[2 + i].ToByteArray(), i);
//            CameraStreamer.fileIndex++;
//        }
//#endif

        // Send input message
        _lastMessageSent.Clear();
        _lastMessageSent.Append(MSG_R_FrameInput);

        myAvatar.myInput.SimulateInputFromController(ref _lastMessageSent);
        client.SendMultipartMessage(_lastMessageSent);
    }
#endregion
    public void SendFrameUpdate(CameraStreamer.CaptureRequest streamCapture, Avatar a)
    {
        _lastMessageSent.Clear();
        _lastMessageSent.Append(MSG_S_FrameData);
        // TODO: Additional frame message description?
        
        // Look up relationship values for all observed semantics objects
        JSONClass retInfo = new JSONClass();
        retInfo["observed_objects"] = new JSONArray();
        retInfo["observed_relations"] = new JSONClass();
        foreach(SemanticObject o in a.observedObjs)
            retInfo["observed_objects"].Add(o.identifier);
        foreach(SemanticRelationship rel in _relationsToTest)
            retInfo["observed_relations"][rel.name] = rel.GetJsonString(a.observedObjs);
        // Send out the real message
        _lastMessageSent.Append(retInfo.ToJSON(0));

        // Add in captured frames
        int numValues = Mathf.Min(streamCapture.shadersList.Count, streamCapture.capturedImages.Count);
        for(int i = 0; i < numValues; ++i)
            _lastMessageSent.Append(streamCapture.capturedImages[i].pictureBuffer);
        
        a.myServer.SendMultipartMessage(_lastMessageSent);
//        Debug.LogFormat("Sending frame message with {0} frames for {1} values", lastMessageSent.FrameCount, numValues);
    }

    public ResponseSocket GetServerForClient(RequestSocket client)
    {
        foreach(ResponseSocket server in _createdSockets)
        {
            if (_avatarClients.ContainsKey(server) && _avatarClients[server] == client)
                return server;
        }
        return null;
    }
}
