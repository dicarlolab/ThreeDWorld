using UnityEngine;
using System.Collections;

/// <summary>
/// Component that forces Unity Rigidbodies physics to only update
/// when the simulation is telling it to. This ensures an even framerate/time
/// for the agent.
/// </summary>
using System.Collections.Generic;


public static class SimulationManager
{
    enum MyLogLevel {
        LogAll,
        Warning,
        Errors,
        None
    }
#region Fields
    public static int numPhysicsFramesPerUpdate = 5;
    private static int framesToProcess = 0;
    private static int totalFramesProcessed = 0;
    private static bool _hasFinishedInit = false;
    private static NetMessenger myNetMessenger = null;
    private static MyLogLevel logLevel = MyLogLevel.LogAll;
    private static MyLogLevel stackLogLevel = MyLogLevel.Warning;
    private static string logFileLocation = "output_log.txt";
#endregion

#region Properties
    public static bool shouldRun {
        get {
            return framesToProcess > 0;
        }
    }
    
    public static float timeElapsed {
        get {
            return totalFramesProcessed * Time.fixedDeltaTime;
        }
    }
#endregion

    public static bool FinishUpdatingFrames()
    {
        if (framesToProcess > 0)
        {
            --framesToProcess;
            ++totalFramesProcessed;
            Time.timeScale = (framesToProcess > 0) ? 1.0f : 0.0f;
            return framesToProcess == 0;
        }
        return false;
    }

    public static void ToggleUpdates()
    {
        framesToProcess = numPhysicsFramesPerUpdate;
        Time.timeScale = (framesToProcess > 0) ? 1.0f : 0.0f;
        foreach(Avatar a in myNetMessenger.GetAllAvatars())
            a.readyForSimulation = false;
    }
    
    public static void CheckToggleUpdates()
    {
//        Debug.Log("Avatars ready: " + myNetMessenger.AreAllAvatarsReady());
        if (myNetMessenger.AreAllAvatarsReady())
            ToggleUpdates();
    }

    private static bool TestLogLevel(MyLogLevel myLog, LogType testLog)
    {
        switch(testLog)
        {
            case LogType.Log:
                return myLog <= MyLogLevel.LogAll;
            case LogType.Warning:
                return myLog <= MyLogLevel.Warning;
            default:
                return myLog <= MyLogLevel.Errors;
        }
    }

    private static void HandleLog(string logString, string stackTrace, LogType type)
    {
#if !UNITY_EDITOR
        if (TestLogLevel(logLevel, type))
        {
            string output = string.Format("\n{1}: {0}\n", logString, type);
            System.IO.File.AppendAllText(logFileLocation, output);
            if (TestLogLevel(stackLogLevel, type))
            {
                // Write out stack trace
                System.IO.File.AppendAllText(logFileLocation, "\nSTACK: " + System.Environment.StackTrace + "\n");
            }
        }
#endif
    }

    private static void ReadLogLevel(SimpleJSON.JSONNode json, ref MyLogLevel value)
    {
        if (json != null && json.Tag == SimpleJSON.JSONBinaryTag.Value)
        {
            string testVal = json.Value.ToLowerInvariant();
            if (testVal == "log")
                value = MyLogLevel.LogAll;
            if (testVal == "warning")
                value = MyLogLevel.Warning;
            if (testVal == "error")
                value = MyLogLevel.Errors;
            if (testVal == "none")
                value = MyLogLevel.None;
        }
    }

    public static void ParseJsonInfo(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return;
        if (!System.IO.File.Exists(fileName))
        {
            Debug.LogWarningFormat("Couldn't open configuration file at {0}", fileName);
            return;
        }
        string testConfigInfo = System.IO.File.ReadAllText(fileName);
        SimpleJSON.JSONClass json = SimpleJSON.JSON.Parse(testConfigInfo) as SimpleJSON.JSONClass;
        if (json != null)
        {
            ReadLogLevel(json["log_level"], ref logLevel);
            ReadLogLevel(json["stack_log_level"], ref stackLogLevel);
        }
        Debug.LogFormat("Completed reading configuration at {0}", fileName);
    }

    // Should use argument -executeMethod SimulationManager.Init
    public static void Init()
    {
        if (_hasFinishedInit)
            return;
        System.IO.File.WriteAllText(logFileLocation, "Starting Initialization:\n");
        Application.logMessageReceived += HandleLog;
        List<string> args = new List<string>(System.Environment.GetCommandLineArgs());
        string configLocation = null;
        // Parse arguments
        {
            string output = "Args: ";
            string curFlag = null;
            foreach (string arg in args)
            {
                output += "'" + arg + "' ";
                if (curFlag == "-config")
                    configLocation = arg;
                if (arg.StartsWith("-"))
                    curFlag = arg;
                else
                    curFlag = null;
            }
            Debug.Log(output);
        }

        ParseJsonInfo(configLocation);

        // Init NetMessenger
        myNetMessenger = GameObject.FindObjectOfType<NetMessenger>();
        if (myNetMessenger != null)
            myNetMessenger.Init();
        else
            Debug.LogWarning("Couldn't find a NetMessenger to Initialize!");

        _hasFinishedInit = true;
    }
}
