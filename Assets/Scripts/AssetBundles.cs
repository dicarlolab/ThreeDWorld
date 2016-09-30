using UnityEngine;
using UnityEditor;
using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;



public class CreateAssetBundles
{
	public Dictionary<GameObject, string> ListSelectedPrefabs()
	/*
	 * This function walks through all the objects selected in the editor and returns 
	 * the list of selected prefabs.
	 */
	{
		Dictionary<GameObject, string> allSelected = new Dictionary<GameObject, string>();

		foreach(UnityEngine.Object obj in Selection.objects)
		{
			if (obj != null)
			{
				if (obj is GameObject) {
					allSelected.Add (obj as GameObject, AssetDatabase.GetAssetPath (obj));
				} else if (obj is DefaultAsset) {
					HashSet<GameObject> children = (obj as DefaultAsset).GetAllChildrenAssets<GameObject> ();
					foreach (GameObject child in children) {
						allSelected.Add (child, AssetDatabase.GetAssetPath (child));
					}
				}
			}
		}

		if (allSelected == null || allSelected.Count == 0)
			return null;

		List<GameObject> toRemove = new List<GameObject>();
		foreach (KeyValuePair<GameObject, string> entry in allSelected) {
//			Debug.Log (PrefabUtility.GetPrefabType (entry.Key).ToString());
			if (entry.Key == null || PrefabUtility.GetPrefabType (entry.Key) != PrefabType.Prefab) {
				toRemove.Add (entry.Key);
			}
		}

		foreach (GameObject entry in toRemove) {
			allSelected.Remove (entry);
		}
		return allSelected;
	}
	[MenuItem ("Assets/Build AssetBundles/Merged")]

	static void BuildAllAssetBundlesMerged ()
	{
		string defaultName = "default_bundle.bundle";
		// Build all bundles existing in the project
//		BuildPipeline.BuildAssetBundles ("Assets/AssetBundles/", BuildAssetBundleOptions.None, BuildTarget.StandaloneOSXUniversal);
		// For building bundles selectively
		CreateAssetBundles bundlesClass = new CreateAssetBundles();
		Dictionary<GameObject, string> selectedPrefabs = bundlesClass.ListSelectedPrefabs();
		AssetBundleBuild[] buildMap = new AssetBundleBuild[1];
		string[] assetPath = new string[selectedPrefabs.Count];

		int loop_counter = 0;
		foreach (KeyValuePair<GameObject, string> entry in selectedPrefabs) {
			assetPath [loop_counter] = entry.Value;
			loop_counter++;
		}
		buildMap [0].assetBundleName = defaultName;
		buildMap [0].assetNames = assetPath;
		BuildPipeline.BuildAssetBundles ("Assets/AssetBundles/Merged", 
			buildMap, 
			BuildAssetBundleOptions.None,
			BuildTarget.StandaloneOSXUniversal
		);
	}

	[MenuItem ("Assets/Build AssetBundles/Separate")]

	static void BuildAllAssetBundlesSeparate ()
	{
		// Build all bundles existing in the project
		//		BuildPipeline.BuildAssetBundles ("Assets/AssetBundles/", BuildAssetBundleOptions.None, BuildTarget.StandaloneOSXUniversal);
		// For building bundles selectively
		CreateAssetBundles bundlesClass = new CreateAssetBundles();
		Dictionary<GameObject, string> selectedPrefabs = bundlesClass.ListSelectedPrefabs();
		AssetBundleBuild[] buildMap = new AssetBundleBuild[selectedPrefabs.Count];

		int loop_counter = 0;
		foreach (KeyValuePair<GameObject, string> entry in selectedPrefabs) {
			string _bundleName;
			string[] _assetPath = new string[1];
			_bundleName = entry.Key.name + ".bundle";
			// In case prefabs had some kind of file extension replace the file extension with .bundle
//			_bundleName = _bundleName.Remove(_bundleName.LastIndexOf('.')) + ".bundle";

			buildMap [loop_counter].assetBundleName = _bundleName;
			_assetPath [0] = entry.Value;
			buildMap [loop_counter].assetNames = _assetPath;
			loop_counter++;
		}

		BuildPipeline.BuildAssetBundles ("Assets/AssetBundles/Separated", 
			buildMap, 
			BuildAssetBundleOptions.None,
			BuildTarget.StandaloneOSXUniversal
		);
	}
}


public class GetAssetBundleNames
{
	[MenuItem ("Assets/Get AssetBundle names")]
	static void GetNames ()
	{
		var names = AssetDatabase.GetAllAssetBundleNames ();
		foreach (var name in names) {
			Debug.Log ("AssetBundle: " + name);
			var asset = AssetBundle.LoadFromFile (System.IO.Path.Combine ("Assets/AssetBundles", name));
			var assetNames = asset.GetAllAssetNames ();
			foreach (var assetName in assetNames)
				Debug.Log ("Objects: " + assetName);
			asset.Unload (false);
		}
	}
}

//public class SetAssetBundleLabel
//{
//	[MenuItem ("Set AssetBundle Labels")]
//	static void SetLabels ()
//	{
//		Dictionary<GameObject, string> allSelected = new Dictionary<GameObject, string>();
//
//		foreach (GameObject obj in Selection.gameObjects) {
//			allSelected.Add (obj, AssetDatabase.GetAssetPath (obj));
//		}
//
//		foreach(UnityEngine.Object obj in Selection.objects)
//		{
//			if (obj != null)
//			{
//				if (obj is GameObject) {
//					allSelected.Add (obj as GameObject, AssetDatabase.GetAssetPath (obj));
//				} else if (obj is DefaultAsset) {
//					HashSet<GameObject> children = (obj as DefaultAsset).GetAllChildrenAssets<GameObject> ();
//					foreach (GameObject child in children) {
//						allSelected.Add (child, AssetDatabase.GetAssetPath (child));
//					}
//				}
//			}
//		}
//		if (allSelected == null || allSelected.Count == 0)
//			return;
//
//		List<GameObject> toRemove = new List<GameObject>();
//		foreach (KeyValuePair<GameObject, string> entry in allSelected) {
//			if (entry.Key == null || PrefabUtility.GetPrefabType (entry.Key) != PrefabType.ModelPrefab) {
//				toRemove.Add (entry.Key);
//			}
//		}
//
//		foreach (GameObject entry in toRemove) {
//			allSelected.Remove (entry);
//		}
//		foreach (KeyValuePair<GameObject, string> entry in allSelected) {
//			
//		}
//	}
//}