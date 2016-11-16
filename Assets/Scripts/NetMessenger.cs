using UnityEngine;
using System.Collections;
using NetMQ;
using System;
using UnityEngine.UI;
using System.Collections.Generic;
using NetMQ.Sockets;
using LitJson;
using UnityEngine.SceneManagement;
using AsyncIO;

/// <summary>
/// Manages connections with all clients
/// </summary>
public class NetMessenger : MonoBehaviour
{
    #region Fields
    // Template for the avatar to create for each connection
	public Avatar avatarPrefab;
    public string portNumber = "5556";
    public string hostAddress = "192.168.1.106";
    public bool shouldCreateTestClient = false;
    public bool shouldCreateServer = true;
    public bool debugNetworkMessages = false;
    public static bool logTimingInfo = false;
    public static bool logSimpleTimeInfo = false;
    public bool saveDebugImageFiles = false;
    public bool usePngFiles = false;
    public RequestSocket clientSimulation = null;
	public string environmentScene = "Empty";

    private DateTime _timeForLastMsg;
    private NetMQContext _ctx;
    private NetMQMessage _lastMessage = new NetMQMessage();
    private NetMQMessage _lastMessageSent = new NetMQMessage();
    private List<ResponseSocket> _createdSockets = new List<ResponseSocket>();
    private Dictionary<ResponseSocket, Avatar> _avatars = new Dictionary<ResponseSocket, Avatar>();
    private Dictionary<ResponseSocket, RequestSocket> _avatarClients = new Dictionary<ResponseSocket, RequestSocket>();
    private List<SemanticRelationship> _relationsToTest = new List<SemanticRelationship>();
	private List<Scene> scenesToUnload = new List<Scene>();

	// variables for reset scene loading - check out documentation for more details
	private bool skipFrame = false;
	private bool waitForSceneInit = false;
	private JsonData lastJsonContents = null;
	private ResponseSocket lastSocket = null;

    #endregion

    #region Const message values
    // To Send From Server
    const string MSG_S_ConfirmClientJoin = "CLIENT_INIT";
    const string MSG_S_FrameData = "FRAME_UPDATE";

    // To Receive From Client
	const string MSG_R_Terminate = "TERMINATE";
    const string MSG_R_ClientJoin = "CLIENT_JOIN";
    const string MSG_R_FrameInput = "CLIENT_INPUT";
	const string MSG_R_SceneSwitch = "SCENE_SWITCH";
	const string MSG_R_SceneEdit = "CLIENT_SCENE_EDIT";
	const string MSG_R_ClientJoinWithConfig = "CLIENT_JOIN_WITH_CONFIG";
    #endregion

    public List<Avatar> GetAllAvatars()
    {
        List<Avatar> ret = new List<Avatar>(_avatars.Values);
        return ret;
    }

    #region Unity callbacks
    void Start()
    {
		AsyncIO.ForceDotNet.Force ();
        _timeForLastMsg = DateTime.Now;
        _relationsToTest.Add(new OnRelation());
        _relationsToTest.Add (new PushRelation ());
        _relationsToTest.Add (new TouchingRelation ());
        SimulationManager.Init();
    }

