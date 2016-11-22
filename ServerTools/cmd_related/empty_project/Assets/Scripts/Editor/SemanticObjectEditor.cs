using UnityEditor;
using UnityEngine;
using System.Collections;

[CustomEditor(typeof(SemanticObjectComplex))]
public class SemanticObjectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        SemanticObjectComplex semObj = target as SemanticObjectComplex;
        if (GUILayout.Button("SetDefaultSubObjects"))
            semObj.GetDefaultLayout();
    }
}
