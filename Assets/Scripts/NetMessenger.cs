using UnityEngine;
using System.Collections;
using NetMQ;
using System;
using UnityEngine.UI;
using System.Collections.Generic;
using NetMQ.Sockets;
using SimpleJSON;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Text;

/// <summary>
/// Manages connections with all clients
/// </summary>
public class NetMessenger : MonoBehaviour
{
#region Fields
    // Template for the avatar to create for each connection
    public Avatar avatarPrefab;
    public string portNumber = "5556";
    public string hostAddress = "127.0.0.1";
    public bool fakeResponses = true;
    public bool shouldCreateTestClient = false;
    public bool shouldCreateServer = true;
    public bool debugNetworkMessages = false;
    public bool saveDebugImageFiles = false;
    public bool usePngFiles = false;
    public RequestSocket clientSimulation = null;

    private float lastServerMsg = 0.0f;
    private float lastClientMsg = 0.0f;
    private NetMQContext _ctx;
    private NetMQMessage _lastMessage = new NetMQMessage();
    private NetMQMessage _lastMessageSent = new NetMQMessage();
    private NetMQMessage _debugMessage = new NetMQMessage();
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

    const string shortString = "12";
    const string longString = "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890";
    const string longerString = "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890";
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
        // Read port number
        portNumber = SimulationManager.argsConfig["port_number"].ReadString(portNumber);
        hostAddress = SimulationManager.argsConfig["host_address"].ReadString(hostAddress);
        shouldCreateTestClient = SimulationManager.argsConfig["create_test_client"].ReadBool(shouldCreateTestClient);
        shouldCreateServer = SimulationManager.argsConfig["create_server"].ReadBool(shouldCreateServer);
        debugNetworkMessages = SimulationManager.argsConfig["debug_network_messages"].ReadBool(debugNetworkMessages);
        CameraStreamer.preferredImageFormat = SimulationManager.argsConfig["image_format"].ReadString("bmp");
        saveDebugImageFiles = SimulationManager.argsConfig["save_out_debug_image_files"].ReadBool(false);

//        // Create Procedural generation
//        if (ProceduralGeneration.Instance == null && shouldCreateServer)
//            GameObject.Instantiate(Resources.Load("Prefabs/ProceduralGeneration"));