	public void Init(string hostAddress, string portNumber, bool shouldCreateTestClient, bool shouldCreateServer, bool debugNetworkMessages,
		bool logSimpleTimingInfo, bool logDetailedTimeInfo, string preferredImageFormat, bool saveDebugImageFiles, string environmentScene)
    {
		this.avatarPrefab = Resources.Load<Avatar>("Prefabs/Avatar");
		if (this.avatarPrefab == null) {
			Debug.Log ("it doesnt exist still!");
		}
		Debug.Log ("May have just printed something right here^");

		Debug.Log (this.avatarPrefab.name);

        // Read port number
		this.portNumber = portNumber;
		this.hostAddress = hostAddress;
		this.shouldCreateTestClient = shouldCreateTestClient;
		this.shouldCreateServer = shouldCreateServer;
		this.debugNetworkMessages = debugNetworkMessages;
		logSimpleTimeInfo = logSimpleTimingInfo;
		logTimingInfo = logDetailedTimeInfo;
		CameraStreamer.preferredImageFormat = preferredImageFormat; // defaults to bmp
        this.saveDebugImageFiles = saveDebugImageFiles; // defaults to False
		this.environmentScene = environmentScene; // defaults to "Empty"

		// Load Environment Scene
		if (shouldCreateServer) {
			SceneManager.LoadScene (environmentScene, LoadSceneMode.Additive);
			if (!SceneManager.GetSceneByName (environmentScene).IsValid()) {
				Debug.LogWarning ("Scene name \"" + environmentScene + "\" was not found.");
			}
		}

        // Start up connections
        _ctx = NetMQContext.Create();
        CreateNewSocketConnection();

		Debug.Log ("Net Messenger Initialized!");
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
		if (!skipFrame) {
			if (!this.waitForSceneInit) {
				if (clientSimulation != null) {
					string output;
					if (clientSimulation.HasIn && clientSimulation.TryReceiveFrameString (out output))
						Debug.LogWarning ("Received: " + output);
					return;
				}
				foreach (ResponseSocket server in _createdSockets) {
					//Debug.LogFormat ("Server In: {0}, Out: {1}", server.HasIn, server.HasOut);
					if (server.HasIn && server.TryReceiveMultipartMessage (TimeSpan.Zero, ref _lastMessage))
						HandleFrameMessage (server, _lastMessage);
					RequestSocket client = null;
					if (_avatarClients.ContainsKey (server))
						client = _avatarClients [server];
					if (client != null) {
						Debug.LogFormat("Client In: {0}, Out: {1}", client.HasIn, client.HasOut);
						if (client.HasIn && client.TryReceiveMultipartMessage (TimeSpan.Zero, ref _lastMessage))
							HandleClientFrameMessage (client, _lastMessage);
					}
				}
			} else {
				foreach (Scene scene in scenesToUnload) {
					Debug.Log ("Unloading: " + scene.name);
					SceneManager.UnloadScene (scene);
				}

				scenesToUnload.Clear ();

				OnClientJoin (lastSocket, lastJsonContents);
				Debug.Log ("Client Join Handled");

				foreach (Avatar a in _avatars.Values)
					a.UpdateObservedObjects();

				this.waitForSceneInit = false;
			}
		} else
			this.skipFrame = false;
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
			Debug.Log("connecting...");
            server.Bind("tcp://" + this.hostAddress + ":" + this.portNumber);
            _createdSockets.Add(server);
			Debug.Log ("...connected@" + this.hostAddress + ":" + this.portNumber);
            if (shouldCreateTestClient)
                CreateTestClient(server);
        }

