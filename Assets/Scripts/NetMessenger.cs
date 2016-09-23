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
/// Manages connections with all clients.
/// </summary>
public class NetMessenger : MonoBehaviour
{
    #region Fields
    private string portNumber;
	private string hostAddress;
    private bool debugNetworkMessages;
    public bool logTimeInfo;

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
	//TODO: Implement Scene editting!
	const string MSG_R_SceneEdit = "CLIENT_SCENE_EDIT";
	const string MSG_R_ClientJoinWithConfig = "CLIENT_JOIN_WITH_CONFIG";
    #endregion

    #region Unity callbacks
    void Start()
    {
		AsyncIO.ForceDotNet.Force ();
        _timeForLastMsg = DateTime.Now;
		//TODO: if we want semantic relations, we will need to optimize the semantic relations code!
        //_relationsToTest.Add(new OnRelation());
        //_relationsToTest.Add (new PushRelation ());
        //_relationsToTest.Add (new TouchingRelation ());
        SimulationManager.Init();
    }

	/// <summary>
	/// Called after avatars have been prepped, either waits for a scene to finish loading or attempts a zmq rep req cycle.
	/// </summary>
	void Update()
	{
		if (!skipFrame) {
			if (!this.waitForSceneInit) {
				foreach (ResponseSocket server in _createdSockets) {
					//Debug.LogFormat ("Server In: {0}, Out: {1}", server.HasIn, server.HasOut);
					if (server.HasIn && server.TryReceiveMultipartMessage (TimeSpan.Zero, ref _lastMessage))
						HandleFrameMessage (server, _lastMessage);
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

	/// <summary>
	/// Runs specified num Physics Frames (gets called till FinishUpdatingFrames), reevaluates observable objects, relationships, and preps for taking snapshots.
	/// </summary>
	/// <seealso cref="SimulationManager.FinishUpdatingFrames">Determines how many calls to Fixed Update are run (one physics frame is processed per fixed update).</seealso>
	private void FixedUpdate()
	{
		// TODO: Handle this for when we have multiple Avatars
		if (SimulationManager.FinishUpdatingFrames())
		{
			if (logTimeInfo)
				Debug.LogFormat("Start FinishUpdatingFrames() {0}", Utils.GetTimeStamp());
			HashSet<SemanticObject> allObserved = new HashSet<SemanticObject>();
			HashSet<string> relationshipsActive = new HashSet<string>();
			foreach(Avatar a in _avatars.Values)
			{
				a.UpdateObservedObjects();
				allObserved.UnionWith(a.observedObjs);
				relationshipsActive.UnionWith(a.relationshipsToRetrieve);
			}
			if (logTimeInfo)
				Debug.LogFormat("Finished find avatar observed objects {0}", Utils.GetTimeStamp());

			// Process all the relation changes
			bool hasAll = relationshipsActive.Contains("ALL");
			foreach(SemanticRelationship rel in _relationsToTest)
			{
				if (hasAll || relationshipsActive.Contains(rel.name))
					rel.Setup(allObserved);
			}
			if (logTimeInfo)
				Debug.LogFormat("Finished relationships setup {0}", Utils.GetTimeStamp());

			foreach(Avatar a in _avatars.Values)
				a.ReadyFramesForRequest();
			if (logTimeInfo)
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
	/// <summary>
	/// Init the Net Messenger bound at the given host address and port number.
	/// </summary>
	/// <param name="hostAddress">Host address.</param>
	/// <param name="portNumber">Port number.</param>
	/// <param name="debugNetworkMessages">If set to <c>true</c> debug network messages.</param>
	/// <param name="logTimeInfo">If set to <c>true</c> log time info.</param>
	/// <param name="preferredImageFormat">Preferred image format.</param>
	/// <param name="saveDebugImageFiles">If set to <c>true</c> save debug image files.</param>
	/// <param name="environmentScene">Environment scene.</param>
	public void Init(string hostAddress, string portNumber, bool debugNetworkMessages, bool logTimeInfo,
		string preferredImageFormat, string environmentScene)
	{
		// Read port number
		this.portNumber = portNumber;
		this.hostAddress = hostAddress;
		this.debugNetworkMessages = debugNetworkMessages;
		this.logTimeInfo = logTimeInfo;
		CameraStreamer.preferredImageFormat = preferredImageFormat; // defaults to png

		// Load Environment Scene
		SceneManager.LoadScene (environmentScene, LoadSceneMode.Additive);
		if (!SceneManager.GetSceneByName (environmentScene).IsValid())
			Debug.LogErrorFormat ("Scene name \'{0}\' was not found.", environmentScene);

		// Start up connections
		_ctx = NetMQContext.Create();
		//TODO:currently only ever makes one server, this structure should either make multiple servers for each client, 
		//     or figure out how to have multiple clients on the same server and scrap the list of servers.
		CreateServerConnection();

		Debug.Log ("Net Messenger Initialized!");
	}

	public List<Avatar> GetAllAvatars()
	{
		List<Avatar> ret = new List<Avatar>(_avatars.Values);
		return ret;
	}

	public bool AreAllAvatarsReady()
	{
		bool allReady = true;
		foreach(Avatar a in _avatars.Values)
			allReady = allReady && a.readyForSimulation;
		return allReady;
	}

	private void CreateServerConnection()
    {
        ResponseSocket server = _ctx.CreateResponseSocket();
		Debug.Log("connecting...");
        server.Bind("tcp://" + this.hostAddress + ":" + this.portNumber);
        _createdSockets.Add(server);
		Debug.Log ("...connected@" + this.hostAddress + ":" + this.portNumber);
    }
    #endregion

    #region Receive messages from the client
    public void HandleFrameMessage(ResponseSocket server, NetMQMessage msg)
    {
		Debug.Log ("Handling Message");
        if (logTimeInfo)
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

	/// <summary>
	/// Triggers whenever a scene is loaded, forces.
	/// </summary>
	/// <param name="scene">Scene.</param>
	/// <param name="mode">Mode.</param>
	private void sceneWasLoaded(Scene scene, LoadSceneMode mode) {
		this.skipFrame = true;
	}

	/// <summary>
	/// Handles receiving a CLIENT_INPUT
	/// </summary>
	/// <param name="server">Server.</param>
	/// <param name="jsonData">Json data.</param>
    public void RecieveClientInput(ResponseSocket server, JsonData jsonData)
    {
        _avatars[server].HandleNetInput(jsonData);
    }

	/// <summary>
	/// Handles receiving a CLIENT_JOIN or CLIENT_JOIN_WITH_CONFIG and the generation of a new client and avatar.
	/// </summary>
	/// <param name="server">Server.</param>
	/// <param name="data">Json message contents.</param>
    public void OnClientJoin(ResponseSocket server, JsonData data)
    {
		//TODO:Restructure Resources/Prefabs filestructure once avatar is generalized to put all avatar prefabs in a folder
		//	   and allow for client joins to specify what kind of avatar they want to use via the json message.
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
    }
			
	/// <summary>
	/// Reads the frame to a string.
	/// </summary>
	/// <returns>The frame as a string.</returns>
	/// <param name="frame">Frame.</param>
	static private string ReadOutFrame(NetMQFrame frame)
	{
		string test = null;
		if (frame.BufferSize == 4)
			test = BitConverter.ToInt32(frame.Buffer, 0).ToString();
		else if (frame.BufferSize == 8)
			test = BitConverter.ToInt64(frame.Buffer, 0).ToString();
		else if (frame.BufferSize > 5000)
			test = "PNG " + frame.BufferSize;
		else
			test = frame.ConvertToString(System.Text.Encoding.ASCII);
		return test;
	}

	/// <summary>
	/// Reads the json message to a string.
	/// </summary>
	/// <returns>The out message.</returns>
	/// <param name="msg">Json message.</param>
	static public string ReadOutMessage(NetMQMessage msg)
	{
		string output = string.Format("({0} frames)", msg.FrameCount);
		for(int i = 0; i < msg.FrameCount; ++i)
		{
			output += string.Format("\n{0}: \"{1}\"", i, ReadOutFrame(msg[i]));
		}
		return output;
	}
    #endregion

	#region Send messages to the client
	/// <summary>
	/// Converts a color UID to a string concat of hexadecimal values of the rgb channels.
	/// </summary>
	/// <returns>The UID as a string.</returns>
	/// <param name="colorUID">Color UID.</param>
	public static string colorUIDToString(Color colorUID) {
		int r = (int)(colorUID.r * 256f);
		int g = (int)(colorUID.g * 256f);
		int b = (int)(colorUID.b * 256f);
		return r.ToString ("x2") + g.ToString ("x2") + b.ToString ("x2");
	}

	/// <summary>
	/// Send a message to the client with scene metadata and generated images.
	/// </summary>
	/// <param name="streamCapture">Stream capture.</param>
	/// <param name="a">Avatar for target client.</param>
    public void SendFrameUpdate(CameraStreamer.CaptureRequest streamCapture, Avatar a)
	{
        if (logTimeInfo)
            Debug.LogFormat("Start SendFrameUpdate() {0} {1}", a.name, Utils.GetTimeStamp());
        _lastMessageSent.Clear();
        JsonData jsonData = CreateMsgJson(MSG_S_FrameData);
        // TODO: Additional frame message description?

		//TODO: If semantic relationship code is ever used again, implement something akin to this for reporting it in the metadata
		/*
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
        */

        jsonData["avatar_position"] = a.transform.position.ToJson();
        jsonData["avatar_rotation"] = a.transform.rotation.ToJson();

		//add initial scene info
		if (a.sendSceneInfo) {
            jsonData["scene_info"] = new JsonData(JsonType.Array);
			SemanticObject[] allObjects = UnityEngine.Object.FindObjectsOfType<SemanticObject>();
			foreach(SemanticObject semObj in allObjects){
				JsonData _info;
				_info = new JsonData(JsonType.Array);
				_info.Add(semObj.gameObject.name);
				Color colorID = semObj.gameObject.GetComponentInChildren<Renderer> ().material.GetColor ("_idval");
				_info.Add(colorUIDToString(colorID));
				jsonData["sceneInfo"].Add(_info);

	    	}
	    }

        if (logTimeInfo)
            Debug.LogFormat("Finished collect Json data {0}", Utils.GetTimeStamp());
        // Send out the real message
        string jsonString = LitJson.JsonMapper.ToJson(jsonData);
        _lastMessageSent.Append(jsonString);
        if (logTimeInfo)
            Debug.LogFormat("Finished encode json data of length {1}, {0}", Utils.GetTimeStamp(), jsonString.Length);

        // Add in captured frames(directly, non-JSON)
        int numValues = Mathf.Min(streamCapture.shadersList.Count, streamCapture.capturedImages.Count);
        for(int i = 0; i < numValues; ++i)
            _lastMessageSent.Append(streamCapture.capturedImages[i].pictureBuffer);
        if (logTimeInfo)
            Debug.LogFormat("Finished Encode Image data {0}", Utils.GetTimeStamp());

		a.myServer.SendMultipartMessage(_lastMessageSent);
		//Debug.Log (_lastMessageSent.ToString());
        Debug.LogFormat("Sending frame message with {0} frames for {1} values", _lastMessageSent.FrameCount, numValues);
        if (logTimeInfo)
            Debug.LogFormat("Finish SendFrameUpdate() {0} {1}", a.name, Utils.GetTimeStamp());
    }

	/// <summary>
	/// Creates a json message with a msg type header.
	/// </summary>
	/// <returns>The message json.</returns>
	/// <param name="msgType">Message type.</param>
    public static JsonData CreateMsgJson(string msgType)
    {
        JsonData ret = new JsonData(JsonType.Object);
        ret["msg_type"] = msgType;
        return ret;
    }
	#endregion
}