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
        gp.Process();
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