		/**
        else
        {
            clientSimulation = _ctx.CreateRequestSocket();
            clientSimulation.Connect("tcp://" + this.hostAddress + ":" + this.portNumber);
            clientSimulation.SendFrame(CreateJoinMsgJson().ToJSON());
        }
        */
    }

    private void CreateTestClient(ResponseSocket server)
    {
        RequestSocket client = _ctx.CreateRequestSocket();
        client.Connect("tcp://" + hostAddress + ":" + portNumber);
        _avatarClients[server] = client;
        client.SendFrame(CreateJoinMsgJson().ToJSON());
    }
    #endregion

    #region Receive messages from the client
    public void HandleFrameMessage(ResponseSocket server, NetMQMessage msg)
    {
		Debug.Log ("Handling Message");
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
			case MSG_R_Terminate:
				Application.Quit ();
				break;
            case MSG_R_ClientJoin:
                OnClientJoin(server, jsonData);
				RecieveClientInput(server, jsonData);
                break;
            case MSG_R_FrameInput:
                RecieveClientInput(server, jsonData);
                break;
			case MSG_R_ClientJoinWithConfig:
				Debug.Log ("Config received!");
				ReceiveSceneSwitch (server, jsonData);
				Debug.Log ("Config handled!");
				break;
			case MSG_R_SceneSwitch:
				ReceiveSceneSwitch(server, jsonData);
				break;
            default:
                Debug.LogWarningFormat("Invalid message from client! Unknown msg_type '{0}'\n{1}", msgHeader, jsonData.ToJSON());
                break;
        }
    }

	public void ReceiveSceneSwitch(ResponseSocket server, JsonData jsonData)
	{
		string newEnvironmentScene = jsonData["config"]["environment_scene"].ReadString ("MISSING SCENE TYPE!");

		SimulationManager.setArgsConfig (jsonData["config"]);

		Debug.Log ("Scene config: " + jsonData ["config"].ToJSON ());

                //Debug.Log("Scale Related Dict: " + SimulationManager.argsConfig["scale_relat_dict"]["bed1"]["option"].ToJSON());

		this.scenesToUnload = new List<Scene> ();

		// Unload old scene(s)
		for (int i = 0; i < SceneManager.sceneCount; i++) {
			Scene sceneAtIndex = SceneManager.GetSceneAt (i);
			if (sceneAtIndex.path.StartsWith ("Assets/Scenes/EnvironmentScenes/")) {
				Debug.Log ("Queueing scene unload for: " + sceneAtIndex.name);
				this.scenesToUnload.Add (sceneAtIndex);
			}
		}

		Debug.Log ("Loading \'" + newEnvironmentScene + "\'");

		// Load new scene
		SceneManager.LoadScene (newEnvironmentScene, LoadSceneMode.Additive);
		if (!SceneManager.GetSceneByName (newEnvironmentScene).IsValid()) {
			Debug.LogError ("Scene name \"" + newEnvironmentScene + "\" was not found.");
		}

		if (!SceneManager.GetSceneByName (newEnvironmentScene).path.StartsWith ("Assets/Scenes/EnvironmentScenes/")) {
			Debug.LogError ("Scene is not located in Assets/Scenes/EnvironmentScenes/, please relocate scene file to this directory!");
		}

		SceneManager.sceneLoaded += sceneWasLoaded;

		this.skipFrame = true;
		this.waitForSceneInit = true;

		this.lastSocket = server;
		this.lastJsonContents = jsonData;
	}

	private void sceneWasLoaded(Scene scene, LoadSceneMode mode) {
		this.skipFrame = true;
	}

    public void RecieveClientInput(ResponseSocket server, JsonData jsonData)
    {
        _avatars[server].HandleNetInput(jsonData);
    }

    public void OnClientJoin(ResponseSocket server, JsonData data)
    {
        // Setup new avatar object from prefab
		GameObject newGameObject = Instantiate (Resources.Load ("Prefabs/Avatar")) as GameObject;
		Debug.Log (newGameObject.name);
		Avatar newAvatar = newGameObject.GetComponent<Avatar> ();
        if (_avatars.ContainsKey(server))
        {
            Avatar oldAvatar = _avatars[server];
            if (oldAvatar != null && oldAvatar.gameObject != null)
                GameObject.Destroy(_avatars[server].gameObject);
        }
		_avatars[server] = newAvatar;
		newAvatar.InitNetData(this, server);

		newAvatar.sendSceneInfo = data["send_scene_info"].ReadBool(false);
		newAvatar.shouldCollectObjectInfo = data["get_obj_data"].ReadBool(false);
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

    public void
	SimulateClientInput(RequestSocket client, JsonData jsonData, NetMQMessage msg)
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

	//converts color UID to string for sending to client via message
	public static int colorUIDToInt(Color colorUID) {
		int r = (int)(colorUID.r * 256f);
		int g = (int)(colorUID.g * 256f);
		int b = (int)(colorUID.b * 256f);
		return r * 256 * 256 + g * 256 + b;
	}

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
			SemanticObject[] allObjects = UnityEngine.Object.FindObjectsOfType<SemanticObject>();
			foreach(SemanticObject semObj in allObjects){
				JsonData _info;
				_info = new JsonData(JsonType.Array);
				_info.Add(semObj.gameObject.name);
				Color colorID = semObj.gameObject.GetComponentInChildren<Renderer> ().material.GetColor ("_idval");
				_info.Add(colorUIDToInt(colorID));
				jsonData["sceneInfo"].Add(_info);

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
		//Debug.Log (_lastMessageSent.ToString());
        Debug.LogFormat("Sending frame message with {0} frames for {1} values", _lastMessageSent.FrameCount, numValues);
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
	public static JsonData CreateJoinMsgJson()
	{
		JsonData ret = new JsonData (JsonType.Object);
		ret ["msg_type"] = MSG_R_ClientJoinWithConfig;
		JsonData config = new JsonData (JsonType.Object);
		config["environment_scene"] = "ProceduralGeneration";
		config["random_seed"] = 1; // Omit and it will just choose one at random. Chosen seeds are output into the log(under warning or log level)."should_use_standardized_size": False,
		config["standardized_size"] =  new Vector3(1.0f, 1.0f, 1.0f).ToJson();
		config["disabled_items"] = new JsonData(JsonType.Array); //["SQUIRL", "SNAIL", "STEGOSRS"], // A list of item names to not use, e.g. ["lamp", "bed"] would exclude files with the word "lamp" or "bed" in their file path
		config["permitted_items"] = new JsonData(JsonType.Array); //["bed1", "sofa_blue", "lamp"],
		config["complexity"] = 7500;
		config["num_ceiling_lights"] = 4;
		config["minimum_stacking_base_objects"] = 15;
		config["minimum_objects_to_stack"] = 100;
		config["room_width"] = 40.0f;
		config["room_height"] = 15.0f;
		config["room_length"] = 40.0f;
		config["wall_width"] = 1.0f;
		config["door_width"] = 1.5f;
		config["door_height"] = 3.0f;
		config["window_size_width"] = 5.0f;
		config["window_size_height"] = 5.0f;
		config["window_placement_height"] = 5.0f;
		config["window_spacing"] = 10.0f;  // Average spacing between windows on walls
		config["wall_trim_height"] = 0.5f;
		config["wall_trim_thickness"] = 0.01f;
		config["min_hallway_width"] = 5.0f;
		config["number_rooms"] = 1;
		config["max_wall_twists"] = 3;
		config["max_placement_attempts"] = 300;   // Maximum number of failed placements before we consider a room fully filled.
		config["grid_size"] = 0.4f;    // Determines how fine tuned a grid the objects are placed on during Proc. Gen. Smaller the number, the

		ret["config"] = config;
		return ret;
	}
}