        // Start up connections
//        _ctx = NetMQContext.Create();
        CreateNewSocketConnection2();
//        CreateNewSocketConnection();
    }

    public bool AreAllAvatarsReady()
    {
        bool allReady = true;
        foreach(Avatar a in _avatars.Values)
            allReady = allReady && a.readyForSimulation;
        return allReady;
    }

    public static void PrepDebugMessage(NetMQMessage msg)
    {
        if (msg.FrameCount < 2)
        {
            Debug.Log("Resetting message");
            msg.Clear();
            const int numFrames = 1000;
            msg.Append("TESTING");
            for(int i = 0; i < numFrames; ++i)
                msg.Append(shortString);
        }
    }
    
    void Update()
    {
        if (fakeResponses)
        {
            FakeUpdate();
            return;
        }
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
        needShutdown = true;
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

        thread = null;
        clientThread = null;
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
            clientSimulation.SendFrame(CreateMsgJson(MSG_R_ClientJoin).ToJSON(0));
        }
    }

    private void CreateTestClient(ResponseSocket server)
    {
        RequestSocket client = _ctx.CreateRequestSocket();
        client.Connect("tcp://" + hostAddress + ":" + portNumber);
        _avatarClients[server] = client;
        client.SendFrame(CreateMsgJson(MSG_R_ClientJoin).ToJSON(0));
    }

    public Socket mSocket = null;
    public TcpListener serverTcp = null;
    public TcpClient clientTcp = null;
    public volatile bool hasClientMsg = false;
    public volatile bool hasServerMsg = false;
    public volatile bool hasClientMsgToSend = false;
    public volatile bool hasServerMsgToSend = false;
    public volatile bool needShutdown = false;
    public volatile bool testInput = false;
    public DateTime lastServerMsgTime = DateTime.MinValue;
    public DateTime lastClientMsgTime = DateTime.MinValue;
    public Thread thread;
    public Thread clientThread;
    public List<MyMessageInfo> pendingServerMessages = new List<MyMessageInfo>();
    public List<MyMessageInfo> pendingClientMessages = new List<MyMessageInfo>();
    public List<TcpClient> clients = new List<TcpClient>();

    public Queue<MyMessageInfo> outgoingServerMsg = new Queue<MyMessageInfo>();
    public Queue<MyMessageInfo> outgoingTestClientMsg = new Queue<MyMessageInfo>();


    private void OnClientJoin(IAsyncResult result)
    {
        Debug.Log("Got new client connection!");
        TcpClient newClient;
        using (SemaphoreWrapper sem = new SemaphoreWrapper("Clients List"))
        {
            newClient = serverTcp.EndAcceptTcpClient(result);
            clients.Add(newClient);
        }
        newClient.ReceiveBufferSize = 512000;
        newClient.SendBufferSize = 512000;
        Debug.Log("Finished adding new client connection!");
        StreamWriter sw = new StreamWriter(newClient.GetStream());
        MyMessageInfo newInfo = new MyMessageInfo(newClient, "CLIENT JOINED!");
        newInfo.Send(sw);
    }

    private bool ReadServerMessages(TcpClient c)
    {
        if (!c.Connected)
        {
            Debug.LogWarning("Removing disconnected client");
            return false;
        }
        try
        {
            NetworkStream s = c.GetStream();
            if (s.DataAvailable)
            {
                Debug.LogWarning("Server: Has msg to Read " + DateTime.Now.Millisecond);
                StreamReader sr = new System.IO.StreamReader(s);
                using (SemaphoreWrapper sem = new SemaphoreWrapper("Sem Server Messages"))
                {
                    while(s.DataAvailable)
                    {
                        // Save out messages for handling later
                        pendingServerMessages.Add(new MyMessageInfo(c, sr));
                        hasServerMsg = true;
                    }
                }
                Debug.LogWarning("Server: Finished Reading " + DateTime.Now.Millisecond);
            }
        }
        catch(SocketException e)
        {
            // Force disconnect with client
            Debug.LogException(e);
            return false;
        }
        return true;
    }

    private void ServerTcpThread()
    {
        
        System.Net.IPAddress hostIPAddress = System.Net.IPAddress.Parse(hostAddress);//new System.Net.IPAddress(new byte[]{0x7F,0x00, 0x00, 0x01});
        serverTcp = new TcpListener(hostIPAddress, int.Parse(portNumber));
        lastServerMsgTime = DateTime.Now;
        HashSet<TcpClient> toRemove = new HashSet<TcpClient>();
        try
        {
            serverTcp.Start();
            serverTcp.BeginAcceptTcpClient(OnClientJoin, null);
            while(!needShutdown)
            {
                using (SemaphoreWrapper sem = new SemaphoreWrapper("Clients List", false))
                {
                    foreach(TcpClient c in clients)
                    {
                        if (!ReadServerMessages(c))
                            toRemove.Add(c);
                    }
                    while(toRemove.Count > 0)
                    {
                        clients.RemoveAll((TcpClient c)=>{
                            return toRemove.Contains(c);
                        });
                        toRemove.Clear();
                    }
                }
                if (hasServerMsgToSend)
                {
                    Debug.LogWarning("Server: Has some msg to Send " + DateTime.Now.Millisecond);
                    using (SemaphoreWrapper sem = new SemaphoreWrapper("outgoingServerMsg"))
                    {
                        Debug.LogWarning("Server: Has "+outgoingServerMsg.Count+" msg to Send " + DateTime.Now.Millisecond);
                        while (outgoingServerMsg.Count > 0)
                            outgoingServerMsg.Dequeue().Send();
                        hasServerMsgToSend = false;
                        Debug.LogWarning("Server: Finished sending "+outgoingServerMsg.Count+" msg " + DateTime.Now.Millisecond);
                    }
                }
                Thread.Sleep(0);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("ServerTcpThread catch" + Thread.CurrentThread.ManagedThreadId);
            Debug.LogException(e);
        }
        finally
        {
            Debug.LogWarning("ServerTcpThread finally" + Thread.CurrentThread.ManagedThreadId);
            serverTcp.Stop();
        }
    }

    private void TestClientTcpThread()
    {
        clientTcp = new TcpClient(hostAddress, int.Parse(portNumber));
        clientTcp.ReceiveBufferSize = 512000;
        clientTcp.SendBufferSize = 512000;
        lastClientMsgTime = DateTime.Now;
        try
        {
            NetworkStream s = clientTcp.GetStream();
            while(!needShutdown && !testInput)
            {
                if (s.DataAvailable)
                {
                    Debug.LogWarning("Client: Has msg to Read " + DateTime.Now.Millisecond);
                    while (s.DataAvailable)
                    {
                        MyMessageInfo newMsg = new MyMessageInfo(clientTcp);
                        newMsg.Read();
                        using (SemaphoreWrapper sem = new SemaphoreWrapper("Sem Client Messages"))
                        {
                            pendingClientMessages.Add(newMsg);
                            hasClientMsg = true;
                        }
                    }
                    Debug.LogWarning("Client: Finished reading " + DateTime.Now.Millisecond);
                }
                if (hasClientMsgToSend)
                {
                    Debug.LogWarning("Client: Has some msg to Send " + DateTime.Now.Millisecond);
                    using (SemaphoreWrapper sem = new SemaphoreWrapper("outgoingTestClientMsg"))
                    {
                        Debug.LogWarning("Client: Has "+outgoingTestClientMsg.Count+" msg to Send " + DateTime.Now.Millisecond);
                        while (outgoingTestClientMsg.Count > 0)
                            outgoingTestClientMsg.Dequeue().Send();
                        hasClientMsgToSend = false;
                        Debug.LogWarning("Client: Finished sending "+outgoingTestClientMsg.Count+" msg to Send " + DateTime.Now.Millisecond);
                    }
                }
                Thread.Sleep(0);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("TestClientTcpThread catch" + Thread.CurrentThread.ManagedThreadId);
            Debug.LogException(e);
        }
        finally
        {
            Debug.LogWarning("TestClientTcpThread finally " + Thread.CurrentThread.ManagedThreadId);
            clientTcp.Close();
        }
    }

    public void CreateNewSocketConnection2()
    {
        Debug.Log("CreateNewSocketConnection2");
        thread = new Thread(ServerTcpThread);
        clientThread = new Thread(TestClientTcpThread);
        thread.Start();
        clientThread.Start();
    }

    private void OnReceiveMessageFromClient(MyMessageInfo m)
    {
        Debug.LogFormat("Read in {0} characters in msg for server.", (m.text == null) ? 0 : m.text.Length);
        MyMessageInfo newInfo = new MyMessageInfo(m.myClient, "TESTING");
        newInfo.text = longerString;
        for(int i = 0; i < 100; ++i)
            newInfo.text += longerString;
//        Debug.LogFormat("Got message from client after {0}", newTime.Subtract(lastServerMsgTime).TotalMilliseconds);
        using (SemaphoreWrapper sem = new SemaphoreWrapper("outgoingServerMsg"))
        {
            outgoingServerMsg.Enqueue(newInfo);
            hasServerMsgToSend = true;
        }
    }

    // Only for TestClient
    private void OnReceiveMessageFromServer(MyMessageInfo m)
    {
        Debug.LogFormat("Read in {0} characters in msg for server.", (m.text == null) ? 0 : m.text.Length);
        MyMessageInfo newInfo = new MyMessageInfo(m.myClient, "CLIENT RESPONSE!");
        newInfo.text = longerString;
        for(int i = 0; i < 100; ++i)
            newInfo.text += longerString;
//        Debug.LogFormat("Got message from client after {0}", newTime.Subtract(lastServerMsgTime).TotalMilliseconds);
        using (SemaphoreWrapper sem = new SemaphoreWrapper("outgoingTestClientMsg"))
        {
            outgoingTestClientMsg.Enqueue(newInfo);
            hasClientMsgToSend = true;
        }
    }

    private void FakeUpdate()
    {
        testInput = Input.GetKey(KeyCode.S);
        if (hasServerMsg)
        {
            DateTime newTime = DateTime.Now;
            Debug.LogFormat("Got message from client after {0}", newTime.Subtract(lastServerMsgTime).TotalMilliseconds);
            lastServerMsgTime = newTime;
            List<MyMessageInfo> needsReplies = new List<MyMessageInfo>();

            // Retrieve messages from the connection thread
            using (SemaphoreWrapper sem = new SemaphoreWrapper("Sem Server Messages"))
            {
                foreach(MyMessageInfo m in pendingServerMessages)
                    needsReplies.Add(m);
                pendingServerMessages.Clear();
                hasServerMsg = false;
            }

            // Process messages on the main thread
            foreach(MyMessageInfo m in needsReplies)
                OnReceiveMessageFromClient(m);
        }

        if (hasClientMsg)
        {
            DateTime newTime = DateTime.Now;
            Debug.LogFormat("Got message from server after {0}", newTime.Subtract(lastClientMsgTime).TotalMilliseconds);
            lastClientMsgTime = newTime;
            List<MyMessageInfo> needsReplies = new List<MyMessageInfo>();

            // Retrieve messages from the connection thread
            using (SemaphoreWrapper sem = new SemaphoreWrapper("Sem Client Messages"))
            {
                foreach(MyMessageInfo m in pendingClientMessages)
                    needsReplies.Add(m);
                pendingClientMessages.Clear();
                hasClientMsg = false;
            }

            // Process messages on the main thread
            foreach(MyMessageInfo m in needsReplies)
                OnReceiveMessageFromServer(m);
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            if (clientThread != null)
            {
                Debug.LogWarning("Aborting client thread");
                clientThread.Abort();
                clientThread = null;
            }
        }
    }

#endregion
#region Receive messages from the client
    public void HandleFrameMessage(ResponseSocket server, NetMQMessage msg)
    {
        if (debugNetworkMessages)
            Debug.LogFormat("Received Msg on Server: {0}", ReadOutMessage(msg));
        if (fakeResponses)
        {
            float newTime = Time.unscaledTime;
            Debug.LogFormat("Server: sinceServer:{0} sinceClient:{1} Total:{2}", newTime - lastServerMsg, newTime - lastClientMsg, newTime);
            lastServerMsg = Time.unscaledTime;
//            _lastMessageSent.Clear();
//            _lastMessage.Append("test");
//            _lastMessageSent.Append(msgData.ToJSON(0));
            PrepDebugMessage(_debugMessage);
            server.SendMultipartMessage(_debugMessage);
//            server.Send("test");
            return;
        }

        string msgHeader = msg.First.ConvertToString();
        JSONClass jsonData = msg.ReadJson(out msgHeader);
        if (jsonData == null)
        {
            Debug.LogError("Invalid message from client! Cannot parse JSON!\n" + ReadOutMessage(msg));
            return;
        }
        if (msgHeader == null)
        {
            Debug.LogError("Invalid message from client! No msg_type!\n" + jsonData.ToJSON(0));
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
                Debug.LogWarningFormat("Invalid message from client! Unknown msg_type '{0}'\n{1}", msgHeader, jsonData.ToJSON(0));
                break;
        }
    }

    public void RecieveClientInput(ResponseSocket server, JSONClass jsonData)
    {
        _avatars[server].HandleNetInput(jsonData);
    }

    public void OnClientJoin(ResponseSocket server, JSONClass data)
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

        if (fakeResponses)
        {
            float newTime = Time.unscaledTime;
            Debug.LogFormat("Client: sinceServer:{0} sinceClient:{1} Total:{2}", newTime - lastServerMsg, newTime - lastClientMsg, newTime);
            lastClientMsg = newTime;
            PrepDebugMessage(_debugMessage);
//            _lastMessageSent.Clear();
//            _lastMessage.Append("test");
//            _lastMessageSent.Append(msgData.ToJSON(0));
            client.SendMultipartMessage(_debugMessage);
//            client.Send("test");
            return;
        }

        string msgHeader = msg.First.ConvertToString();
        JSONClass jsonData = msg.ReadJson(out msgHeader);
        if (jsonData == null)
        {
            Debug.LogError("Invalid message from server! Cannot parse JSON!\n" + ReadOutMessage(msg));
            return;
        }
        if (msgHeader == null)
        {
            Debug.LogError("Invalid message from server! No msg_type!\n" + jsonData.ToJSON(0));
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
                Debug.LogWarningFormat("Invalid message from server! Unknown msg_type '{0}'\n{1}", msgHeader, jsonData.ToJSON(0));
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
    
    public void SimulateClientInput(RequestSocket client, JSONClass jsonData, NetMQMessage msg)
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
        JSONClass msgData = CreateMsgJson(MSG_R_FrameInput);
        myAvatar.myInput.SimulateInputFromController(ref msgData);
        _lastMessageSent.Clear();
        _lastMessageSent.Append(msgData.ToJSON(0));
        client.SendMultipartMessage(_lastMessageSent);
    }
#endregion
    public void SendFrameUpdate(CameraStreamer.CaptureRequest streamCapture, Avatar a)
    {
        _lastMessageSent.Clear();
        JSONClass jsonData = CreateMsgJson(MSG_S_FrameData);
        // TODO: Additional frame message description?
        
        // Look up relationship values for all observed semantics objects
        jsonData["observed_objects"] = new JSONArray();
        jsonData["observed_relations"] = new JSONClass();
        foreach(SemanticObject o in a.observedObjs)
            jsonData["observed_objects"].Add(o.identifier);
        foreach(SemanticRelationship rel in _relationsToTest)
            jsonData["observed_relations"][rel.name] = rel.GetJsonString(a.observedObjs);

        jsonData["avatar_position"] = a.transform.position.ToJson();
        jsonData["avatar_rotation"] = a.transform.rotation.ToJson();
//        // Add in captured frames
//        int numValues = Mathf.Min(streamCapture.shadersList.Count, streamCapture.capturedImages.Count);
//        JSONArray imagesArray = new JSONArray();
//        for(int i = 0; i < numValues; ++i)
//            imagesArray.Add(new JSONData(Convert.ToBase64String(streamCapture.capturedImages[i].pictureBuffer)));
//        jsonData["captured_pngs"] = imagesArray;

        // Send out the real message
        _lastMessageSent.Append(jsonData.ToJSON(0));

        // Add in captured frames(directly, non-JSON)
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

    public static JSONClass CreateMsgJson(string msgType)
    {
        JSONClass ret = new JSONClass();
        ret["msg_type"] = msgType;
        return ret;
    }
}

public class SemaphoreWrapper : IDisposable
{
    public string whichSemaphore = null;
    public bool shouldLog = false;
    public Semaphore mySemaphore = null;
    public static Dictionary<string, Semaphore> activeSemaphores = new Dictionary<string, Semaphore>();
    public SemaphoreWrapper(string id, bool log = false)
    {
        whichSemaphore = id;
        mySemaphore = GetSemaphore(id);
        shouldLog = log;
        if (shouldLog)
            Debug.LogFormat("Waiting {0} for {1}\n{2}", whichSemaphore, Thread.CurrentThread.ManagedThreadId, System.Environment.StackTrace);
        mySemaphore.WaitOne();
        if (shouldLog)
            Debug.LogFormat("Locking {0} for {1}", whichSemaphore, Thread.CurrentThread.ManagedThreadId);
    }


    public static SemaphoreWrapper GetWrapper(string id, bool log = true)
    {
        return new SemaphoreWrapper(id, log);
    }

    public static Semaphore GetSemaphore(string id)
    {
        if (!activeSemaphores.ContainsKey(id))
            activeSemaphores.Add(id, new Semaphore(1,1));
        return activeSemaphores[id];

    }

    public void Dispose()
    {
        if (shouldLog)
            Debug.LogFormat("Awaiting Release {0} for {1}\n{2}", whichSemaphore, Thread.CurrentThread.ManagedThreadId, System.Environment.StackTrace);
        mySemaphore.Release();
        if (shouldLog)
            Debug.LogFormat("Released {0} for {1}", whichSemaphore, Thread.CurrentThread.ManagedThreadId);
    }
}

public class MyMessageInfo
{
    public MyMessageInfo(TcpClient newClient, StreamReader sr)
    {
        myClient = newClient;
        Read();
    }
    public MyMessageInfo(TcpClient newClient, string newText = null)
    {
        myClient = newClient;
        text = newText;
    }
    public string text = null;
    public TcpClient myClient = null;
    public List<byte[]> byteData = new List<byte[]>();
    static private char[] _buffer = null;

    public void Send()
    {
        Send(new StreamWriter(myClient.GetStream()));
    }

    public void Send(StreamWriter sw)
    {
        sw.AutoFlush = false;
        if (!string.IsNullOrEmpty(text) && !text.EndsWith("\n"))
            text += "\n";

        // Create header
        string header = string.Format("{0}", (text == null) ? 0 : text.Length);
        for(int i = 0; i < byteData.Count; ++i)
            header += string.Format(" {0}", byteData[i].Length);

        sw.WriteLine(header);
        if (!string.IsNullOrEmpty(text))
            sw.Write(text);

        for(int i = 0; i < byteData.Count; ++i)
            sw.BaseStream.Write(byteData[i], 0, byteData[i].Length);
        sw.Flush();
    }

    public void Read()
    {
        using (SemaphoreWrapper sem1 = new SemaphoreWrapper("MyMessageInfo:Read"))
        {
            NetworkStream s  = myClient.GetStream();
            StreamReader sr = new StreamReader(s);
            string header = sr.ReadLine();
            string[] dataArrays = header.Split(new char[]{' '});

            for(int i = 0; i < dataArrays.Length; ++i)
            {
                int size = 0;
                if (!int.TryParse(dataArrays[i], out size))
                    Debug.LogWarningFormat("Couldn't parse packet size!: '{0}' in '{1}', Thread: {2}", dataArrays[i], header, Thread.CurrentThread.ManagedThreadId);
                if (size == 0)
                    continue;
                // First one is the json text string, the rest are byte arrays
                if (i == 0)
                {
                    text = "";
                    // Adding check for when Unity wipes out statics in editor on script compiles
                    using (SemaphoreWrapper sem = new SemaphoreWrapper("MyMessageInfo:charBuffer"))
                    {
                        if (_buffer == null)
                            _buffer = new char[65536];
                        int attempts = 0;
                        int numWritten = -1;
                        StringBuilder sb = new StringBuilder(size, size);
                        while(size > 0)
                        {
                            numWritten = Mathf.Min(size, _buffer.Length);
                            numWritten = sr.ReadBlock(_buffer, 0, numWritten);
//                            text += new string(_buffer, 0, numWritten);
                            sb.Append(_buffer, 0, numWritten);
                            size -= numWritten;
                            if (numWritten <= 0 && size > 0)
                            {
                                // DEBUG TEST CODE
                                Debug.LogWarningFormat("Delaying read '{0}' for attempts", numWritten, attempts);
                                sem.mySemaphore.Release();
                                Thread.Sleep(0);
                                sem.mySemaphore.WaitOne();
                                attempts++;
                                if (attempts > 900)
                                    break;
                            }
                        }
                        text = sb.ToString();
                    }
                }
                else
                {
                    byte[] newBytes = new byte[size];
                    s.Read(newBytes, 0, size);
                    byteData.Add(newBytes);
                }
            }
        }
    }
};
