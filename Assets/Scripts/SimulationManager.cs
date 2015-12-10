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
#region Fields
    public static int numPhysicsFramesPerUpdate = 5;
    private static int framesToProcess = 0;
    private static int totalFramesProcessed = 0;
    private static bool _hasFinishedInit = false;
    private static NetMessenger myNetMessenger = null;
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
    }
    
    public static void CheckToggleUpdates()
    {
        Debug.Log("Avatars ready: " + myNetMessenger.AreAllAvatarsReady());
        if (myNetMessenger.AreAllAvatarsReady())
            ToggleUpdates();
    }

    private static void HandleLog(string logString, string stackTrace, LogType type)
    {
#if !UNITY_EDITOR
        string output = string.Format("{1}: {0}", logString, type);
        System.Console.Write(output);
        System.Console.WriteLine();
        if (type != LogType.Log && type != LogType.Warning)
        {
            // Write out stack trace
            System.Console.Write(stackTrace);
            System.Console.WriteLine();
        }
#endif
    }

    // Should use argument -executeMethod SimulationManager.Init
    public static void Init()
    {
        if (_hasFinishedInit)
            return;
        Application.logMessageReceived += HandleLog;
        List<string> args = new List<string>(System.Environment.GetCommandLineArgs());
//        if (args.Contains("test_arg"))
        {
            string output = "Args: ";
            foreach (string arg in args)
                output += "'" + arg + "' ";
            Debug.Log(output);
        }

        // Init NetMessenger
        myNetMessenger = GameObject.FindObjectOfType<NetMessenger>();
        if (myNetMessenger != null)
            myNetMessenger.Init();
        else
            Debug.LogWarning("Couldn't find a NetMessenger to Initialize!");

        _hasFinishedInit = true;
    }
}
