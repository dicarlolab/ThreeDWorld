using UnityEditor;
using UnityEngine;
using System.Collections;

[CustomEditor(typeof(SemanticObject))]
public class SemanticObjectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        SemanticObject semObj = target as SemanticObject;
        if (GUILayout.Button("SetDefaultSubObjects"))
            semObj.GetDefaultLayout();
    }
}
