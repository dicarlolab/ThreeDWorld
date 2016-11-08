using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.IO;
using System.Text;
using UnityEditor;

public class CreatePrefabCMD
{
        private static string GetArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name && args.Length > i + 1)
                {
                    return args[i + 1];
                }
            }
            return null;
        }

	public static void CreatePrefabFromModel ()
	{
		Dictionary<GameObject, string> allSelected = new Dictionary<GameObject, string> ();

                /*
		foreach (GameObject obj in Selection.gameObjects) {

			allSelected.Add (obj, AssetDatabase.GetAssetPath (obj));

                        tmp_asset_path   = AssetDatabase.GetAssetPath (obj);
                        Debug.Log(tmp_asset_path);
		}

		foreach (UnityEngine.Object obj in Selection.objects) {
			if (obj != null) {
				if (obj is GameObject) {
					allSelected.Add (obj as GameObject, AssetDatabase.GetAssetPath (obj));

                                        tmp_asset_path   = AssetDatabase.GetAssetPath (obj);
                                        Debug.Log(tmp_asset_path);

				} else if (obj is DefaultAsset) {
					HashSet<GameObject> children = (obj as DefaultAsset).GetAllChildrenAssets<GameObject> ();
					foreach (GameObject child in children) {
						allSelected.Add (child, AssetDatabase.GetAssetPath (child));

                                                tmp_asset_path   = AssetDatabase.GetAssetPath (child);
                                                Debug.Log(tmp_asset_path);
                                                //Debug.Log(AssetDatabase.LoadMainAssetAtPath(tmp_asset_path)==child);
					}
				}
			}
		}
                */

                //string tmp_asset_path = "Assets/Models/sel_objs/02747177/8acbba54310b76cb6c43a5634bf2724/8acbba54310b76cb6c43a5634bf2724.obj";
                //string tmp_asset_path = "Assets/Models/sel_objs/test_mine/";
                string tmp_asset_path = GetArg ("-outputDir");;
                string[] dir = Directory.GetDirectories(tmp_asset_path);
                foreach (string d_tmp in dir) {
                    //Debug.Log(d_tmp);
                    string data_new_path = d_tmp + d_tmp.Remove(0, d_tmp.LastIndexOf ('/')) + ".obj";
                    Debug.Log(data_new_path);

                    allSelected.Add (AssetDatabase.LoadMainAssetAtPath(data_new_path) as GameObject, data_new_path);
                }
                //allSelected.Add (AssetDatabase.LoadMainAssetAtPath(tmp_asset_path) as GameObject, tmp_asset_path);

		if (allSelected == null || allSelected.Count == 0)
			return;

		List<GameObject> toRemove = new List<GameObject> ();
		foreach (KeyValuePair<GameObject, string> entry in allSelected) {
			if (entry.Key == null || PrefabUtility.GetPrefabType (entry.Key) != PrefabType.ModelPrefab) {
				toRemove.Add (entry.Key);
			}
		}

		foreach (GameObject entry in toRemove) {
			allSelected.Remove (entry);
		}

		foreach (KeyValuePair<GameObject, string> entry in allSelected) {
			string loadingDirectory = "Assets/Models/";

			if (!entry.Value.StartsWith (loadingDirectory)) {
				Debug.LogError ("Selected Assets are not contained within " + loadingDirectory + " directory. Please move or copy assets to this directory, select them from there and try again.");
			} else {
				//get rid of location in hierarchy

				string subPathWithFileType = entry.Value.Remove (0, loadingDirectory.Length);
				//get rid of file type
                                //Debug.Log(subPathWithFileType);
				string subPathWithoutFileType = subPathWithFileType.Remove (subPathWithFileType.LastIndexOf ('.'));
				//get rid of file name
				string subPathWithoutFilename = subPathWithoutFileType.Remove (subPathWithoutFileType.LastIndexOf ('/'));

				//if directory does not already exist, make one
//				System.IO.Directory.CreateDirectory(Application.dataPath + "/Resources/Prefabs/Converted Models/" + subPathWithoutFilename);
				System.IO.Directory.CreateDirectory (Application.dataPath + "/PrefabDatabase/GeneratedPrefabs/" + subPathWithoutFilename);

				Debug.Log (Application.dataPath + "/PrefabDatabase/GeneratedPrefabs/" + subPathWithoutFilename);

				//make meta file
				EditorApplication.Step ();

				//make prefab
				MakeSimplePrefabObj (entry.Key, subPathWithoutFileType);

				//force GC
				EditorApplication.SaveAssets ();
				Resources.UnloadUnusedAssets ();
				EditorUtility.UnloadUnusedAssetsImmediate ();

				GC.Collect ();
				EditorApplication.Step ();

			}
		}
                
                allSelected = new Dictionary<GameObject, string> ();

                tmp_asset_path = GetArg ("-outputDir");
                tmp_asset_path = tmp_asset_path.Replace("/Models/", "/PrefabDatabase/GeneratedPrefabs/");
                dir = Directory.GetDirectories(tmp_asset_path);
                foreach (string d_tmp in dir) {
                    //Debug.Log(d_tmp);
                    string data_new_path = d_tmp + d_tmp.Remove(0, d_tmp.LastIndexOf ('/')) + ".prefab";
                    Debug.Log(data_new_path);
                    allSelected.Add (AssetDatabase.LoadMainAssetAtPath(data_new_path) as GameObject, data_new_path);
                }

		AssetBundleBuild[] buildMap = new AssetBundleBuild[allSelected.Count];

		int loop_counter = 0;
		foreach (KeyValuePair<GameObject, string> entry in allSelected) {
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

		BuildPipeline.BuildAssetBundles ("Assets/PrefabDatabase/AssetBundles/Separated", 
			buildMap, 
			BuildAssetBundleOptions.None,
			BuildTarget.StandaloneOSXUniversal
		);
	}

	private static void MakeSimplePrefabObj (GameObject obj, string subPath)
	{
		Debug.LogFormat ("MakeSimplePrefabObj {0}", obj.name);
		// Find vrml text if we have it
		string vrmlText = null;
		string vrmlPath = AssetDatabase.GetAssetPath (obj);
		if (vrmlPath != null) {
			vrmlPath = vrmlPath.Replace (".obj", ".wrl");
			string fullPath = System.IO.Path.Combine (Application.dataPath, vrmlPath.Substring (7));
			if (!System.IO.File.Exists (fullPath))
				fullPath = fullPath.Replace (".wrl", "_vhacd.wrl");
			if (System.IO.File.Exists (fullPath)) {
				using (System.IO.StreamReader reader = new System.IO.StreamReader (fullPath)) {
					vrmlText = reader.ReadToEnd ();
				}
			} else
				Debug.LogFormat ("Cannot find {0}\n{1}", vrmlPath, fullPath);
		}

		GameObject instance = GameObject.Instantiate (obj) as GameObject;
		instance.name = obj.name;

		// Remove any old colliders.
		Collider[] foundColliders = instance.transform.GetComponentsInChildren<Collider> ();
		foreach (Collider col in foundColliders)
			UnityEngine.Object.DestroyImmediate (col, true);

		// Create SemanticObject/Rigidbody
		instance.AddComponent<SemanticObjectSimple> ().name = instance.name;

		// Add generatable prefab tags
		instance.AddComponent<GeneratablePrefab> ();

		//NormalSolver.RecalculateNormals (instance.GetComponentInChildren<MeshFilter> ().sharedMesh, 60);

		// Save as a prefab
//		string prefabAssetPath = string.Format("Assets/Resources/Prefabs/Converted Models/{0}.prefab", subPath);
		string prefabAssetPath = string.Format ("Assets/PrefabDatabase/GeneratedPrefabs/{0}.prefab", subPath);
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject> (prefabAssetPath);
		if (prefab == null)
			prefab = PrefabUtility.CreatePrefab (prefabAssetPath, instance);
		else
			prefab = PrefabUtility.ReplacePrefab (instance, prefab);
		
		// Create colliders for the prefab
		ConcaveCollider.FH_CreateColliders (prefab, vrmlText, true);

		// Save out updated metadata settings
		GeneratablePrefab metaData = prefab.GetComponent<GeneratablePrefab> ();
		metaData.ProcessPrefab ();

		EditorUtility.SetDirty (prefab);
		try {
			EditorUtility.SetDirty (instance);
		} catch {
			Debug.LogFormat("Instance could not be collected: {0}", instance);
		}
		GameObject.Destroy (instance);
	}
}

