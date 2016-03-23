using UnityEngine;
using System.Collections;
using NetMQ;
using System;
using UnityEngine.UI;
using System.Collections.Generic;
using NetMQ.Sockets;
using LitJson;

/// <summary>
/// Manages connections with all clients
/// </summary>
public class NetMessenger : MonoBehaviour
{
    #region Fields
    // Template for the avatar to create for each connection
    public Avatar avatarPrefab;
    public string portNumber = "5556";
    public string hostAddress = "*";
    public bool shouldCreateTestClient = false;
    public bool shouldCreateServer = true;
    public bool debugNetworkMessages = false;
    public static bool logTimingInfo = false;
    public static bool logSimpleTimeInfo = false;
    public bool saveDebugImageFiles = false;
    public bool usePngFiles = false;
    public RequestSocket clientSimulation = null;

    private DateTime _timeForLastMsg;
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
        _timeForLastMsg = DateTime.Now;
        _relationsToTest.Add(new OnRelation());
        _relationsToTest.Add (new PushRelation ());
        _relationsToTest.Add (new TouchingRelation ());
        SimulationManager.Init();
    }

    public void Init()
    {
        // Read port number
        portNumber = SimulationManager.argsConfig["port_number"].ReadString(portNumber);
        hostAddress = SimulationManager.argsConfig["host_address"].ReadString(hostAddress);
        shouldCreateTestClient = SimulationManager.argsConfig["create_test_client"].ReadBool(shouldCreateTestClient);
        shouldCreateServer = SimulationManager.argsConfig["create_server"].ReadBool(shouldCreateServer);
        debugNetworkMessages = SimulationManager.argsConfig["debug_network_messages"].ReadBool(debugNetworkMessages);
        logSimpleTimeInfo = SimulationManager.argsConfig["log_timing_info"].ReadBool(logSimpleTimeInfo);
        logTimingInfo = SimulationManager.argsConfig["log_detailed_timing_info"].ReadBool(logTimingInfo);
        CameraStreamer.preferredImageFormat = SimulationManager.argsConfig["image_format"].ReadString("bmp");
        saveDebugImageFiles = SimulationManager.argsConfig["save_out_debug_image_files"].ReadBool(false);

        // Create Procedural generation
        if (ProceduralGeneration.Instance == null && shouldCreateServer)
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
        if (clientSimulation != null)
        {
            string output;
            if (clientSimulation.HasIn && clientSimulation.TryReceiveFrameString(out output))
                Debug.LogWarning("Received: " + output);
            return;
        }
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
            if (logTimingInfo)
                Debug.LogFormat("Start FinishUpdatingFrames() {0}", Utils.GetTimeStamp());
            HashSet<SemanticObject> allObserved = new HashSet<SemanticObject>();
            HashSet<string> relationshipsActive = new HashSet<string>();
            foreach(Avatar a in _avatars.Values)
            {
                a.UpdateObservedObjects();
                allObserved.UnionWith(a.observedObjs);
                relationshipsActive.UnionWith(a.relationshipsToRetrieve);
            }
            if (logTimingInfo)
                Debug.LogFormat("Finished find avatar observed objects {0}", Utils.GetTimeStamp());

            // Process all the relation changes
            bool hasAll = relationshipsActive.Contains("ALL");
            foreach(SemanticRelationship rel in _relationsToTest)
            {
                if (hasAll || relationshipsActive.Contains(rel.name))
                    rel.Setup(allObserved);
            }
            if (logTimingInfo)
                Debug.LogFormat("Finished relationships setup {0}", Utils.GetTimeStamp());

            foreach(Avatar a in _avatars.Values)
                a.ReadyFramesForRequest();
            if (logTimingInfo)
                Debug.LogFormat("Finished FinishUpdatingFrames() {0}", Utils.GetTimeStamp());
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
        if (clientSimulation != null)
        {
            clientSimulation.Close();
            clientSimulation.Dispose();
            clientSimulation = null;
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
        if (shouldCreateServer)
        {
            server.Bind("tcp://" + hostAddress + ":" + portNumber);
            _createdSockets.Add(server);
            if (shouldCreateTestClient)
                CreateTestClient(server);
        }
        else
        {
            clientSimulation = _ctx.CreateRequestSocket();
            clientSimulation.Connect("tcp://" + hostAddress + ":" + portNumber);
            clientSimulation.SendFrame(CreateMsgJson(MSG_R_ClientJoin).ToJSON());
        }
    }

    private void CreateTestClient(ResponseSocket server)
    {
        RequestSocket client = _ctx.CreateRequestSocket();
        client.Connect("tcp://" + hostAddress + ":" + portNumber);
        _avatarClients[server] = client;
        client.SendFrame(CreateMsgJson(MSG_R_ClientJoin).ToJSON());
    }
    #endregion

    #region Receive messages from the client
    public void HandleFrameMessage(ResponseSocket server, NetMQMessage msg)
    {
        if (logTimingInfo || logSimpleTimeInfo)
        {
            DateTime newTime = DateTime.Now;
            Debug.LogFormat("Time since received last msg: {0} ms", newTime.Subtract(_timeForLastMsg).TotalMilliseconds);
            _timeForLastMsg = newTime;
        }
        if (debugNetworkMessages)
            Debug.LogFormat("Received Msg on Server: {0}", ReadOutMessage(msg));
        string msgHeader = msg.First.ConvertToString();
        JsonData jsonData = msg.ReadJson(out msgHeader);
        if (jsonData == null)
        {
            Debug.LogError("Invalid message from client! Cannot parse JSON!\n" + ReadOutMessage(msg));
            return;
        }
        if (msgHeader == null)
        {
            Debug.LogError("Invalid message from client! No msg_type!\n" + jsonData.ToJSON());
            return;
        }

        switch(msgHeader.ToString())
        {
            case MSG_R_ClientJoin:
                OnClientJoin(server, jsonData);
                break;
            case MSG_R_FrameInput:
                RecieveClientInput(server, jsonData);
                break;
            default:
                Debug.LogWarningFormat("Invalid message from client! Unknown msg_type '{0}'\n{1}", msgHeader, jsonData.ToJSON());
                break;
        }
    }

    public void RecieveClientInput(ResponseSocket server, JsonData jsonData)
    {
        _avatars[server].HandleNetInput(jsonData);
    }

    public void OnClientJoin(ResponseSocket server, JsonData data)
    {
        // Setup new avatar object from prefab
        Avatar newAvatar = UnityEngine.Object.Instantiate<Avatar>(avatarPrefab);
        if (_avatars.ContainsKey(server))
        {
            Avatar oldAvatar = _avatars[server];
            if (oldAvatar != null && oldAvatar.gameObject != null)
                GameObject.Destroy(_avatars[server].gameObject);
        }
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
        if (debugNetworkMessages)
            Debug.LogFormat("Received Msg on Client: {0}", ReadOutMessage(msg));
        string msgHeader = msg.First.ConvertToString();

        // Hack to avoid slow parsing of long json values since we're not reading it anyways
        JsonData jsonData = null;
        if (msg.FrameCount > 0)
        {
            string jsonString = msg[0].ConvertToString();
            if (jsonString != null && jsonString.Length > 1000)
            {
                msgHeader = MSG_S_FrameData;
                jsonData = CreateMsgJson(msgHeader);
            }
            else
                jsonData = msg.ReadJson(out msgHeader);
        }

        if (jsonData == null)
        {
            Debug.LogError("Invalid message from server! Cannot parse JSON!\n" + ReadOutMessage(msg));
            return;
        }
        if (msgHeader == null)
        {
            Debug.LogError("Invalid message from server! No msg_type!\n" + jsonData.ToJSON());
            return;
        }

        switch(msgHeader.ToString())
        {
            case MSG_S_ConfirmClientJoin:
                SimulateClientInput(client, jsonData, msg);
                break;
            case MSG_S_FrameData:
                SimulateClientInput(client, jsonData, msg);
                break;
            default:
                Debug.LogWarningFormat("Invalid message from server! Unknown msg_type '{0}'\n{1}", msgHeader, jsonData.ToJSON());
                break;
        }
    }

    static private string ReadOutFrame(NetMQFrame frame)
    {
        string test = null;
        if (frame.BufferSize == 4)
            test = BitConverter.ToInt32(frame.Buffer, 0).ToString();
        else if (frame.BufferSize == 8)
            test = BitConverter.ToInt64(frame.Buffer, 0).ToString();
        //            else if (msg[i].BufferSize > 800000)
        else if (frame.BufferSize > 5000)
            test = "PNG " + frame.BufferSize;
        else
            test = frame.ConvertToString(System.Text.Encoding.ASCII);
        return test;
    }

    static public string ReadOutMessage(NetMQMessage msg)
    {
        string output = string.Format("({0} frames)", msg.FrameCount);
        for(int i = 0; i < msg.FrameCount; ++i)
        {
            output += string.Format("\n{0}: \"{1}\"", i, ReadOutFrame(msg[i]));
        }
        return output;
    }

    public void SimulateClientInput(RequestSocket client, JsonData jsonData, NetMQMessage msg)
    {
        ResponseSocket server = GetServerForClient(client);
        Avatar myAvatar = _avatars[server];

        if (saveDebugImageFiles)
        {
            // Just save out the png data to the local filesystem(Debugging code only)
            if (msg.FrameCount > 1)
            {
                for(int i = 0; i < myAvatar.shaders.Count; ++i)
                    Debug.LogFormat("Saving out: {0}", CameraStreamer.SaveOutImages(msg[msg.FrameCount + i - myAvatar.shaders.Count].ToByteArray(), i));
                CameraStreamer.fileIndex++;
            }
        }

        // Send input message
        JsonData msgData = CreateMsgJson(MSG_R_FrameInput);
        myAvatar.myInput.SimulateInputFromController(ref msgData);
        _lastMessageSent.Clear();
        _lastMessageSent.Append(msgData.ToJSON());
        client.SendMultipartMessage(_lastMessageSent);
    }
    #endregion
    public void SendFrameUpdate(CameraStreamer.CaptureRequest streamCapture, Avatar a)
    {
        if (logTimingInfo)
            Debug.LogFormat("Start SendFrameUpdate() {0} {1}", a.name, Utils.GetTimeStamp());
        _lastMessageSent.Clear();
        JsonData jsonData = CreateMsgJson(MSG_S_FrameData);
        // TODO: Additional frame message description?

        if (a.shouldCollectObjectInfo)
        {
            // Look up relationship values for all observed semantics objects
            jsonData["observed_objects"] = new JsonData(JsonType.Array);
            foreach(SemanticObject o in a.observedObjs)
                jsonData["observed_objects"].Add(o.identifier);
            jsonData["observed_relations"] = new JsonData(JsonType.Object);
            bool collectAllRelationships = a.relationshipsToRetrieve.Contains("ALL");
            foreach(SemanticRelationship rel in _relationsToTest)
            {
                if (collectAllRelationships || a.relationshipsToRetrieve.Contains(rel.name))
                    jsonData["observed_relations"][rel.name] = rel.GetJsonString(a.observedObjs);
            }
        }
        jsonData["avatar_position"] = a.transform.position.ToJson();
        jsonData["avatar_rotation"] = a.transform.rotation.ToJson();
//        // Add in captured frames
//        int numValues = Mathf.Min(streamCapture.shadersList.Count, streamCapture.capturedImages.Count);
//        JSONArray imagesArray = new JsonData(JsonType.Array);
//        for(int i = 0; i < numValues; ++i)
//            imagesArray.Add(new JSONData(Convert.ToBase64String(streamCapture.capturedImages[i].pictureBuffer)));
//        jsonData["captured_pngs"] = imagesArray;

		//add initial scene info
		if (a.sendSceneInfo) {
            jsonData["sceneInfo"] = new JsonData(JsonType.Array);
			GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>() ;
			foreach(GameObject go in allObjects){
			   if (go.GetComponent<Renderer>() != null) {
                    JsonData _info;
                    _info = new JsonData(JsonType.Array);
				   _info.Add(go.name);
				   _info.Add(go.GetComponent<Renderer>().material.GetInt("_idval").ToString());
				   jsonData["sceneInfo"].Add(_info);
			   }
	    	}
	    }
	    
        if (logTimingInfo)
            Debug.LogFormat("Finished collect Json data {0}", Utils.GetTimeStamp());
        // Send out the real message
        string jsonString = LitJson.JsonMapper.ToJson(jsonData);
        _lastMessageSent.Append(jsonString);
        if (logTimingInfo)
            Debug.LogFormat("Finished encode json data of length {1}, {0}", Utils.GetTimeStamp(), jsonString.Length);

        // Add in captured frames(directly, non-JSON)
        int numValues = Mathf.Min(streamCapture.shadersList.Count, streamCapture.capturedImages.Count);
        for(int i = 0; i < numValues; ++i)
            _lastMessageSent.Append(streamCapture.capturedImages[i].pictureBuffer);
        if (logTimingInfo)
            Debug.LogFormat("Finished Encode Image data {0}", Utils.GetTimeStamp());

        a.myServer.SendMultipartMessage(_lastMessageSent);
//        Debug.LogFormat("Sending frame message with {0} frames for {1} values", lastMessageSent.FrameCount, numValues);
        if (logTimingInfo)
            Debug.LogFormat("Finish SendFrameUpdate() {0} {1}", a.name, Utils.GetTimeStamp());
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

    public static JsonData CreateMsgJson(string msgType)
    {
        JsonData ret = new JsonData(JsonType.Object);
        ret["msg_type"] = msgType;
        return ret;
    }
}