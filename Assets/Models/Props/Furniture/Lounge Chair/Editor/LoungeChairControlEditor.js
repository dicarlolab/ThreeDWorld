#pragma strict

@CustomEditor(LoungeChairControl)
@CanEditMultipleObjects

class LoungeChairControlEditor extends Editor {
	
	
	function OnInspectorGUI () {

		EditorGUILayout.LabelField("Set a Random Materials Combination (30 total)");
		if(GUILayout.Button("PRESS TO RANDOMIZE")) {
		var obj = GameObject.Find("Lounge Chair One");
		obj.SendMessage("ChangeMaterials", SendMessageOptions.DontRequireReceiver);
		}
				
		DrawDefaultInspector ();
    }
}