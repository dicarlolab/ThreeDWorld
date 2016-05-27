using UnityEditor;
using UnityEngine;
using System.Collections;

[InitializeOnLoad]
public class ProceduralGenerationProcessing
{
    static ProceduralGenerationProcessing()
    {
        UnityEditor.PrefabUtility.prefabInstanceUpdated += OnPrefabInstanceUpdate;
    }

    static void OnPrefabInstanceUpdate(GameObject instance)
    {
        GeneratablePrefab gpInstance = instance.GetComponent<GeneratablePrefab>();
        if (gpInstance == null)
            return;
        GameObject prefab = UnityEditor.PrefabUtility.GetPrefabParent(instance) as GameObject;        
        GeneratablePrefab gp = prefab.GetComponent<GeneratablePrefab>();
        gp.ProcessPrefab();
        ProceduralGeneration [] allThings = Resources.LoadAll<ProceduralGeneration>("");
        if (allThings != null && allThings.Length > 0)
            allThings[0].SavePrefabInformation(gp, false, true);
    }
}


[CustomEditor(typeof(ProceduralGeneration))]
public class ProceduralGenerationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        ProceduralGeneration pgComp = target as ProceduralGeneration;
        if (GUILayout.Button("CreateRoom"))
            pgComp.CreateRoom(pgComp.roomDim, Vector3.zero);
        if (GUILayout.Button("Init"))
            pgComp.Init();
    }
}